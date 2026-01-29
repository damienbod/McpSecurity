using System.Collections.Concurrent;
using System.Text.Json;
using ClientLibrary;
using McpWebClient.AiServices.Models;
using Microsoft.Extensions.AI;

namespace McpWebClient;

internal partial class PromptingService
{
    private readonly IChatClient _chatClient;
    private readonly IList<AIFunction> _mcpTools;

    private static readonly ConcurrentDictionary<string, ChatSession> _sessions = new();

    public PromptingService(IChatClient chatClient, IList<AIFunction> mcpTools)
    {
        _chatClient = chatClient;
        _mcpTools = mcpTools;
    }

    public async Task<PromptResponse> BeginAsync(string userKey, string prompt)
    {
        var session = _sessions[userKey] = new() { LastUpdatedUtc = DateTime.UtcNow };
        session.History.Add(new ChatMessage(ChatRole.User, prompt));

        var response = await ExecutePrompt(session);
        var lastMessage = response.Messages.LastOrDefault();

        var functionCalls = lastMessage?.Contents
            .OfType<FunctionCallContent>()
            .ToArray() ?? [];

        return ExtractFunctionsAndSyncSession(session, lastMessage, functionCalls);
    }

    private static PromptResponse ExtractFunctionsAndSyncSession(ChatSession session, ChatMessage? lastMessage, FunctionCallContent[] functionCalls)
    {
        if (functionCalls.Length > 0 && lastMessage != null)
        {
            session.History.Add(lastMessage);
            foreach (var call in functionCalls)
            {
                session.PendingCalls[call.CallId] = call;
            }
            session.LastUpdatedUtc = DateTime.UtcNow;
            return new PromptResponse(null, Project(session));
        }

        session.FinalAnswer = lastMessage?.Text;
        session.LastUpdatedUtc = DateTime.UtcNow;
        return new PromptResponse(session.FinalAnswer, new());
    }

    private async Task<Microsoft.Extensions.AI.ChatResponse> ExecutePrompt(ChatSession session)
    {
        var chatOptions = ChatClientHelper.CreateChatOptions(_mcpTools.Cast<AITool>());
        var response = await _chatClient.GetResponseAsync(session.History, chatOptions);
        return response;
    }

    public async Task<PromptResponse> ApproveAsync(string userKey, string functionId)
    {
        if (!_sessions.TryGetValue(userKey, out var session))
        {
            return new PromptResponse("Session not found. Please start again.", new());
        }

        if (!session.PendingCalls.TryGetValue(functionId, out var functionCall))
        {
            return new PromptResponse(session.FinalAnswer, Project(session));
        }

        // Find and invoke the function
        var tool = _mcpTools.FirstOrDefault(t => t.Name == functionCall.Name);
        if (tool != null)
        {
            try
            {
                // Create AIFunctionArguments from the dictionary
                var aiArgs = functionCall.Arguments != null
                    ? new AIFunctionArguments(functionCall.Arguments)
                    : null;
                var result = await tool.InvokeAsync(aiArgs);
                var resultString = result?.ToString() ?? "null";
                var resultContent = new FunctionResultContent(functionCall.CallId, resultString);
                session.History.Add(new ChatMessage(ChatRole.Tool, [resultContent]));
            }
            catch (Exception ex)
            {
                var errorContent = new FunctionResultContent(functionCall.CallId, $"Error: {ex.Message}");
                session.History.Add(new ChatMessage(ChatRole.Tool, [errorContent]));
            }
        }

        session.PendingCalls.Remove(functionId);

        if (session.PendingCalls.Count > 0)
        {
            return new PromptResponse(null, Project(session));
        }

        var response = await ExecutePrompt(session);
        var lastMessage = response.Messages.LastOrDefault();

        var moreCalls = lastMessage?.Contents
            .OfType<FunctionCallContent>()
            .ToArray() ?? [];

        return ExtractFunctionsAndSyncSession(session, lastMessage, moreCalls);
    }

    public Task<PromptResponse> DeclineAsync(string userKey, string functionId)
    {
        Clear(userKey);
        return Task.FromResult(new PromptResponse("Conversation terminated by user.", new()));
    }

    private void Clear(string userKey) => _sessions.TryRemove(userKey, out _);

    private static List<PendingFunctionCall> Project(ChatSession session)
    {
        var list = new List<PendingFunctionCall>();
        foreach (var function in session.PendingCalls.Values)
        {
            string args;
            try
            {
                args = function.Arguments is null ? "{}" : JsonSerializer.Serialize(function.Arguments, new JsonSerializerOptions { WriteIndented = true });
            }
            catch { args = function.Arguments?.ToString() ?? string.Empty; }
            list.Add(new PendingFunctionCall(function.CallId, function.Name, null, args));
        }
        return list;
    }
}
