using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using ModelContextProtocol.Client;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Web;
using static System.Net.Mime.MediaTypeNames;

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
            //OAuth = new()
            //{
            //    RedirectUri = new Uri("http://localhost"),
            //    AuthorizationRedirectDelegate = HandleAuthorizationUrlAsync,
            //    ClientName = "HttpMcpDesktopClient",
            //    ClientId = "eff6bb0e-9871-458f-92ea-923c02250a05",
            //    Scopes = ["api://96b0f495-3b65-4c8f-a0c6-c3767c3365ed/mcp:tools"],
            //}
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
}
