namespace McpWebClient.AiServices.Models;

public record PromptResponse(string? FinalAnswer, List<PendingFunctionCall> PendingFunctions);
