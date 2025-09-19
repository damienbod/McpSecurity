using ClientLibrary;
using McpClient;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.ChatCompletion;
using ModelContextProtocol.Client;

// load configuration from app secrets
var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .Build();

// human-in-the-loop for function calling approval
var useMcpElicitation = false;
var useSecureTransport = false;

// Prepare and build kernel
var kernel = SemanticKernelHelper.GetKernel(config);

// initialize MCP client

using var httpClient = new HttpClient();

// secure transport with authentication
var transport = useSecureTransport
   ? await McpHelper.CreateMcpTransportAsync(httpClient, config)
   : await McpHelper.CreateUnsecureMcpTransportAsync(httpClient, config);

await using IMcpClient mcpClient = await McpClientFactory.CreateAsync(transport, McpHelper.CreateMcpClientOptions());

// import the mcp tools
await kernel.ImportMcpClientToolsAsync(mcpClient);

// Prepare execution
var executionSettings = SemanticKernelHelper.CreatePromptSettings(autoInvokeTools: useMcpElicitation);

var prompt = "Please generate a random string";
var chatHistory = SemanticKernelHelper.InitializeHistory(prompt);
Console.WriteLine($"User: {prompt}");


// execute prompt
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
var messageContent = await chatCompletionService.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);

// New way of accessing function calls using connector agnostic function calling model classes.
if (!useMcpElicitation)
{
    messageContent = await FunctionCallHelper.ProcessFunctionCalls(kernel, executionSettings, chatHistory, chatCompletionService, messageContent);
}

Console.WriteLine($"AI response: {messageContent.Content}");