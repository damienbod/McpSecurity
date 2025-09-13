using McpWebClient.AiServices;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ModelContextProtocol.Client;
using System.Runtime.CompilerServices;

namespace McpWebClient;

public class LlmPromptService
{
    private readonly IConfigurationRoot _configuration;
    private Kernel _kernel;

    public LlmPromptService(IConfigurationRoot configuration)
    {
        _configuration = configuration;
        // Prepare and build kernel
        _kernel = SemanticKernelHelper.GetKernel(_configuration);
    }

    public async Task Setup(IHttpClientFactory clientFactory)
    {
        // initialize MCP client
        var transport = CreateMcpTransport(clientFactory);
        await using IMcpClient mcpClient = await McpClientFactory.CreateAsync(transport);

        // Retrieve the list of tools available on the MCP server and import them to the kernel
        await _kernel.ImportMcpClientFunctionsAsync(mcpClient);
    }

    private static IClientTransport CreateMcpTransport(IHttpClientFactory clientFactory)
    {
        var httpClient = clientFactory.CreateClient();
        //var serverUrl = "https://localhost:7133/mcp";
        var serverUrl = "https://mcpoauthsecurity-hag0drckepathyb6.westeurope-01.azurewebsites.net/mcp";
        var transport = new SseClientTransport(new()
        {
            Endpoint = new Uri(serverUrl),
            Name = "Secure Client",
            OAuth = new()
            {
                ClientName = "HttpMcpClient",
                RedirectUri = new Uri("https://localhost:5001/signin-oidc"), 
                ClientId = "344677a4-a975-4cba-a4b0-2d0771847938",
                Scopes = ["api://96b0f495-3b65-4c8f-a0c6-c3767c3365ed/mcp:tools"],
            }
        }, httpClient);

        return transport;
    }

    public async Task<string?> Chat(string prompt)
    {
        // Prepare execution
        var executionSettings = SemanticKernelHelper.CreatePromptSettings(autoInvokeTools: false);

        var chatHistory = SemanticKernelHelper.InitializeHistory(prompt);
        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

        // execute prompt
        var messageContent = await chatCompletionService.GetChatMessageContentAsync(chatHistory, executionSettings, _kernel);

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
                // disable function 
                // chatHistory.Add(new FunctionResultContent(functionCall.FunctionName, functionCall.PluginName, functionCall.Id).ToChatMessage());
                // functionCalled = true;
                
                var result = await functionCall.InvokeAsync(_kernel);
                chatHistory.Add(result.ToChatMessage());
            }

            // Sending the functions invocation results to the AI model to get the final response
            if (functionCalled)
            {
                messageContent = await chatCompletionService.GetChatMessageContentAsync(chatHistory, executionSettings, _kernel);
                functionCalls = FunctionCallContent.GetFunctionCalls(messageContent).ToArray();
            }
            else
            {
                functionCalls = [];
            }
        }

        return messageContent.Content;
    }
}
