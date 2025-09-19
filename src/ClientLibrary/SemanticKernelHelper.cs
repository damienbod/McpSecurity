using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace ClientLibrary;
public static class SemanticKernelHelper
{
    public static OpenAIPromptExecutionSettings CreatePromptSettings(bool autoInvokeTools)
    {
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        return new()
        {
            Temperature = 0,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: autoInvokeTools, options: new() { RetainArgumentTypes = true })
        };
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    }

    public static ChatHistory InitializeHistory(string prompt)
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);
        return chatHistory;
    }

    public static Kernel GetKernel(IConfigurationRoot config)
    {
        // Prepare and build kernel
        var builder = Kernel.CreateBuilder();
        builder.Services.AddLogging(c => c.AddDebug().SetMinimumLevel(LogLevel.Trace));
        builder.Services.AddAzureOpenAIChatCompletion(
            config["OpenAI:ModelId"],
            config["OpenAI:Endpoint"],
            config["OpenAI:ApiKey"]!);

        return builder.Build();
    }
}
