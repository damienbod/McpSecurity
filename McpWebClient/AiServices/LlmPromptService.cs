using Azure.Core;
using McpWebClient.AiServices;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ModelContextProtocol.Client;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace McpWebClient;

public class LlmPromptService
{
    private readonly IConfiguration _configuration;
    private Kernel _kernel;

    public LlmPromptService(IConfiguration configuration)
    {
        _configuration = configuration;

        // TODO fix, add this into web configuration
        var config = new ConfigurationBuilder()
        .AddUserSecrets<Program>()
        .Build();

        // Prepare and build kernel
        _kernel = SemanticKernelHelper.GetKernel(config);
    }

    public async Task Setup(IHttpClientFactory clientFactory, string accessToken)
    {
        // initialize MCP client
        var transport = CreateMcpTransport(clientFactory, accessToken);
        await using IMcpClient mcpClient = await McpClientFactory.CreateAsync(transport);

        // Retrieve the list of tools available on the MCP server and import them to the kernel
        await _kernel.ImportMcpClientFunctionsAsync(mcpClient);
    }

    private IClientTransport CreateMcpTransport(IHttpClientFactory clientFactory, string accessToken)
    {
        var httpClient = clientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var httpMcpServerUrl = _configuration["HttpMcpServerUrl"];
        if (string.IsNullOrEmpty(httpMcpServerUrl))
        {
            throw new ArgumentNullException("Configuration missing for HttpMcpServerUrl");
        }

        var transport = new SseClientTransport(new()
        {
            Endpoint = new Uri(httpMcpServerUrl),
            Name = "Secure Client",

            // TODO validate if we need this, using AT directly from web app
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
