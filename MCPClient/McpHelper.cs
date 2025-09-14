using ModelContextProtocol.Client;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Web;

namespace MCPClient;

public class McpHelper
{
    public static IClientTransport CreateMcpTransport(HttpClient httpClient)
    {
        var serverUrl = "https://localhost:7133/mcp";
        //var serverUrl = "https://mcpoauthsecurity-hag0drckepathyb6.westeurope-01.azurewebsites.net/mcp";
        var transport = new SseClientTransport(new()
        {
            Endpoint = new Uri(serverUrl),
            Name = "MCP Desktop Client",
            //OAuth = new()
            //{
            //    RedirectUri = new Uri("http://localhost:1179/callback"),
            //    AuthorizationRedirectDelegate = HandleAuthorizationUrlAsync,
            //    ClientName = "HttpMcpDesktopClient",
            //    ClientId = "eff6bb0e-9871-458f-92ea-923c02250a05",
            //    Scopes = ["api://96b0f495-3b65-4c8f-a0c6-c3767c3365ed/mcp:tools"],
            //}
        }, httpClient);

        return transport;
    }
}
