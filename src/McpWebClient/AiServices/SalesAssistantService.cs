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
        "Your available tools are: GetAllCustomers, GetAllOrders, GetCustomerOrders, GetDelayedOrders. " +
        "IMPORTANT: Always call a tool to retrieve data before answering any question about customers or orders. " +
        "NEVER ask the user to upload files, connect systems, or provide data — the data is already available via tools. " +
        "When assessing customer risk, consider: customer tier (A = most critical), number of delayed orders, " +
        "delay duration in days, and order value. Be concise and factual in your responses.";

    private readonly IConfiguration _configuration;
    private readonly ITokenAcquisition _tokenAcquisition;

    private static readonly ConcurrentDictionary<string, SalesSession> _sessions = new();

    public SalesAssistantService(IConfiguration configuration, ITokenAcquisition tokenAcquisition)
    {
        _configuration = configuration;
        _tokenAcquisition = tokenAcquisition;
    }

    public async Task<SalesResponse> BeginAsync(string userKey, string prompt, FunctionCallingMode mode, IHttpClientFactory clientFactory)
    {
        var session = new SalesSession();
        _sessions[userKey] = session;
        return await ChatAsync(session, prompt, mode, clientFactory);
    }

    public async Task<SalesResponse> ContinueAsync(string userKey, string prompt, FunctionCallingMode mode, IHttpClientFactory clientFactory)
    {
        if (!_sessions.TryGetValue(userKey, out var session))
        {
            session = new SalesSession();
            _sessions[userKey] = session;
        }
        return await ChatAsync(session, prompt, mode, clientFactory);
    }

    public void Clear(string userKey) => _sessions.TryRemove(userKey, out _);

    // -------------------------------------------------------------------------

    private async Task<SalesResponse> ChatAsync(SalesSession session, string prompt, FunctionCallingMode mode, IHttpClientFactory clientFactory)
    {
        if (mode == FunctionCallingMode.Local)
            throw new InvalidOperationException("Sales Assistant requires an MCP server connection (Unsecure or Secure mode).");

        session.History.Add(new ChatMessage(ChatRole.User, prompt));

        var (chatClient, mcpClient) = await CreateClientsAsync(mode, clientFactory);
        List<SalesCustomerView> customers = [];
        List<SalesOrderView> orders = [];

        try
        {
            var tools = await mcpClient.GetMcpToolsAsAIFunctionsAsync();

            if (tools.Count == 0)
                throw new InvalidOperationException(
                    "No MCP tools were loaded from the server. Ensure the MCP server is running and reachable.");

            var wrappedClient = new ChatClientBuilder(chatClient).UseFunctionInvocation().Build();

            var chatOptions = ChatClientHelper.CreateChatOptions(tools.Cast<AITool>());

            // Always prepend the system message so it's present every turn
            var messagesWithSystem = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt)
            };
            messagesWithSystem.AddRange(session.History);

            var response = await wrappedClient.GetResponseAsync(messagesWithSystem, chatOptions);
            var finalAnswer = response.Messages.LastOrDefault(m => m.Role == ChatRole.Assistant)?.Text;

            // Append assistant messages to session history
            foreach (var msg in response.Messages)
            {
                if (msg.Role == ChatRole.Assistant || msg.Role == ChatRole.Tool)
                    session.History.Add(msg);
            }

            // Refresh context data after the AI turn
            (customers, orders) = await RefreshContextAsync(mcpClient);

            var chatHistory = BuildChatHistory(session.History);
            return new SalesResponse(finalAnswer, chatHistory, customers, orders);
        }
        finally
        {
            await mcpClient.DisposeAsync();
        }
    }

    private async Task<(IChatClient, McpClient)> CreateClientsAsync(FunctionCallingMode mode, IHttpClientFactory clientFactory)
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables()
            .Build();

        var chatClient = ChatClientHelper.GetChatClient(config);
        var transport = await CreateTransportAsync(mode, clientFactory);
        var mcpClient = await McpClient.CreateAsync(transport);
        return (chatClient, mcpClient);
    }

    private async Task<IClientTransport> CreateTransportAsync(FunctionCallingMode mode, IHttpClientFactory clientFactory)
    {
        var httpClient = clientFactory.CreateClient();

        if (mode == FunctionCallingMode.McpSecure)
        {
            var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync([_configuration["McpScope"]!]);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        var serverUrl = _configuration["HttpMcpServerUrl"]
            ?? throw new InvalidOperationException("HttpMcpServerUrl is not configured.");

        return new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(serverUrl),
            Name = mode == FunctionCallingMode.McpSecure ? "Secure Sales Client" : "Unsecure Sales Client",
            TransportMode = HttpTransportMode.StreamableHttp,
        }, httpClient, NullLoggerFactory.Instance, ownsHttpClient: false);
    }

    private static async Task<(List<SalesCustomerView>, List<SalesOrderView>)> RefreshContextAsync(McpClient mcpClient)
    {
        var tools = await mcpClient.GetMcpToolsAsAIFunctionsAsync();

        var customerTool = tools.FirstOrDefault(t => t.Name == "GetAllCustomers");
        var orderTool = tools.FirstOrDefault(t => t.Name == "GetAllOrders");

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
