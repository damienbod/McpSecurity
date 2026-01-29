using ClientLibrary;
using McpClient;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
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

// Create base chat client
var baseChatClient = ChatClientHelper.GetChatClient(config);

// initialize MCP client
using var httpClient = new HttpClient();

// secure transport with authentication
var transport = useSecureTransport
   ? await McpHelper.CreateMcpTransportAsync(httpClient, config)
   : await McpHelper.CreateUnsecureMcpTransportAsync(httpClient, config);

await using IMcpClient mcpClient = await McpClientFactory.CreateAsync(transport, McpHelper.CreateMcpClientOptions());

// Get MCP tools as AIFunctions
var mcpTools = await mcpClient.GetMcpToolsAsAIFunctionsAsync();

// Create chat client with function invocation if using elicitation (auto-invoke)
IChatClient chatClient = useMcpElicitation
    ? new ChatClientBuilder(baseChatClient).UseFunctionInvocation().Build()
    : baseChatClient;

// Prepare chat options with tools
var chatOptions = ChatClientHelper.CreateChatOptions(mcpTools.Cast<AITool>());

var prompt = "Please generate a random string";
var chatHistory = ChatClientHelper.InitializeHistory(prompt);
Console.WriteLine($"User: {prompt}");

// Execute prompt
var response = await chatClient.GetResponseAsync(chatHistory, chatOptions);

// Process function calls if not using auto-invoke (elicitation)
if (!useMcpElicitation)
{
    response = await FunctionCallHelper.ProcessFunctionCalls(chatClient, chatOptions, chatHistory, response, mcpTools);
}

Console.WriteLine($"AI response: {response.Text}");