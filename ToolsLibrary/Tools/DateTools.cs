using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ToolsLibrary.Tools;

[McpServerToolType]
public class DateTools
{
    [McpServerTool]
    [Description("Returns the current date and time in ISO 8601 format.")]
    public Task<string> GetCurrentDateTime(IMcpServer mcpServer)
        => ElicitationHelper.InvokeEliciation(
            mcpServer,
            nameof(GetCurrentDateTime),
            () => DateTime.UtcNow.ToString("o"));
}
