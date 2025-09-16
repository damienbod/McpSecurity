using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MCPClient;

public class McpHelper
{
    private static PublicClientApplicationOptions? appConfiguration = null;

    // The MSAL Public client app
    private static IPublicClientApplication? application;

    public static async Task<IClientTransport> CreateMcpTransportAsync(HttpClient httpClient, IConfigurationRoot configuration)
    {
        appConfiguration = configuration.Get<PublicClientApplicationOptions>();
        string[] scopes = ["api://96b0f495-3b65-4c8f-a0c6-c3767c3365ed/mcp:tools"];

        // Sign-in user using MSAL and obtain an access token for MS Graph
        var accessToken = await SignInUserAndGetTokenUsingMSAL(appConfiguration!, scopes);

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var httpMcpServer = configuration["HttpMcpServerUrl"];
        var transport = new SseClientTransport(new()
        {
            Endpoint = new Uri(httpMcpServer!),
            Name = "MCP Desktop Client",
        }, httpClient);

        return transport;
    }

    private static async Task<string> SignInUserAndGetTokenUsingMSAL(PublicClientApplicationOptions configuration, string[] scopes)
    {
        string authority = string.Concat(configuration.Instance, configuration.TenantId);

        // Initialize the MSAL library by building a public client application
        application = PublicClientApplicationBuilder.Create(configuration.ClientId)
                        .WithAuthority(authority)
                        .WithDefaultRedirectUri()
                        .Build();

        AuthenticationResult result;
        try
        {
            var accounts = await application.GetAccountsAsync();
            result = await application.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
             .ExecuteAsync();
        }
        catch (MsalUiRequiredException ex)
        {
            result = await application.AcquireTokenInteractive(scopes)
             .WithClaims(ex.Claims)
             .ExecuteAsync();
        }

        return result.AccessToken;
    }

    public static McpClientOptions CreateMcpClientOptions()
       => new McpClientOptions()
       {
           ClientInfo = new()
           {
               Name = "ElicitationClient",
               Version = "1.0.0",
           },
           Capabilities = new()
           {
               Elicitation = new()
               {
                   ElicitationHandler = HandleElicitationAsync,
               },
           }
       };

    public static async ValueTask<ElicitResult> HandleElicitationAsync(ElicitRequestParams? requestParams, CancellationToken token)
    {
        // Bail out if the requestParams is null or if the requested schema has no properties
        if (requestParams?.RequestedSchema?.Properties == null)
        {
            return new ElicitResult();
        }

        // Process the elicitation request
        if (requestParams?.Message is not null)
        {
            Console.WriteLine(requestParams.Message);
        }

        Console.WriteLine($"Please allow/decline function execution with [y,n]");
        var key = Console.ReadKey();
        Console.WriteLine();

        var userAllowance = key.KeyChar == 'y' || key.KeyChar == 'Y';
        var content = new Dictionary<string, JsonElement>();
        content["answer"] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(userAllowance));

        // Return the user's input
        return new ElicitResult
        {
            Action = "accept",
            Content = content
        };
    }
}
