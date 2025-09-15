namespace McpWebClient.AiServices.Models;

public record PendingFunctionCall(string Id, string FunctionName, string PluginName, string ArgumentsJson);
