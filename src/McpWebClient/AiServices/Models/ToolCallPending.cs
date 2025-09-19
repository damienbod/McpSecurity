using ModelContextProtocol.Protocol;

namespace McpWebClient;

public class ToolCallPending
{
    public TaskCompletionSource<bool> Tcs { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ElicitRequestParams Request { get; }
    public DateTime CreatedUtc { get; } = DateTime.UtcNow;
    public ToolCallPending(ElicitRequestParams req) { Request = req; }
}

