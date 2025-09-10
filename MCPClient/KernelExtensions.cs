using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;

namespace MCPClient;

public static class KernelExtensions
{
    public static async Task ImportMcpClientFunctionsAsync(this Kernel kernel, IMcpClient mcpClient)
    {
        // Retrieve the list of tools available on the MCP server
        var tools = await mcpClient.ListToolsAsync().ConfigureAwait(false);
        Console.WriteLine($"Available MCP Tools:");

        foreach (var tool in tools)
        {
            Console.WriteLine($"{tool.Name}: {tool.Description}");
        }

        kernel.Plugins.AddFromFunctions("Tools", tools.Select(aiFunction => aiFunction.AsKernelFunction()));
        Console.WriteLine();
    }
}
