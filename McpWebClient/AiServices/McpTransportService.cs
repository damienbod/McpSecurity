using McpWebClient.AiServices;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;

namespace McpWebClient;

public class McpTransportService
{
    private readonly IDistributedCache _cache;
    private readonly IConfigurationRoot _configuration;

    public McpTransportService(IDistributedCache cache, IConfigurationRoot configuration)
    {
        _cache = cache;
        _configuration = configuration;
    }

    public async Task InitialCacheIfNotExistingAsync(IHttpClientFactory clientFactory)
    {
        // Prepare and build kernel
        var kernel = SemanticKernelHelper.GetKernel(_configuration);

        // initialize MCP client
        var transport = CreateMcpTransport(clientFactory);
        await using IMcpClient mcpClient = await McpClientFactory.CreateAsync(transport);

        // Retrieve the list of tools available on the MCP server and import them to the kernel
        await kernel.ImportMcpClientFunctionsAsync(mcpClient);
    }

    private static IClientTransport CreateMcpTransport(IHttpClientFactory clientFactory)
    {
        var httpClient = clientFactory.CreateClient();
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
