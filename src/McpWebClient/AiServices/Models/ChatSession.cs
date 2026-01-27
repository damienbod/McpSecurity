using Microsoft.Extensions.AI;

namespace McpWebClient;

internal partial class PromptingService
{
    public class ChatSession
    {
        public List<ChatMessage> History { get; } = [];
        public Dictionary<string, FunctionCallContent> PendingCalls { get; } = new();
        public string? FinalAnswer { get; set; }
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}
