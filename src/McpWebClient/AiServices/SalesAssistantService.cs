using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using ClientLibrary;
using McpWebClient.AiServices.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Identity.Web;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace McpWebClient.AiServices;

public class SalesAssistantService
{
    private const string SystemPrompt =
        "You are a Sales Assistant with direct access to sales data through built-in tools. " +
        "Available tools: get_all_customers, get_all_orders, get_customer_orders, get_delayed_orders. " +
        "Use tools to retrieve data when needed, then answer concisely based on the results. " +
        "Do NOT ask the user to provide data or connect external systems — the data is already available. " +
        "Do NOT call the same tool more than once per response. " +
        "When assessing customer risk, consider: tier (A = most critical), delay count, delay duration, and order value.";

    private readonly IConfiguration _configuration;
    private readonly ITokenAcquisition _tokenAcquisition;

    private static readonly ConcurrentDictionary<string, SalesSession> _sessions = new();

    public SalesAssistantService(IConfiguration configuration, ITokenAcquisition tokenAcquisition)
    {
        _configuration = configuration;
        _tokenAcquisition = tokenAcquisition;
    }

    public async Task<SalesResponse> BeginAsync(string userKey, string prompt, IHttpClientFactory clientFactory)
    {
        var session = new SalesSession();
        _sessions[userKey] = session;
        return await ChatAsync(session, prompt, clientFactory);
    }

    public async Task<SalesResponse> ContinueAsync(string userKey, string prompt, IHttpClientFactory clientFactory)
    {
        if (!_sessions.TryGetValue(userKey, out var session))
        {
            session = new SalesSession();
            _sessions[userKey] = session;
        }
        return await ChatAsync(session, prompt, clientFactory);
    }

    public void Clear(string userKey) => _sessions.TryRemove(userKey, out _);

    // -------------------------------------------------------------------------

    private async Task<SalesResponse> ChatAsync(SalesSession session, string prompt, IHttpClientFactory clientFactory)
    {
        session.History.Add(new ChatMessage(ChatRole.User, prompt));

        var (chatClient, mcpClient) = await CreateClientsAsync(clientFactory);
        List<SalesCustomerView> customers = [];
        List<SalesOrderView> orders = [];

        try
        {
            var tools = await mcpClient.GetMcpToolsAsAIFunctionsAsync();

            if (tools.Count == 0)
                throw new InvalidOperationException(
                    "No MCP tools were loaded from the server. Ensure the MCP server is running and reachable.");

            var wrappedClient = new ChatClientBuilder(chatClient)
                .UseFunctionInvocation()
                .Build();

            var chatOptions = ChatClientHelper.CreateChatOptions(tools.Cast<AITool>());

            // Always prepend the system message so it's present every turn
            var messagesWithSystem = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt)
            };
            messagesWithSystem.AddRange(session.History);

            var response = await wrappedClient.GetResponseAsync(messagesWithSystem, chatOptions);
            var finalAnswer = response.Messages.LastOrDefault(m => m.Role == ChatRole.Assistant)?.Text;

            // Extract MCP tool calls made during this turn for display
            var toolCalls = ExtractToolCalls(response.Messages);

            // Append assistant/tool messages to session history for multi-turn context
            foreach (var msg in response.Messages)
            {
                if (msg.Role == ChatRole.Assistant || msg.Role == ChatRole.Tool)
                    session.History.Add(msg);
            }

            // Refresh context data after the AI turn (reuse already-fetched tools)
            (customers, orders) = await RefreshContextAsync(tools);

            var chatHistory = BuildChatHistory(session.History);
            return new SalesResponse(finalAnswer, chatHistory, customers, orders, toolCalls);
        }
        finally
        {
            await mcpClient.DisposeAsync();
        }
    }

    private async Task<(IChatClient, McpClient)> CreateClientsAsync(IHttpClientFactory clientFactory)
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables()
            .Build();

        var chatClient = ChatClientHelper.GetChatClient(config);
        var transport = await CreateTransportAsync(clientFactory);
        var mcpClient = await McpClient.CreateAsync(transport);
        return (chatClient, mcpClient);
    }

    private async Task<IClientTransport> CreateTransportAsync(IHttpClientFactory clientFactory)
    {
        var httpClient = clientFactory.CreateClient();

        var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync([_configuration["McpScope"]!]);
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

    private static async Task<(List<SalesCustomerView>, List<SalesOrderView>)> RefreshContextAsync(IList<AIFunction> tools)
    {
        var customerTool = tools.FirstOrDefault(t => t.Name == "get_all_customers");
        var orderTool = tools.FirstOrDefault(t => t.Name == "get_all_orders");

        List<SalesCustomerView> customers = [];
        List<SalesOrderView> orders = [];

        if (customerTool != null)
        {
            var result = await customerTool.InvokeAsync(null);
            customers = DeserializeCustomers(result?.ToString());
        }

        if (orderTool != null)
        {
            var result = await orderTool.InvokeAsync(null);
            orders = DeserializeOrders(result?.ToString());
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
        var results = new HashSet<string>();

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
                    results.Add(result.CallId);
                }
            }
        }

        return calls.Select(kv => new McpToolCall(
            kv.Value.Name,
            kv.Value.Args,
            results.Contains(kv.Key)
        )).ToList();
    }

    private static List<SalesChatMessage> BuildChatHistory(List<ChatMessage> history) =>
        history
            .Where(m => m.Role == ChatRole.User || m.Role == ChatRole.Assistant)
            .Where(m => !string.IsNullOrWhiteSpace(m.Text))
            .Select(m => new SalesChatMessage(m.Role == ChatRole.User ? "user" : "assistant", m.Text ?? string.Empty))
            .ToList();

    private class SalesSession
    {
        public List<ChatMessage> History { get; } = [];
    }
}
