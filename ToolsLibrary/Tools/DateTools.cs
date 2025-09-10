using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ToolsLibrary.Tools;

[McpServerToolType]
public class DateTools
{
    [McpServerTool]
    [Description("Returns the current date and time in ISO 8601 format.")]
    public string GetCurrentDateTime()
    {
        return DateTime.UtcNow.ToString("o");
    }
}
