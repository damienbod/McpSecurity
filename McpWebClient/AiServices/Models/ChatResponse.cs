namespace McpWebClient.AiServices.Models;

public record ChatResponse(string? FinalAnswer, List<PendingFunctionCall> PendingFunctions);
