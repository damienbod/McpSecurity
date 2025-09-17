using ClientLibrary;
using McpWebClient.AiServices.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Collections.Concurrent;
using System.Text.Json;

namespace McpWebClient;

internal partial class PromptingService
{
    private readonly Kernel _kernel;
    private readonly bool _autoInvoke;

    private static readonly ConcurrentDictionary<string, ChatSession> _sessions = new();

    public PromptingService(Kernel kernel, bool autoInvoke)
    {
        _kernel = kernel;
        _autoInvoke = autoInvoke;
    }

    public async Task<ChatResponse> BeginAsync(string userKey, string prompt)
    {
        var session = _sessions[userKey] = new() { LastUpdatedUtc = DateTime.UtcNow };
        session.History.AddUserMessage(prompt);

        var messageContent = await ExecutePrompt(session);

        var functionCalls = FunctionCallContent.GetFunctionCalls(messageContent).ToArray();

        return ExtractFunctionsAndSyncSession(session, messageContent, functionCalls);
    }

    private static ChatResponse ExtractFunctionsAndSyncSession(ChatSession session, ChatMessageContent messageContent, FunctionCallContent[] functionCalls)
    {
        if (functionCalls.Length > 0)
        {
            session.History.Add(messageContent);
            foreach (var call in functionCalls)
            {
                session.PendingCalls[call.Id] = call;
            }
            session.LastUpdatedUtc = DateTime.UtcNow;
            return new ChatResponse(null, Project(session));
        }

        session.FinalAnswer = messageContent.Content;
        session.LastUpdatedUtc = DateTime.UtcNow;
        return new ChatResponse(session.FinalAnswer, new());
    }

    private async Task<ChatMessageContent> ExecutePrompt(ChatSession session)
    {
        var executionSettings = SemanticKernelHelper.CreatePromptSettings(autoInvokeTools: _autoInvoke);
        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

        var messageContent = await chatCompletionService.GetChatMessageContentAsync(session.History, executionSettings, _kernel);
        return messageContent;
    }

    public async Task<ChatResponse> ApproveAsync(string userKey, string functionId)
    {
        if (!_sessions.TryGetValue(userKey, out var session))
        {
            return new ChatResponse("Session not found. Please start again.", new());
        }

        if (!session.PendingCalls.TryGetValue(functionId, out var functionCall))
        {
            return new ChatResponse(session.FinalAnswer, Project(session));
        }

        var result = await functionCall.InvokeAsync(_kernel);
        session.History.Add(result.ToChatMessage());
        session.PendingCalls.Remove(functionId);

        if (session.PendingCalls.Count > 0)
        {
            return new ChatResponse(null, Project(session));
        }


        var messageContent = await ExecutePrompt(session);

        var moreCalls = FunctionCallContent.GetFunctionCalls(messageContent).ToArray();
        return ExtractFunctionsAndSyncSession(session, messageContent, moreCalls);
    }

    public Task<ChatResponse> DeclineAsync(string userKey, string functionId)
    {
        Clear(userKey);
        return Task.FromResult(new ChatResponse("Conversation terminated by user.", new()));
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
            list.Add(new PendingFunctionCall(function.Id, function.FunctionName, function.PluginName, args));
        }
        return list;
    }
}
