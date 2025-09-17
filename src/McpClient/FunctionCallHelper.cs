using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace McpClient
{
    public static class FunctionCallHelper
    {
        public static async Task<ChatMessageContent> ProcessFunctionCalls(Kernel kernel, Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings executionSettings, ChatHistory chatHistory, IChatCompletionService chatCompletionService, ChatMessageContent messageContent)
        {
            var functionCalls = FunctionCallContent.GetFunctionCalls(messageContent).ToArray();
            var functionCalled = false;

            // human-in-the-loop for function calling approval
            while (functionCalls.Length != 0)
            {
                // Adding function call from AI model to chat history
                chatHistory.Add(messageContent);

                // Iterating over the requested function calls and invoking them
                foreach (var functionCall in functionCalls)
                {
                    // approve function call
                    Console.WriteLine($"Please allow/decline function execution with: {functionCall.FunctionName} with arguments: {string.Join(';', functionCall.Arguments.Select(x => $"{x.Key}:{x.Value}"))} [y,n]");
                    var key = Console.ReadKey();
                    Console.WriteLine();

                    if (key.KeyChar != 'y' && key.KeyChar != 'Y')
                    {
                        Console.WriteLine($"Function call {functionCall.FunctionName} declined");
                        chatHistory.Add(new FunctionResultContent(functionCall.FunctionName, functionCall.PluginName, functionCall.Id).ToChatMessage());
                        continue;
                    }

                    functionCalled = true;
                    var result = await functionCall.InvokeAsync(kernel);
                    chatHistory.Add(result.ToChatMessage());
                    Console.WriteLine($"Function call : {result.InnerContent}");
                }

                // Sending the functions invocation results to the AI model to get the final response
                if (functionCalled)
                {
                    messageContent = await chatCompletionService.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);
                    functionCalls = FunctionCallContent.GetFunctionCalls(messageContent).ToArray();
                }
                else
                {
                    functionCalls = [];
                }
            }

            return messageContent;
        }
    }
}
