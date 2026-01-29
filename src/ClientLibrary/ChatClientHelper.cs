using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace ClientLibrary;

/// <summary>
/// Helper class for creating and configuring IChatClient using Microsoft.Extensions.AI
/// </summary>
public static class ChatClientHelper
{
    /// <summary>
    /// Creates ChatOptions for the AI chat client
    /// </summary>
    /// <param name="tools">Optional tools/functions to make available</param>
    /// <param name="autoInvokeTools">Whether to auto-invoke tools or return them for manual processing</param>
    public static ChatOptions CreateChatOptions(IEnumerable<AITool>? tools = null)
    {
        var options = new ChatOptions
        {
            Temperature = 0f
        };

        if (tools != null)
        {
            foreach (var tool in tools)
            {
                options.Tools ??= [];
                options.Tools.Add(tool);
            }

            // Set tool mode - Auto means the model decides when to call tools
            options.ToolMode = ChatToolMode.Auto;

        }

        return options;
    }

    /// <summary>
    /// Initializes a new chat message list with a user prompt
    /// </summary>
    public static List<ChatMessage> InitializeHistory(string prompt)
    {
        return [new ChatMessage(ChatRole.User, prompt)];
    }

    /// <summary>
    /// Creates an IChatClient configured for Azure OpenAI
    /// </summary>
    public static IChatClient GetChatClient(IConfigurationRoot config)
    {
        var endpoint = config["OpenAI:Endpoint"] ?? throw new InvalidOperationException("OpenAI:Endpoint configuration is missing");
        var apiKey = config["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey configuration is missing");
        var modelId = config["OpenAI:ModelId"] ?? throw new InvalidOperationException("OpenAI:ModelId configuration is missing");

        var azureClient = new AzureOpenAIClient(
            new Uri(endpoint),
            new ApiKeyCredential(apiKey));

        // GetChatClient returns OpenAI.Chat.ChatClient which has the AsIChatClient extension
        OpenAI.Chat.ChatClient openAiChatClient = azureClient.GetChatClient(modelId);
        return openAiChatClient.AsIChatClient();
    }

    /// <summary>
    /// Creates an IChatClient with function invocation middleware for auto-invoke scenarios
    /// </summary>
    public static IChatClient GetChatClientWithFunctionInvocation(IConfigurationRoot config, IEnumerable<AIFunction>? functions = null)
    {
        var baseClient = GetChatClient(config);

        var builder = new ChatClientBuilder(baseClient);

        if (functions != null)
        {
            builder.UseFunctionInvocation();
        }

        return builder.Build();
    }
}
