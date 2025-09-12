using ModelContextProtocol.Client;

namespace MCPClient;

public class McpHelper
{
    public static IClientTransport CreateMcpTransport(HttpClient httpClient)
    {
        //var serverUrl = "https://localhost:7133/mcp";
        var serverUrl = "https://mcpoauthsecurity-hag0drckepathyb6.westeurope-01.azurewebsites.net/mcp";
        var transport = new SseClientTransport(new()
        {
            Endpoint = new Uri(serverUrl),
            Name = "Secure Weather Client",
            OAuth = new()
            {
                ClientName = "ProtectedMcpClient",
                RedirectUri = new Uri("http://localhost:1179/callback"), 

            }
        }, httpClient);

        return transport;

    }
}
