using McpWebClient.AiServices;
using ModelContextProtocol.Protocol;

namespace SalesDemo.Tests;

public class SalesNotesTests
{
    private static readonly string[] AvailableTools = ["get_all_customers", "get_customer_follow_up_notes"];
    [Fact]
    public async Task BrowserListsCustomersAndLoadsLatestNotesForSelectedCustomer()
    {
        var calls = new List<string>();
        Task<CallToolResult> Call(string name, IReadOnlyDictionary<string, object?>? args)
        {
            calls.Add(name);
            if (name == "get_all_customers")
                return Task.FromResult(Result("""[{"id":"fabrikam-gmbh","name":"Fabrikam GmbH","industry":"Retail","tier":"A","accountManager":"Marco"}]"""));
            Assert.Equal("fabrikam-gmbh", args!["customerId"]);
            return Task.FromResult(Result("""
                [{"id":"1","customerId":"fabrikam-gmbh","text":"Earlier","createdAtUtc":"2025-01-01T10:00:00Z"},
                 {"id":"2","customerId":"fabrikam-gmbh","text":"Call about delivery dates","createdAtUtc":"2025-01-02T10:00:00Z"}]
                """));
        }

        var initial = await SalesAssistantService.LoadNotesAsync(null, Call, AvailableTools);
        Assert.Single(initial.Customers);
        Assert.Null(initial.SelectedCustomerId);
        Assert.Single(calls);

        var selected = await SalesAssistantService.LoadNotesAsync("fabrikam-gmbh", Call, AvailableTools);
        Assert.Equal("fabrikam-gmbh", selected.SelectedCustomerId);
        Assert.Equal("Call about delivery dates", selected.Notes[0].Text);
        Assert.Equal(2, selected.Notes.Count);
    }

    [Fact]
    public async Task EmptyNotesAndErrorsAreDistinguished()
    {
        Task<CallToolResult> Empty(string name, IReadOnlyDictionary<string, object?>? _) =>
            Task.FromResult(Result(name == "get_all_customers"
                ? """[{"id":"fabrikam-gmbh","name":"Fabrikam GmbH","industry":"","tier":"","accountManager":""}]"""
                : "[]"));
        var view = await SalesAssistantService.LoadNotesAsync("fabrikam-gmbh", Empty, AvailableTools);
        Assert.Empty(view.Notes);
        await Assert.ThrowsAsync<InvalidOperationException>(() => SalesAssistantService.LoadNotesAsync("missing", Empty, AvailableTools));

        Task<CallToolResult> Failed(string _, IReadOnlyDictionary<string, object?>? __) =>
            Task.FromResult(Result("Not authorized", error: true));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SalesAssistantService.LoadNotesAsync(null, Failed, AvailableTools));
        Assert.Contains("Not authorized", error.Message);

        Task<CallToolResult> Malformed(string _, IReadOnlyDictionary<string, object?>? __) => Task.FromResult(Result("not json"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => SalesAssistantService.LoadNotesAsync(null, Malformed, AvailableTools));
    }

    [Fact]
    public async Task OlderServerReportsMissingNotesToolBeforeCallingIt()
    {
        var called = false;
        Task<CallToolResult> Call(string _, IReadOnlyDictionary<string, object?>? __)
        {
            called = true;
            return Task.FromResult(Result("[]"));
        }
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SalesAssistantService.LoadNotesAsync(null, Call, ["get_all_customers"]));
        Assert.Contains("connected MCP server does not provide the sales notes tools", error.Message);
        Assert.Contains("SalesMcpServerUrl", error.Message);
        Assert.False(called);
    }

    private static CallToolResult Result(string text, bool error = false) => new()
    {
        IsError = error,
        Content = [new TextContentBlock { Text = text }]
    };
}
