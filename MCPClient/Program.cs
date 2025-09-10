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
var chatHistory = SemanticKernelHelper.InitializeHistory("Please generate five random numbers?");
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
        Console.WriteLine($"Please allow/decline function execution with: {functionCall.FunctionName} with arguments: {functionCall.Arguments} [y,n]");
        var key = Console.ReadKey();

        if (key.KeyChar != 'y' && key.KeyChar != 'Y')
        {
            Console.WriteLine($"\nFunction call {functionCall.FunctionName} declined");
            chatHistory.Add(new FunctionResultContent(functionCall.FunctionName, functionCall.PluginName, functionCall.Id).ToChatMessage());
            continue;
        }

        functionCalled = true;
        var result = await functionCall.InvokeAsync(kernel);
        chatHistory.Add(result.ToChatMessage());
        Console.WriteLine($"Function call : {result}");
    }

    // Sending the functions invocation results to the AI model to get the final response
    if (functionCalled)
    {
        messageContent = await chatCompletionService.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);
        Console.WriteLine($"AI Agent: {messageContent.Content}");
        functionCalls = FunctionCallContent.GetFunctionCalls(messageContent).ToArray();
    }
    else
    {
        functionCalls = [];
    }
}

Console.WriteLine($"\nAI response: {messageContent.Content}");