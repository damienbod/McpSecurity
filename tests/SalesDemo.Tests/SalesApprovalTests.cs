using System.Runtime.CompilerServices;
using McpWebClient.AiServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using ToolsLibrary.Data;

namespace SalesDemo.Tests;

public class SalesApprovalTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WritesNeedApprovalAndCannotBeReplayed(bool approve)
    {
        var store = new SalesDataStore();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var chat = new ToolCallingClient();
        var function = AIFunctionFactory.Create(
            (string customerId, string text) => store.AddNote(customerId, text),
            "add_customer_follow_up_note");
        var service = new SalesAssistantService(new ConfigurationBuilder().Build(), null!, cache,
            _ => Task.FromResult(new SalesAssistantService.ClientSetup(chat, [function], new EmptyConnection())));

        var first = await service.BeginAsync("user", "Add a note", null!);
        var approval = Assert.Single(first.PendingApprovals);
        Assert.Contains("fabrikam-gmbh", approval.Arguments);
        Assert.Empty(store.GetNotes("fabrikam-gmbh"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RespondToApprovalAsync("other-user", approval.Id, true, null!));

        var response = await service.RespondToApprovalAsync("user", approval.Id, approve, null!);
        Assert.Empty(response.PendingApprovals);
        Assert.Equal(approve ? 1 : 0, store.GetNotes("fabrikam-gmbh").Count);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RespondToApprovalAsync("user", approval.Id, true, null!));
        Assert.Equal(approve ? 1 : 0, store.GetNotes("fabrikam-gmbh").Count);
    }

    [Fact]
    public async Task MultipleApprovalsWaitForEveryDecisionAndClearDiscardsThem()
    {
        var store = new SalesDataStore();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var chat = new ToolCallingClient(twoCalls: true);
        var function = AIFunctionFactory.Create(
            (string customerId, string text) => store.AddNote(customerId, text),
            "add_customer_follow_up_note");
        var service = new SalesAssistantService(new ConfigurationBuilder().Build(), null!, cache,
            _ => Task.FromResult(new SalesAssistantService.ClientSetup(chat, [function], new EmptyConnection())));

        var initial = await service.BeginAsync("user", "Add two notes", null!);
        Assert.Equal(2, initial.PendingApprovals.Count);
        var afterFirst = await service.RespondToApprovalAsync("user", initial.PendingApprovals[0].Id, true, null!);
        Assert.Single(afterFirst.PendingApprovals);
        Assert.Empty(store.GetNotes("fabrikam-gmbh"));
        Assert.Single((await service.GetCurrentAsync("user"))!.PendingApprovals);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ContinueAsync("user", "Hi", null!));
        var afterSecond = await service.RespondToApprovalAsync("user", initial.PendingApprovals[1].Id, false, null!);
        Assert.Empty(afterSecond.PendingApprovals);
        Assert.Single(store.GetNotes("fabrikam-gmbh"));

        var otherChat = new ToolCallingClient();
        var otherService = new SalesAssistantService(new ConfigurationBuilder().Build(), null!, cache,
            _ => Task.FromResult(new SalesAssistantService.ClientSetup(otherChat, [function], new EmptyConnection())));
        var next = await otherService.BeginAsync("other-user", "Add note", null!);
        var pendingId = Assert.Single(next.PendingApprovals).Id;
        await otherService.ClearAsync("other-user");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            otherService.RespondToApprovalAsync("other-user", pendingId, true, null!));
        Assert.Single(store.GetNotes("fabrikam-gmbh"));
    }

    [Fact]
    public async Task ConcurrentDuplicateApprovalExecutesOnlyOnce()
    {
        var store = new SalesDataStore();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var chat = new ToolCallingClient();
        var function = AIFunctionFactory.Create(
            (string customerId, string text) => store.AddNote(customerId, text),
            "add_customer_follow_up_note");
        var service = new SalesAssistantService(new ConfigurationBuilder().Build(), null!, cache,
            _ => Task.FromResult(new SalesAssistantService.ClientSetup(chat, [function], new EmptyConnection())));
        var pending = Assert.Single((await service.BeginAsync("user", "Add note", null!)).PendingApprovals);

        async Task<bool> TryApprove()
        {
            try { await service.RespondToApprovalAsync("user", pending.Id, true, null!); return true; }
            catch (InvalidOperationException) { return false; }
        }

        var results = await Task.WhenAll(TryApprove(), TryApprove());
        Assert.Single(results, r => r);
        Assert.Single(store.GetNotes("fabrikam-gmbh"));
    }

    [Fact]
    public async Task FailedResumeClearsConversationRatherThanReplayingAnUncertainWrite()
    {
        var store = new SalesDataStore();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var chat = new ToolCallingClient(failAfterCall: true);
        var function = AIFunctionFactory.Create(
            (string customerId, string text) => store.AddNote(customerId, text),
            "add_customer_follow_up_note");
        var service = new SalesAssistantService(new ConfigurationBuilder().Build(), null!, cache,
            _ => Task.FromResult(new SalesAssistantService.ClientSetup(chat, [function], new EmptyConnection())));
        var pending = Assert.Single((await service.BeginAsync("user", "Add note", null!)).PendingApprovals);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RespondToApprovalAsync("user", pending.Id, true, null!));
        Assert.Contains("Check Follow-up notes", error.Message);
        Assert.Single(store.GetNotes("fabrikam-gmbh"));
        Assert.Null(await service.GetCurrentAsync("user"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RespondToApprovalAsync("user", pending.Id, true, null!));
        Assert.Single(store.GetNotes("fabrikam-gmbh"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StatusChangeRequiresApprovalAndExpiredApprovalCannotRun(bool expire)
    {
        var store = new SalesDataStore();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var chat = new ToolCallingClient(functionName: "update_order_status",
            arguments: new Dictionary<string, object?> { ["orderId"] = "ORD-011", ["status"] = "Delayed" });
        var function = AIFunctionFactory.Create(
            (string orderId, string status) => store.UpdateOrderStatus(orderId, Enum.Parse<ToolsLibrary.Models.OrderStatus>(status), null),
            "update_order_status");
        var service = new SalesAssistantService(new ConfigurationBuilder().Build(), null!, cache,
            _ => Task.FromResult(new SalesAssistantService.ClientSetup(chat, [function], new EmptyConnection())));
        var pending = Assert.Single((await service.BeginAsync("user", "Mark delayed", null!)).PendingApprovals);
        Assert.Equal(ToolsLibrary.Models.OrderStatus.InProgress, store.GetAllOrders().Single(o => o.Id == "ORD-011").Status);
        if (expire)
        {
            cache.Remove("sales-session:user");
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RespondToApprovalAsync("user", pending.Id, true, null!));
        }
        else
        {
            await service.RespondToApprovalAsync("user", pending.Id, true, null!);
        }
        Assert.Equal(expire ? ToolsLibrary.Models.OrderStatus.InProgress : ToolsLibrary.Models.OrderStatus.Delayed,
            store.GetAllOrders().Single(o => o.Id == "ORD-011").Status);
    }

    [Fact]
    public void ContextMergesRepeatedCustomerSearchesAndOrderResults()
    {
        var messages = new[]
        {
            new ChatMessage(ChatRole.Assistant,
            [
                new FunctionCallContent("a", "search_customers", new Dictionary<string, object?>()),
                new FunctionCallContent("b", "get_customer_overview", new Dictionary<string, object?>()),
                new FunctionCallContent("c", "search_orders", new Dictionary<string, object?>())
            ]),
            new ChatMessage(ChatRole.Tool,
            [
                new FunctionResultContent("a", "[{\"id\":\"fabrikam-gmbh\",\"name\":\"Fabrikam GmbH\",\"tier\":\"A\"}]"),
                new FunctionResultContent("b", "{\"id\":\"helvetic-pharma\",\"name\":\"Helvetic Pharma\",\"tier\":\"A\"}"),
                new FunctionResultContent("c", "[{\"id\":\"ORD-012\",\"customerId\":\"alpine-bikes\",\"customerName\":\"Alpine Bikes\",\"status\":\"OnTime\",\"value\":6500}]")
            ])
        };
        var (customers, orders) = SalesAssistantService.ExtractContextFromToolResults(messages);
        Assert.Equal(3, customers.Count);
        Assert.Contains(customers, c => c.Id == "helvetic-pharma" && c.Tier == "A");
        Assert.Contains(customers, c => c.Id == "alpine-bikes");
        Assert.Single(orders);
    }

    private sealed class EmptyConnection : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ToolCallingClient(bool twoCalls = false, bool failAfterCall = false,
        string functionName = "add_customer_follow_up_note", IDictionary<string, object?>? arguments = null) : IChatClient
    {
        private int _calls;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (_calls++ == 0)
            {
                var call = new FunctionCallContent("call-1", functionName,
                    arguments ?? new Dictionary<string, object?> { ["customerId"] = "fabrikam-gmbh", ["text"] = "Call about delivery" });
                var calls = new List<AIContent> { call };
                if (twoCalls)
                    calls.Add(new FunctionCallContent("call-2", "add_customer_follow_up_note",
                        new Dictionary<string, object?> { ["customerId"] = "fabrikam-gmbh", ["text"] = "Second note" }));
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, calls)));
            }
            if (failAfterCall) throw new InvalidOperationException("Simulated model failure");
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Done")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
