using System.Net.Http.Headers;
using System.Text.Json;
using ClientLibrary;
using McpWebClient.AiServices.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Identity.Web;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace McpWebClient.AiServices;

public class SalesAssistantService
{
    private const int MaxHistoryMessages = 40;
    private const string SystemPrompt =
        "You are a Sales Assistant with direct access to sales data through built-in tools. " +
        "Use tools to retrieve data when needed, then answer concisely based on the results. " +
        "Do NOT ask the user to provide data or connect external systems — the data is already available. " +
        "You may call a tool multiple times when comparing customers. " +
        "Write tools change shared demo data and require explicit approval; do not claim changes happened before they succeed. " +
        "The sample orders are from 2025; distinguish delivered delays from undelivered orders. " +
        "When assessing customer risk, consider: tier (A = most critical), delay count, delay duration, and order value.";

    private static readonly HashSet<string> ReadTools = [
        "get_all_customers", "get_all_orders", "get_customer_orders", "get_delayed_orders",
        "search_customers", "search_orders", "get_customer_overview", "get_customer_follow_up_notes"
    ];
    private static readonly HashSet<string> WriteTools = ["update_order_status", "add_customer_follow_up_note"];
    private static readonly object CacheLock = new();

    private readonly IConfiguration _configuration;
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly IMemoryCache _sessionCache;
    private readonly Func<IHttpClientFactory, Task<ClientSetup>> _createClients;
    private readonly ILogger<SalesAssistantService> _logger;
    private static readonly MemoryCacheEntryOptions SessionCacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(30),
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(8)
    };

    public SalesAssistantService(
        IConfiguration configuration,
        ITokenAcquisition tokenAcquisition,
        IMemoryCache sessionCache,
        ILogger<SalesAssistantService> logger)
    {
        _configuration = configuration;
        _tokenAcquisition = tokenAcquisition;
        _sessionCache = sessionCache;
        _logger = logger;
        _createClients = CreateClientsAsync;
    }

    internal SalesAssistantService(IConfiguration configuration, ITokenAcquisition tokenAcquisition,
        IMemoryCache sessionCache, Func<IHttpClientFactory, Task<ClientSetup>> createClients)
        : this(configuration, tokenAcquisition, sessionCache, NullLogger<SalesAssistantService>.Instance) => _createClients = createClients;

    internal sealed record ClientSetup(IChatClient ChatClient, IList<AIFunction> Tools, IAsyncDisposable Connection);

    public async Task<SalesResponse> BeginAsync(string userKey, string prompt, IHttpClientFactory clientFactory)
    {
        var session = GetOrCreateSession(userKey);
        await session.Gate.WaitAsync();
        try
        {
            EnsureCurrentSession(userKey, session);
            session.History.Clear();
            session.Pending.Clear();
            session.ApprovalResponses.Clear();
            session.LastResponse = null;
            return await ChatAsync(session, prompt, clientFactory);
        }
        finally { session.Gate.Release(); }
    }

    public async Task<SalesResponse> ContinueAsync(string userKey, string prompt, IHttpClientFactory clientFactory)
    {
        var session = GetOrCreateSession(userKey);
        await session.Gate.WaitAsync();
        try
        {
            EnsureCurrentSession(userKey, session);
            if (session.Pending.Count != 0)
                throw new InvalidOperationException("Approve or reject the pending actions before sending another message.");
            return await ChatAsync(session, prompt, clientFactory);
        }
        finally { session.Gate.Release(); }
    }

    public async Task<SalesResponse> RespondToApprovalAsync(
        string userKey, string approvalId, bool approved, IHttpClientFactory clientFactory)
    {
        SalesSession? session;
        lock (CacheLock) _sessionCache.TryGetValue(GetSessionCacheKey(userKey), out session);
        if (session is null)
            throw new InvalidOperationException("The conversation expired. Start a new conversation.");

        await session.Gate.WaitAsync();
        try
        {
            EnsureCurrentSession(userKey, session);
            if (!session.Pending.Remove(approvalId, out var request))
                throw new InvalidOperationException("This approval is no longer pending.");
            session.ApprovalResponses.Add(request.CreateResponse(approved, approved ? null : "User declined"));
            if (session.Pending.Count > 0)
                return BuildPendingResponse(session);

            session.History.Add(new ChatMessage(ChatRole.User, session.ApprovalResponses.ToList()));
            session.ApprovalResponses.Clear();
            try
            {
                return await ChatAsync(session, null, clientFactory);
            }
            catch (MicrosoftIdentityWebChallengeUserException)
            {
                lock (CacheLock) _sessionCache.Remove(GetSessionCacheKey(userKey));
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to complete approved sales action; its outcome may be unknown.");
                lock (CacheLock) _sessionCache.Remove(GetSessionCacheKey(userKey));
                throw new InvalidOperationException(
                    "The approved action could not be completed reliably. It may have succeeded. Check Follow-up notes before retrying to avoid duplicates; see server logs for the error.", ex);
            }
        }
        finally { session.Gate.Release(); }
    }

    public async Task<SalesNotesView> GetNotesAsync(string? customerId, IHttpClientFactory clientFactory)
    {
        var transport = await CreateTransportAsync(clientFactory);
        await using var client = await McpClient.CreateAsync(transport);
        var tools = await client.ListToolsAsync();
        return await LoadNotesAsync(customerId, async (name, arguments) =>
            await client.CallToolAsync(name, arguments), tools.Select(t => t.Name));
    }

    internal static async Task<SalesNotesView> LoadNotesAsync(
        string? customerId,
        Func<string, IReadOnlyDictionary<string, object?>?, Task<CallToolResult>> callTool,
        IEnumerable<string> availableTools)
    {
        var toolNames = availableTools.ToHashSet(StringComparer.Ordinal);
        if (!toolNames.Contains("get_all_customers") || !toolNames.Contains("get_customer_follow_up_notes"))
            throw new InvalidOperationException(
                "The connected MCP server does not provide the sales notes tools. Start the updated HttpMcpServer from this worktree and check SalesMcpServerUrl. Restarting a server clears its in-memory demo data.");
        var customerResult = await callTool("get_all_customers", null);
        var customers = ReadToolJson<List<SalesCustomerView>>(customerResult, "get_all_customers");
        if (string.IsNullOrEmpty(customerId))
            return new SalesNotesView(customers, null, []);
        if (!customers.Any(c => c.Id == customerId))
            throw new InvalidOperationException("The selected customer was not found.");

        var result = await callTool("get_customer_follow_up_notes",
            new Dictionary<string, object?> { ["customerId"] = customerId });
        var notes = ReadToolJson<List<SalesNoteView>>(result, "get_customer_follow_up_notes");
        return new SalesNotesView(customers, customerId, notes.OrderByDescending(n => n.CreatedAtUtc).ToList());
    }

    private static T ReadToolJson<T>(CallToolResult result, string toolName)
    {
        var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
        if (result.IsError == true)
            throw new InvalidOperationException($"Could not load notes data ({toolName}): {text}");
        try
        {
            return JsonSerializer.Deserialize<T>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new JsonException("The server returned null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"The server returned an invalid response for {toolName}.", ex);
        }
    }

    public async Task ClearAsync(string userKey)
    {
        SalesSession? session;
        lock (CacheLock) _sessionCache.TryGetValue(GetSessionCacheKey(userKey), out session);
        if (session is null) return;
        await session.Gate.WaitAsync();
        try
        {
            lock (CacheLock)
            {
                if (_sessionCache.TryGetValue(GetSessionCacheKey(userKey), out SalesSession? current) &&
                    ReferenceEquals(current, session))
                    _sessionCache.Remove(GetSessionCacheKey(userKey));
            }
        }
        finally { session.Gate.Release(); }
    }

    public async Task<SalesResponse?> GetCurrentAsync(string userKey)
    {
        SalesSession? session;
        lock (CacheLock) _sessionCache.TryGetValue(GetSessionCacheKey(userKey), out session);
        if (session is null) return null;
        await session.Gate.WaitAsync();
        try
        {
            lock (CacheLock)
            {
                if (!_sessionCache.TryGetValue(GetSessionCacheKey(userKey), out SalesSession? current) ||
                    !ReferenceEquals(current, session)) return null;
            }
            return session.LastResponse is null ? null : BuildPendingResponse(session);
        }
        finally { session.Gate.Release(); }
    }

    private SalesSession GetOrCreateSession(string userKey)
    {
        lock (CacheLock)
        {
            var key = GetSessionCacheKey(userKey);
            if (!_sessionCache.TryGetValue(key, out SalesSession? session) || session is null)
            {
                session = new SalesSession();
                _sessionCache.Set(key, session, SessionCacheOptions);
            }
            return session;
        }
    }

    private void EnsureCurrentSession(string userKey, SalesSession session)
    {
        lock (CacheLock)
        {
            if (!_sessionCache.TryGetValue(GetSessionCacheKey(userKey), out SalesSession? current) ||
                !ReferenceEquals(current, session))
                throw new InvalidOperationException("The conversation expired or was cleared. Start a new conversation.");
        }
    }

    // -------------------------------------------------------------------------

    private async Task<SalesResponse> ChatAsync(SalesSession session, string? prompt, IHttpClientFactory clientFactory)
    {
        if (prompt is not null)
        {
            session.History.Add(new ChatMessage(ChatRole.User, prompt));
            TrimHistory(session);
        }

        var clients = await _createClients(clientFactory);
        await using var connection = clients.Connection;
        List<SalesCustomerView> customers = [];
        List<SalesOrderView> orders = [];

        var tools = clients.Tools;
        if (tools.Count == 0)
            throw new InvalidOperationException(
                "No MCP tools were loaded from the server. Ensure the MCP server is running and reachable.");

        var wrappedClient = new ChatClientBuilder(clients.ChatClient).UseFunctionInvocation().Build();
        var allowedTools = tools.Where(t => ReadTools.Contains(t.Name) || WriteTools.Contains(t.Name))
            .Select(t => WriteTools.Contains(t.Name) ? (AITool)new ApprovalRequiredAIFunction(t) : t)
            .ToList();
        if (allowedTools.Count == 0)
            throw new InvalidOperationException("No supported sales tools were loaded from the MCP server.");
        var chatOptions = ChatClientHelper.CreateChatOptions(allowedTools);

        var messagesWithSystem = new List<ChatMessage> { new(ChatRole.System, SystemPrompt) };
        messagesWithSystem.AddRange(session.History);

        var response = await wrappedClient.GetResponseAsync(messagesWithSystem, chatOptions);
        var finalAnswer = GetMessageText(response.Messages.LastOrDefault(m => m.Role == ChatRole.Assistant));
        var toolCalls = ExtractToolCalls(response.Messages);

        foreach (var msg in response.Messages)
        {
            if (msg.Role == ChatRole.Assistant || msg.Role == ChatRole.Tool)
                session.History.Add(msg);
        }
        foreach (var approval in response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>())
            session.Pending.Add(Guid.NewGuid().ToString("N"), approval);
        if (session.Pending.Count == 0) TrimHistory(session);

        (customers, orders) = ExtractContextFromToolResults(response.Messages);
        var chatHistory = BuildChatHistory(session.History);
        session.LastResponse = new SalesResponse(finalAnswer, chatHistory, customers, orders, toolCalls, GetPendingApprovals(session));
        return session.LastResponse;
    }

    private static SalesResponse BuildPendingResponse(SalesSession session) =>
        (session.LastResponse ?? throw new InvalidOperationException("No conversation response is available."))
        with { PendingApprovals = GetPendingApprovals(session) };

    private static List<SalesApproval> GetPendingApprovals(SalesSession session) =>
        session.Pending.Select(p =>
        {
            var call = p.Value.ToolCall as FunctionCallContent
                ?? throw new InvalidOperationException("Approval request does not contain a function call.");
            return new SalesApproval(p.Key, call.Name, JsonSerializer.Serialize(call.Arguments));
        }).ToList();

    private async Task<ClientSetup> CreateClientsAsync(IHttpClientFactory clientFactory)
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables()
            .Build();

        var chatClient = ChatClientHelper.GetChatClient(config);
        var transport = await CreateTransportAsync(clientFactory);
        var mcpClient = await McpClient.CreateAsync(transport);
        try
        {
            return new ClientSetup(chatClient, await mcpClient.GetMcpToolsAsAIFunctionsAsync(), mcpClient);
        }
        catch
        {
            await mcpClient.DisposeAsync();
            throw;
        }
    }

    private async Task<IClientTransport> CreateTransportAsync(IHttpClientFactory clientFactory)
    {
        var httpClient = clientFactory.CreateClient();

        var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync([_configuration["McpSalesScope"]!]);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var serverUrl = _configuration["SalesMcpServerUrl"]
            ?? _configuration["HttpMcpServerUrl"]
            ?? throw new InvalidOperationException("SalesMcpServerUrl / HttpMcpServerUrl is not configured.");

        return new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(serverUrl),
            Name = "Secure Sales Client",
            TransportMode = HttpTransportMode.StreamableHttp,
        }, httpClient, NullLoggerFactory.Instance, ownsHttpClient: false);
    }

    /// <summary>
    /// Builds context data from the tool results actually returned in this turn's response messages.
    /// Only shows the data the LLM actually fetched — delayed orders show only delayed, etc.
    /// </summary>
    internal static (List<SalesCustomerView>, List<SalesOrderView>) ExtractContextFromToolResults(
        IEnumerable<ChatMessage> messages)
    {
        // Map callId → (toolName, jsonResult)
        var callNames = new Dictionary<string, string>();
        var callResults = new Dictionary<string, string>();

        foreach (var msg in messages)
        {
            foreach (var content in msg.Contents)
            {
                if (content is FunctionCallContent call)
                    callNames[call.CallId] = call.Name;
                else if (content is FunctionResultContent result)
                    callResults[result.CallId] = result.Result?.ToString() ?? string.Empty;
            }
        }

        List<SalesCustomerView> customers = [];
        List<SalesOrderView> orders = [];

        foreach (var (callId, toolName) in callNames)
        {
            if (!callResults.TryGetValue(callId, out var json) || string.IsNullOrWhiteSpace(json))
                continue;

            switch (toolName)
            {
                case "get_all_customers":
                case "search_customers":
                case "get_customer_overview":
                    var customerJson = toolName == "get_customer_overview" ? $"[{json}]" : json;
                    foreach (var customer in DeserializeCustomers(customerJson))
                    {
                        var index = customers.FindIndex(c => c.Id == customer.Id);
                        if (index < 0) customers.Add(customer);
                        else customers[index] = customer;
                    }
                    break;

                case "get_all_orders":
                case "get_delayed_orders":
                case "get_customer_orders":
                case "search_orders":
                    var fetchedOrders = DeserializeOrders(json);
                    // merge into orders list (avoid duplicates when multiple order tools are called)
                    var existingIds = orders.Select(o => o.Id).ToHashSet();
                    orders.AddRange(fetchedOrders.Where(o => !existingIds.Contains(o.Id)));
                    break;
            }
        }

        foreach (var order in orders)
        {
            if (!customers.Any(c => c.Id == order.CustomerId))
                customers.Add(new SalesCustomerView(order.CustomerId, order.CustomerName, string.Empty, string.Empty, string.Empty));
        }
        return (customers, orders);
    }

    private static List<SalesCustomerView> DeserializeCustomers(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            var raw = JsonSerializer.Deserialize<List<JsonElement>>(json);
            if (raw == null) return [];
            return raw.Select(e => new SalesCustomerView(
                GetString(e, "id"),
                GetString(e, "name"),
                GetString(e, "industry"),
                GetString(e, "tier"),
                GetString(e, "accountManager")
            )).ToList();
        }
        catch { return []; }
    }

    private static List<SalesOrderView> DeserializeOrders(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            var raw = JsonSerializer.Deserialize<List<JsonElement>>(json);
            if (raw == null) return [];
            return raw.Select(e => new SalesOrderView(
                GetString(e, "id"),
                GetString(e, "customerId"),
                GetString(e, "customerName"),
                GetString(e, "productName"),
                GetString(e, "status"),
                GetDateTime(e, "orderDate"),
                GetDateTime(e, "promisedDeliveryDate"),
                GetNullableDateTime(e, "actualDeliveryDate"),
                GetDecimal(e, "value")
            )).ToList();
        }
        catch { return []; }
    }

    private static string GetString(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) ? v.GetString() ?? string.Empty : string.Empty;

    private static DateTime GetDateTime(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.TryGetDateTime(out var dt) ? dt : default;

    private static DateTime? GetNullableDateTime(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind != JsonValueKind.Null && v.TryGetDateTime(out var dt)
            ? dt : null;

    private static decimal GetDecimal(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.TryGetDecimal(out var d) ? d : 0m;

    private static List<McpToolCall> ExtractToolCalls(IEnumerable<ChatMessage> messages)
    {
        var calls = new Dictionary<string, (string Name, string? Args)>();
        var results = new Dictionary<string, bool>();

        foreach (var msg in messages)
        {
            foreach (var content in msg.Contents)
            {
                if (content is FunctionCallContent call)
                {
                    string? args = null;
                    if (call.Arguments is { Count: > 0 })
                    {
                        try { args = JsonSerializer.Serialize(call.Arguments); }
                        catch { /* ignore serialization errors */ }
                    }
                    calls[call.CallId] = (call.Name, args);
                }
                else if (content is FunctionResultContent result)
                {
                    results[result.CallId] = result.Exception is null &&
                        (result.Result is not CallToolResult toolResult || toolResult.IsError != true);
                }
            }
        }

        return calls.Select(kv => new McpToolCall(
            kv.Value.Name,
            kv.Value.Args,
            results.TryGetValue(kv.Key, out var succeeded) ? succeeded : null
        )).ToList();
    }

    private static List<SalesChatMessage> BuildChatHistory(List<ChatMessage> history) =>
        history
            .Where(m => m.Role == ChatRole.User || m.Role == ChatRole.Assistant)
            .Select(m => new
            {
                Role = m.Role == ChatRole.User ? "user" : "assistant",
                Content = GetMessageText(m)
            })
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => new SalesChatMessage(m.Role, m.Content!))
            .ToList();

    private static string? GetMessageText(ChatMessage? message)
    {
        if (message == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            return message.Text;
        }

        var textParts = message.Contents
            .OfType<TextContent>()
            .Select(c => c.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        return textParts.Count == 0 ? null : string.Join(Environment.NewLine, textParts);
    }

    private static string GetSessionCacheKey(string userKey) => $"sales-session:{userKey}";

    private static void TrimHistory(SalesSession session)
    {
        if (session.History.Count <= MaxHistoryMessages)
        {
            return;
        }

        var removeCount = session.History.Count - MaxHistoryMessages;
        session.History.RemoveRange(0, removeCount);
    }

    private class SalesSession
    {
        public List<ChatMessage> History { get; } = [];
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public Dictionary<string, ToolApprovalRequestContent> Pending { get; } = [];
        public List<AIContent> ApprovalResponses { get; } = [];
        public SalesResponse? LastResponse { get; set; }
    }
}
