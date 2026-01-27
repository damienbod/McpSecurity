using Microsoft.Extensions.AI;
using System.Text.Json;

namespace McpClient
{
    public static class FunctionCallHelper
    {
        public static async Task<ChatResponse> ProcessFunctionCalls(
            IChatClient chatClient, 
            ChatOptions chatOptions, 
            IList<ChatMessage> chatHistory, 
            ChatResponse response,
            IList<AIFunction> availableTools)
        {
            // Get the last message from the response
            var lastMessage = response.Messages.LastOrDefault();
            if (lastMessage == null) return response;

            // Check for function calls in the response
            var functionCalls = lastMessage.Contents
                .OfType<FunctionCallContent>()
                .ToArray();

            // human-in-the-loop for function calling approval
            while (functionCalls.Length != 0)
            {
                // Add the assistant's message with function calls to history
                chatHistory.Add(lastMessage);

                var functionResults = new List<FunctionResultContent>();
                var anyFunctionCalled = false;

                // Iterating over the requested function calls and invoking them
                foreach (var functionCall in functionCalls)
                {
                    // Format arguments for display
                    var argsDisplay = functionCall.Arguments != null
                        ? string.Join("; ", functionCall.Arguments.Select(x => $"{x.Key}:{x.Value}"))
                        : "none";

                    // Approve function call
                    Console.WriteLine($"Please allow/decline function execution: {functionCall.Name} with arguments: {argsDisplay} [y,n]");
                    var key = Console.ReadKey();
                    Console.WriteLine();

                    if (key.KeyChar != 'y' && key.KeyChar != 'Y')
                    {
                        Console.WriteLine($"Function call {functionCall.Name} declined");
                        // Add declined result
                        functionResults.Add(new FunctionResultContent(functionCall.CallId, "Function call declined by user"));
                        continue;
                    }

                    anyFunctionCalled = true;

                    // Find and invoke the function
                    var tool = availableTools.FirstOrDefault(t => t.Name == functionCall.Name);
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
                            functionResults.Add(new FunctionResultContent(functionCall.CallId, resultString));
                            Console.WriteLine($"Function call result: {resultString}");
                        }
                        catch (Exception ex)
                        {
                            functionResults.Add(new FunctionResultContent(functionCall.CallId, $"Error: {ex.Message}"));
                            Console.WriteLine($"Function call error: {ex.Message}");
                        }
                    }
                    else
                    {
                        functionResults.Add(new FunctionResultContent(functionCall.CallId, $"Tool '{functionCall.Name}' not found"));
                    }
                }

                // Add function results to chat history
                var resultMessage = new ChatMessage(ChatRole.Tool, [.. functionResults]);
                chatHistory.Add(resultMessage);

                // Get next response from AI
                if (anyFunctionCalled)
                {
                    response = await chatClient.GetResponseAsync(chatHistory, chatOptions);
                    lastMessage = response.Messages.LastOrDefault();
                    functionCalls = lastMessage?.Contents
                        .OfType<FunctionCallContent>()
                        .ToArray() ?? [];
                }
                else
                {
                    functionCalls = [];
                }
            }

            return response;
        }
    }
}
