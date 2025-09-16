using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static ModelContextProtocol.Protocol.ElicitRequestParams;

namespace ToolsLibrary.Tools;

public static class ElicitationHelper
{
    public static async Task<T> InvokeEliciation<T>(IMcpServer mcpServer, string toolName, Func<T> toolExecution)
    {
        if (mcpServer.ClientCapabilities?.Elicitation == null)
        {
            // for demo purpose, we allow direct execution if the client does not support elicitation
            return toolExecution();
        }

        var userAccepted = await mcpServer.ElicitAsync(GetElicitationParams(toolName));

        if (userAccepted.Action != "accept" || userAccepted.Content?["answer"].ValueKind != System.Text.Json.JsonValueKind.True)
        {
            throw new McpException("User declined to proceed");
        }

        return toolExecution();
    }

    public static RequestSchema GetElicitationSchema() => new RequestSchema()
    {
        Properties =
        {
            ["answer"] = new BooleanSchema()
        },
    };

    public static ElicitRequestParams GetElicitationParams(string name) => new()
    {
        Message = $"Do you want to execute tool '{name}'",
        RequestedSchema = GetElicitationSchema(),
    };
}
