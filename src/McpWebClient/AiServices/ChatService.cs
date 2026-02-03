using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using ClientLibrary;
using McpWebClient.AiServices.Elicitation;
using McpWebClient.AiServices.Models;
using Microsoft.Extensions.AI;
using Microsoft.Identity.Web;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace McpWebClient;

public enum ApprovalMode
{
    [Display(Name = "Auto (no human approval)")]
    Auto,
    [Display(Name = "Manual Approval")]
    Manual,
    [Display(Name = "Elicitation Approval")]
    Elicitation
}

public enum FunctionCallingMode
{
    [Display(Name = "Local Function Calling")]
    Local,
    [Display(Name = "Unauthenticated MCP")]
    McpUnsecure,
    [Display(Name = "Confidential OIDC MCP")]
    McpSecure
}

public class ChatService
{
    private readonly IConfiguration _configuration;
    private readonly ElicitationCoordinator _elicitationCoordinator;
    private IChatClient _chatClient;
    private IList<AIFunction> _tools = [];
    private IMcpClient _mcpClient = null!;
    private bool _initialized;
    private ApprovalMode _approvalMode = ApprovalMode.Auto;
    private FunctionCallingMode _functionCallingMode = FunctionCallingMode.Local;
    private readonly ITokenAcquisition _tokenAcquisition;

    private PromptingService? _promptingService;

    public ChatService(IConfiguration configuration, ElicitationCoordinator elicitationCoordinator, ITokenAcquisition tokenAcquisition)
    {
        _configuration = configuration;
        _elicitationCoordinator = elicitationCoordinator;
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();
        _chatClient = ChatClientHelper.GetChatClient(config);
        _tokenAcquisition = tokenAcquisition;
    }

    public void SetApprovalMode(ApprovalMode mode)
    {
        if (_approvalMode != mode)
        {
            _initialized = false;
            _approvalMode = mode;
        }
    }

    public void SetFunctionCallingMode(FunctionCallingMode mode)
    {
        if (_functionCallingMode != mode)
        {
            _initialized = false;
            _functionCallingMode = mode;
        }
    }

    public async Task EnsureSetupAsync(IHttpClientFactory clientFactory)
    {
        if (_initialized) return;



        if (_functionCallingMode == FunctionCallingMode.Local)
        {
            _tools = GetLocalTools();
        }
        else
        {
            _mcpClient = await McpClientFactory.CreateAsync(await CreateMcpTransport(clientFactory), GetMcpOptions());
            _tools = await _mcpClient.GetMcpToolsAsAIFunctionsAsync();
        }

        // Wrap chat client with function invocation if using elicitation or auto mode (auto-invoke)
        if (_approvalMode is ApprovalMode.Elicitation or ApprovalMode.Auto)
        {
            _chatClient = new ChatClientBuilder(_chatClient).UseFunctionInvocation().Build();
        }

        _promptingService = new PromptingService(_chatClient, _tools);
        _initialized = true;
    }

    private McpClientOptions? GetMcpOptions()
    {
        return _approvalMode == ApprovalMode.Elicitation ? new McpClientOptions
        {
            ClientInfo = new() { Name = "WebElicitationClient", Version = "1.0.0" },
            Capabilities = new() { Elicitation = new() { ElicitationHandler = HandleElicitationAsync } }
        } : null;
    }

    // Inlined former WebElicitationHandler logic
    private ValueTask<ElicitResult> HandleElicitationAsync(ElicitRequestParams? requestParams, CancellationToken token)
    {
        return _elicitationCoordinator.HandleAsync(requestParams, token);
    }

    private async Task<IClientTransport> CreateMcpTransport(IHttpClientFactory clientFactory)
    {
        var clientName = "Unsecure Client";
        var httpClient = clientFactory.CreateClient();

        if (_functionCallingMode == FunctionCallingMode.McpSecure)
        {
            clientName = "Secure Client";
            var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync([_configuration["McpScope"]!]);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        var httpMcpServer = _configuration["HttpMcpServerUrl"] ?? throw new ArgumentNullException("Configuration missing for HttpMcpServerUrl");
        var transport = new SseClientTransport(new()
        {
            Endpoint = new Uri(httpMcpServer!),
            Name = clientName,
        }, httpClient);

        return transport;
    }

    private IList<AIFunction> GetLocalTools() => [
        AIFunctionFactory.Create(
             () => DateTime.UtcNow.ToString("o"),
             "GetCurrentDateTime",
             "Returns the current date and time in ISO 8601 format."),
        AIFunctionFactory.Create(
             ([Description("The date to generate random number from")] DateTime? datetime = null) => {
                if (datetime == null)
                {
                    datetime = DateTime.Now;
                }

                var min = (int)datetime.Value.Ticks % 100;
                var max = min + 1_000;

                return Random.Shared.Next(min, max);
            },
             "GetRandomNumberFromDateTime",
             "Generates a random number based on a date.")
    ];

    private PromptingService Handler => _promptingService ?? throw new InvalidOperationException("Service not initialized");

    public Task<PromptResponse> BeginChatAsync(string userKey, string prompt) => Handler.BeginAsync(userKey, prompt);
    public Task<PromptResponse> ApproveFunctionAsync(string userKey, string functionId) => Handler.ApproveAsync(userKey, functionId);
    public Task<PromptResponse> DeclineFunctionAsync(string userKey, string functionId) => Handler.DeclineAsync(userKey, functionId);
}
