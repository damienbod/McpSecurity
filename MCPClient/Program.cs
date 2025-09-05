// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol.Client;

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

// Prepare and build kernel
var builder = Kernel.CreateBuilder();
builder.Services.AddLogging(c => c.AddDebug().SetMinimumLevel(LogLevel.Trace));
builder.Services.AddAzureOpenAIChatCompletion(
    config["OpenAI:ModelId"],
    config["OpenAI:Endpoint"],
    config["OpenAI:ApiKey"]!);

Kernel kernel = builder.Build();

// We can customize a shared HttpClient with a custom handler if desired
using var sharedHandler = new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1)
};
using var httpClient = new HttpClient(sharedHandler);

var serverUrl = "https://localhost:7133/mcp";
var transport = new SseClientTransport(new()
{
    Endpoint = new Uri(serverUrl),
    Name = "Secure Weather Client",
    //OAuth = new()
    //{
    //    ClientName = "ProtectedMcpClient",
    //    RedirectUri = new Uri("http://localhost:1179/callback"),

    //}
}, httpClient);

// Create an MCPClient for the protected MCP server
await using var mcpClient = await McpClientFactory.CreateAsync(transport);


// Retrieve the list of tools available on the MCP server
var tools = await mcpClient.ListToolsAsync().ConfigureAwait(false);
foreach (var tool in tools)
{
    Console.WriteLine($"{tool.Name}: {tool.Description}");
}

kernel.Plugins.AddFromFunctions("Tools", tools.Select(aiFunction => aiFunction.AsKernelFunction()));

// Enable automatic function calling
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
OpenAIPromptExecutionSettings executionSettings = new()
{
    Temperature = 0,
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new() { RetainArgumentTypes = true })
};
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

// Test using GitHub tools
var prompt = "Please generate a random number?";
var result = await kernel.InvokePromptAsync(prompt, new(executionSettings)).ConfigureAwait(false);
Console.WriteLine($"\n\n{prompt}\n{result}");