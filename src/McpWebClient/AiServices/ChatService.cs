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
    Manual,
    Elicitation
}

public class ChatService
{
    private readonly IConfiguration _configuration;
    private readonly ElicitationCoordinator _elicitationCoordinator;
    private IChatClient _chatClient;
    private IList<AIFunction> _mcpTools = [];
    private IMcpClient _mcpClient = null!;
    private bool _initialized;
    private ApprovalMode _mode = ApprovalMode.Manual;
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

    public void SetMode(ApprovalMode mode)
    {
        if (_mode != mode)
        {
            _initialized = false;
            _mode = mode;
        }
    }

    public async Task EnsureSetupAsync(IHttpClientFactory clientFactory)
    {
        if (_initialized) return;

        var accessToken = await _tokenAcquisition
            .GetAccessTokenForUserAsync([_configuration["McpScope"]!]);

        _mcpClient = await McpClientFactory.CreateAsync(CreateMcpTransport(clientFactory, accessToken), GetMcpOptions());
        _mcpTools = await _mcpClient.GetMcpToolsAsAIFunctionsAsync();

        // Wrap chat client with function invocation if using elicitation (auto-invoke)
        if (_mode == ApprovalMode.Elicitation)
        {
            _chatClient = new ChatClientBuilder(_chatClient).UseFunctionInvocation().Build();
        }

        _promptingService = new PromptingService(_chatClient, _mcpTools);
        _initialized = true;
    }

    private McpClientOptions? GetMcpOptions()
    {
        return _mode == ApprovalMode.Elicitation ? new McpClientOptions
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

    private IClientTransport CreateMcpTransport(IHttpClientFactory clientFactory, string accessToken)
    {
        var httpClient = clientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var httpMcpServerUrl = _configuration["HttpMcpServerUrl"] ?? throw new ArgumentNullException("Configuration missing for HttpMcpServerUrl");
        return new SseClientTransport(new() { Endpoint = new Uri(httpMcpServerUrl), Name = "Secure Client" }, httpClient);
    }

    private PromptingService Handler => _promptingService ?? throw new InvalidOperationException("Service not initialized");

    public Task<PromptResponse> BeginChatAsync(string userKey, string prompt) => Handler.BeginAsync(userKey, prompt);
    public Task<PromptResponse> ApproveFunctionAsync(string userKey, string functionId) => Handler.ApproveAsync(userKey, functionId);
    public Task<PromptResponse> DeclineFunctionAsync(string userKey, string functionId) => Handler.DeclineAsync(userKey, functionId);
}
