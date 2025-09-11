// See https://aka.ms/new-console-template for more information
using MCPClient;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ModelContextProtocol.Client;

// load configuration from app secrets
var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

// Prepare and build kernel
var kernel = SemanticKernelHelper.GetKernel(config);

// initialize MCP client
using var httpClient = new HttpClient();
var transport = McpHelper.CreateMcpTransport(httpClient);
await using IMcpClient mcpClient = await McpClientFactory.CreateAsync(transport);

// Retrieve the list of tools available on the MCP server and import them to the kernel
await kernel.ImportMcpClientFunctionsAsync(mcpClient);

// Prepare execution
var executionSettings = SemanticKernelHelper.CreatePromptSettings(autoInvokeTools: false);
//var prompt = "Please generate a random number";
var prompt = "Please generate a random number with the ragne of -10 and 10";
//var prompt = "Please generate a random number based from the current date";
//var prompt = "Please generate five random numbers?";
//var prompt = "Please generate two random numbers. Use these numbers to generate a thrid random number within the range of the first two.";

var chatHistory = SemanticKernelHelper.InitializeHistory(prompt);
Console.WriteLine($"User: {prompt}");
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

// execute prompt
var messageContent = await chatCompletionService.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);

// New way of accessing function calls using connector agnostic function calling model classes.
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

Console.WriteLine($"AI response: {messageContent.Content}");