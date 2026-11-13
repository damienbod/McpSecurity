using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ToolsLibrary.Tools;

/// <summary>
/// Sample MCP tools for demonstration purposes.
/// These tools can be invoked by MCP clients to perform various operations.
/// </summary>
[McpServerToolType]
public class RandomNumberTools
{
    [McpServerTool]
    [Description("Generates a random number based on a date.")]
    public int GetRandomNumberFromDateTime(
        McpServer mcpServer,
        RequestContext<CallToolRequestParams> context,
        [Description("The date to generate random number from")] DateTime? datetime = null) =>
        ElicitationHelper.InvokeEliciation(
            mcpServer,
            context,
            nameof(GetRandomNumberFromDateTime),
            () =>
            {
                if (datetime == null)
                {
                    datetime = DateTime.Now;
                }

                var min = (int)datetime.Value.Ticks % 100;
                var max = min + 1_000;

                return Random.Shared.Next(min, max);
            });

    [McpServerTool]
    [Description("Generates a random number between the specified minimum and maximum values.")]
    public int GetRandomNumber(
    McpServer mcpServer,
    RequestContext<CallToolRequestParams> context,
   [Description("Minimum value (inclusive)")] int min = 0,
   [Description("Maximum value (exclusive)")] int max = 100)
   => ElicitationHelper.InvokeEliciation(mcpServer, context, nameof(GetRandomNumber), () => Random.Shared.Next(min, max));

}
