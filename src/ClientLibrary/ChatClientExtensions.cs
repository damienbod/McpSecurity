using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace ClientLibrary;

/// <summary>
/// Extension methods for integrating MCP tools with IChatClient
/// </summary>
public static class ChatClientExtensions
{
    /// <summary>
    /// Retrieves MCP tools and returns them as AIFunction instances for use with IChatClient
    /// </summary>
    public static async Task<IList<AIFunction>> GetMcpToolsAsAIFunctionsAsync(this IMcpClient mcpClient)
    {
        // Retrieve the list of tools available on the MCP server
        var tools = await mcpClient.ListToolsAsync().ConfigureAwait(false);

        // McpClientTool inherits from AIFunction, so we can cast directly
        return tools.Cast<AIFunction>().ToList();
    }

    /// <summary>
    /// Wraps an IChatClient with function invocation middleware and MCP tools
    /// </summary>
    public static IChatClient WithMcpTools(this IChatClient chatClient, IEnumerable<AIFunction> tools)
    {
        return new ChatClientBuilder(chatClient)
            .UseFunctionInvocation()
            .Build();
    }
}
