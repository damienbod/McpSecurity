using McpWebClient.AiServices.Elicitation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace McpWebClient.Hubs;

[Authorize]
public class ElicitationHub : Hub
{
    private readonly ElicitationCoordinator _coordinator;
    public ElicitationHub(ElicitationCoordinator coordinator) => _coordinator = coordinator;

    public IEnumerable<object> Pending()
        => _coordinator.GetAll().Select(p => new { id = p.Id, message = p.Message });

    public bool Approve(string id) => _coordinator.Approve(id);
    public bool Decline(string id) => _coordinator.Decline(id);
}
