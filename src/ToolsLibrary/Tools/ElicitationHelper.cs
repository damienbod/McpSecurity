using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static ModelContextProtocol.Protocol.ElicitRequestParams;

namespace ToolsLibrary.Tools;

public static class ElicitationHelper
{
    private const string ApprovalKey = "approval";

    // Multi round-trip request (MRTR, spec 2026-07-28): the first call throws InputRequiredException,
    // the client asks the user and retries the same tool call with the answer in InputResponses.
    public static T InvokeEliciation<T>(
        McpServer mcpServer,
        RequestContext<CallToolRequestParams> context,
        string toolName,
        Func<T> toolExecution)
    {
        var inputResponses = context.Params?.InputResponses;
        if (inputResponses != null && inputResponses.TryGetValue(ApprovalKey, out var response))
        {
            var userAccepted = response.Deserialize(InputResponse.ElicitResultJsonTypeInfo);
            if (userAccepted?.Action != "accept" || userAccepted.Content?["answer"].ValueKind != System.Text.Json.JsonValueKind.True)
            {
                throw new McpException("User declined to proceed");
            }

            return toolExecution();
        }

        if (mcpServer.ClientCapabilities?.Elicitation == null || !mcpServer.IsMrtrSupported)
        {
            // for demo purpose, we allow direct execution if the client does not support elicitation
            return toolExecution();
        }

        throw new InputRequiredException(
            inputRequests: new Dictionary<string, InputRequest>
            {
                [ApprovalKey] = InputRequest.ForElicitation(GetApprovalParams(toolName))
            },
            requestState: ApprovalKey);
    }

    public static ElicitRequestParams GetApprovalParams(string name) => new()
    {
        Message = $"Do you want to execute tool '{name}'",
        RequestedSchema = new RequestSchema()
        {
            Properties =
            {
                ["answer"] = new BooleanSchema()
            },
        },
    };
}
