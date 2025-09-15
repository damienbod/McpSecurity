using McpWebClient.Hubs;
using Microsoft.AspNetCore.SignalR;
using ModelContextProtocol.Protocol;
using System.Collections.Concurrent;
using System.Text.Json;

namespace McpWebClient.AiServices.Elicitation;

/// <summary>
/// Coordinates elicitation requests coming from the MCP protocol with user approvals delivered via SignalR hub.
/// </summary>
public partial class ElicitationCoordinator
{
    private readonly TimeSpan _timeout = TimeSpan.FromMinutes(2);
    private readonly IHubContext<ElicitationHub>? _hubContext;

    public ElicitationCoordinator(IHubContext<ElicitationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    private readonly ConcurrentDictionary<string, ToolCallPending> _pending = new();

    public IEnumerable<(string Id, string? Message)> GetAll() => _pending.Select(kv => (kv.Key, kv.Value.Request.Message));

    public ValueTask<ElicitResult> HandleAsync(ElicitRequestParams? requestParams, CancellationToken token)
    {
        if (requestParams is null)
        {
            return ValueTask.FromResult(new ElicitResult());
        }

        var id = Guid.NewGuid().ToString("N");
        var entry = new ToolCallPending(requestParams);
        _pending[id] = entry;

        _ = _hubContext?.Clients.All.SendAsync("ElicitationPending", new { id, message = requestParams.Message });

        _ = Task.Run(async () =>
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(_timeout);
                await Task.WhenAny(entry.Tcs.Task, Task.Delay(Timeout.Infinite, cts.Token));
                if (!entry.Tcs.Task.IsCompleted)
                {
                    entry.Tcs.TrySetResult(false);
                }
            }
            catch
            {
                entry.Tcs.TrySetResult(false);
            }
        });

        return AwaitDecisionAsync(id, entry);
    }

    private async ValueTask<ElicitResult> AwaitDecisionAsync(string id, ToolCallPending pending)
    {
        var approved = await pending.Tcs.Task.ConfigureAwait(false);
        _pending.TryRemove(id, out _);
        var content = new Dictionary<string, JsonElement>();
        content["answer"] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(approved));
        _ = _hubContext?.Clients.All.SendAsync("ElicitationCompleted", new { id, approved });
        return new ElicitResult
        {
            Action = "accept",
            Content = content
        };
    }

    public bool Approve(string id)
    {
        if (_pending.TryGetValue(id, out var p))
        {
            return p.Tcs.TrySetResult(true);
        }
        return false;
    }

    public bool Decline(string id)
    {
        if (_pending.TryGetValue(id, out var p))
        {
            return p.Tcs.TrySetResult(false);
        }
        return false;
    }
}
