using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;

namespace ClientLibrary;

public static class KernelExtensions
{
    public static async Task ImportMcpClientToolsAsync(this Kernel kernel, IMcpClient mcpClient)
    {
        // Retrieve the list of tools available on the MCP server
        var tools = await mcpClient.ListToolsAsync().ConfigureAwait(false);

        // import the tools into the kernel
        kernel.Plugins.AddFromFunctions("Tools", tools.Select(aiFunction => aiFunction.AsKernelFunction()));
    }
}
