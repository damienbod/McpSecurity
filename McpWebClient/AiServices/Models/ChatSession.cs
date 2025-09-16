using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace McpWebClient;

internal partial class PromptingSerivce
{
    public class ChatSession
    {
        public ChatHistory History { get; } = new();
        public Dictionary<string, FunctionCallContent> PendingCalls { get; } = new();
        public string? FinalAnswer { get; set; }
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}
