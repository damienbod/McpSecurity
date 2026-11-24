using Microsoft.Extensions.AI;
using System.Diagnostics;

namespace FunctionCallingDemo;

/// <summary>
/// One demo per Microsoft.Extensions.AI (MEAI) function calling setting.
/// </summary>
public class Demos(IChatClient baseClient)
{
    // ------------------------------------------------------------------
    // The "agent": send a prompt and print everything that happened.
    // ------------------------------------------------------------------
    private static async Task<ChatResponse> AskAsync(IChatClient client, string prompt, ChatOptions options)
    {
        ConsoleUi.User(prompt);
        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetResponseAsync(prompt, options);
        ConsoleUi.Messages(response.Messages);
        ConsoleUi.Info($"{stopwatch.ElapsedMilliseconds} ms, finish reason: {response.FinishReason?.ToString() ?? "-"}");
        return response;
    }

    // Wraps the base client with the FunctionInvokingChatClient (auto function calling)
    private IChatClient WithFunctionInvocation(Action<FunctionInvokingChatClient>? configure = null) =>
        new ChatClientBuilder(baseClient)
            .UseFunctionInvocation(configure: configure)
            .Build();

    // ------------------------------------------------------------------
    // 1. Automatic function invocation
    // ------------------------------------------------------------------
    public async Task AutoInvokeAsync()
    {
        ConsoleUi.Title("1. Automatic function invocation",
            "UseFunctionInvocation() adds a FunctionInvokingChatClient that runs the call loop for you.");

        var client = WithFunctionInvocation();
        var options = new ChatOptions { Tools = [Tools.GetWeather, Tools.GetCurrentTime] };

        await AskAsync(client, "What time is it and what is the weather in Basel?", options);
    }

    // ------------------------------------------------------------------
    // 2. Manual function invocation (no FunctionInvokingChatClient)
    // ------------------------------------------------------------------
    public async Task ManualInvokeAsync()
    {
        ConsoleUi.Title("2. Manual function invocation",
            "Without UseFunctionInvocation() the model only *requests* calls. You execute them and send results back.");

        AIFunction[] tools = [Tools.GetWeather, Tools.GetCurrentTime];
        var options = new ChatOptions { Tools = [.. tools] };
        var prompt = "What time is it and what is the weather in Köln?";

        ConsoleUi.User(prompt);
        List<ChatMessage> history = [new(ChatRole.User, prompt)];

        while (true)
        {
            var response = await baseClient.GetResponseAsync(history, options);
            ConsoleUi.Messages(response.Messages);
            history.AddMessages(response);

            var calls = response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>().ToList();
            if (calls.Count == 0)
            {
                break;
            }

            ConsoleUi.Info($"Model requested {calls.Count} call(s) - executing them manually");
            foreach (var call in calls)
            {
                var tool = tools.First(t => t.Name == call.Name);
                var result = await tool.InvokeAsync(new AIFunctionArguments(call.Arguments));
                var toolMessage = new ChatMessage(ChatRole.Tool, [new FunctionResultContent(call.CallId, result)]);
                ConsoleUi.Messages([toolMessage]);
                history.Add(toolMessage);
            }
        }
    }

    // ------------------------------------------------------------------
    // 3. ChatOptions.ToolMode
    // ------------------------------------------------------------------
    public async Task ToolModeAsync()
    {
        ConsoleUi.Title("3. ChatOptions.ToolMode",
            "Controls whether the model may, must not, or must call a tool.");

        var client = WithFunctionInvocation();
        var prompt = "Hi! Tell me a short fun fact about Switzerland.";

        (string Name, ChatToolMode Mode)[] modes =
        [
            ("Auto (default) - model decides, here probably no tool needed", ChatToolMode.Auto),
            ("None - tools are known but the model must not call them", ChatToolMode.None),
            ("RequireAny - the model must call at least one tool", ChatToolMode.RequireAny),
            ("RequireSpecific(\"get_current_time\") - the model must call exactly this tool", ChatToolMode.RequireSpecific("get_current_time")),
        ];

        foreach (var (name, mode) in modes)
        {
            ConsoleUi.Step($"ToolMode = {name}");
            var options = new ChatOptions { Tools = [Tools.GetWeather, Tools.GetCurrentTime], ToolMode = mode };
            await AskAsync(client, prompt, options);
        }
    }

    // ------------------------------------------------------------------
    // 4. Parallel calls: AllowConcurrentInvocation & AllowMultipleToolCalls
    // ------------------------------------------------------------------
    public async Task ParallelAsync()
    {
        ConsoleUi.Title("4. Parallel function calls",
            "FunctionInvokingChatClient.AllowConcurrentInvocation and ChatOptions.AllowMultipleToolCalls. Each get_weather call takes 1s.");

        var prompt = "What is the weather in Basel, Bern and Geneva?";

        ConsoleUi.Step("AllowConcurrentInvocation = false (default) - multiple calls in one response run one after another");
        await AskAsync(WithFunctionInvocation(), prompt,
            new ChatOptions { Tools = [Tools.GetWeather] });

        ConsoleUi.Step("AllowConcurrentInvocation = true - multiple calls in one response run in parallel");
        await AskAsync(WithFunctionInvocation(f => f.AllowConcurrentInvocation = true), prompt,
            new ChatOptions { Tools = [Tools.GetWeather] });

        ConsoleUi.Step("ChatOptions.AllowMultipleToolCalls = false - the model may only request one call per response");
        await AskAsync(WithFunctionInvocation(), prompt,
            new ChatOptions { Tools = [Tools.GetWeather], AllowMultipleToolCalls = false });
    }

    // ------------------------------------------------------------------
    // 5. MaximumIterationsPerRequest
    // ------------------------------------------------------------------
    public async Task MaxIterationsAsync()
    {
        ConsoleUi.Title("5. MaximumIterationsPerRequest",
            "Limits how many model round trips with function calls are done for one request (default 40).");

        var prompt = "Use the add tool step by step, one call at a time: add 1 and 2, then add 10 to the result, then add 100 to that result. Do not calculate yourself.";
        var options = new ChatOptions { Tools = [Tools.Add], AllowMultipleToolCalls = false };

        ConsoleUi.Step("MaximumIterationsPerRequest = 40 (default)");
        await AskAsync(WithFunctionInvocation(), prompt, options);

        ConsoleUi.Step("MaximumIterationsPerRequest = 1 - the loop stops early");
        await AskAsync(WithFunctionInvocation(f => f.MaximumIterationsPerRequest = 1), prompt, options);
    }

    // ------------------------------------------------------------------
    // 6. Error handling: IncludeDetailedErrors & MaximumConsecutiveErrorsPerRequest
    // ------------------------------------------------------------------
    public async Task ErrorsAsync()
    {
        ConsoleUi.Title("6. Error handling",
            "IncludeDetailedErrors and MaximumConsecutiveErrorsPerRequest. The lookup_customer tool always throws.");

        var prompt = "Look up customer 42.";
        var options = new ChatOptions { Tools = [Tools.BrokenDatabase] };

        ConsoleUi.Step("IncludeDetailedErrors = false (default) - the model only gets a generic error");
        await AskAsync(WithFunctionInvocation(), prompt, options);

        ConsoleUi.Step("IncludeDetailedErrors = true - the exception message is sent to the model (may leak internals!)");
        await AskAsync(WithFunctionInvocation(f => f.IncludeDetailedErrors = true), prompt, options);

        ConsoleUi.Step("MaximumConsecutiveErrorsPerRequest = 0 (default 3) - the exception is thrown to the caller");
        try
        {
            await AskAsync(WithFunctionInvocation(f => f.MaximumConsecutiveErrorsPerRequest = 0), prompt, options);
        }
        catch (Exception ex)
        {
            ConsoleUi.Error($"Caller received {ex.GetType().Name}: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------
    // 7. Unknown calls: TerminateOnUnknownCalls & AdditionalTools
    // ------------------------------------------------------------------
    public async Task UnknownCallsAsync()
    {
        ConsoleUi.Title("7. Unknown function calls",
            "TerminateOnUnknownCalls and AdditionalTools. The model is told about get_stock_price, but it is not in ChatOptions.Tools.");

        var prompt = "What is the current stock price of MSFT?";
        var options = new ChatOptions { Tools = [Tools.GetCurrentTime] };

        // Simulates a tool that is advertised to the model "behind the back" of the
        // FunctionInvokingChatClient (e.g. added by another middleware or the service).
        IChatClient Build(Action<FunctionInvokingChatClient>? configure) =>
            new ChatClientBuilder(baseClient)
                .UseFunctionInvocation(configure: configure)
                .Use(async (messages, chatOptions, next, cancellationToken) =>
                {
                    var injected = chatOptions?.Clone() ?? new ChatOptions();
                    injected.Tools = [.. injected.Tools ?? [], Tools.GetStockPrice.AsDeclarationOnly()];
                    await next(messages, injected, cancellationToken);
                })
                .Build();

        ConsoleUi.Step("TerminateOnUnknownCalls = false (default) - an error result 'function not found' is sent to the model");
        await AskAsync(Build(null), prompt, options);

        ConsoleUi.Step("TerminateOnUnknownCalls = true - the loop stops and the call is returned to the caller");
        await AskAsync(Build(f => f.TerminateOnUnknownCalls = true), prompt, options);

        ConsoleUi.Step("AdditionalTools = [get_stock_price] - the client knows how to invoke it");
        await AskAsync(Build(f => f.AdditionalTools = [Tools.GetStockPrice]), prompt, options);
    }

    // ------------------------------------------------------------------
    // 8. Terminate from inside a function
    // ------------------------------------------------------------------
    public async Task TerminateAsync()
    {
        ConsoleUi.Title("8. Terminate the loop from a function",
            "A tool sets FunctionInvokingChatClient.CurrentContext.Terminate = true -> no final model answer.");

        var options = new ChatOptions { Tools = [Tools.EscalateToHuman, Tools.GetCurrentTime] };
        await AskAsync(WithFunctionInvocation(),
            "My order never arrived and I am very angry. I want to talk to a human now!", options);
    }

    // ------------------------------------------------------------------
    // 9. Custom FunctionInvoker
    // ------------------------------------------------------------------
    public async Task FunctionInvokerAsync()
    {
        ConsoleUi.Title("9. Custom FunctionInvoker",
            "Intercepts every function call: logging, policies or overriding results.");

        var client = WithFunctionInvocation(f => f.FunctionInvoker = async (context, cancellationToken) =>
        {
            ConsoleUi.Info($"FunctionInvoker: iteration {context.Iteration}, call {context.FunctionCallIndex + 1}/{context.FunctionCount}: {context.Function.Name}({ConsoleUi.FormatArgs(context.Arguments)})");

            if (context.Arguments.TryGetValue("city", out var city) && city?.ToString() == "Atlantis")
            {
                ConsoleUi.Info("FunctionInvoker: blocked by policy, function not executed");
                return "This city is not supported.";
            }

            return await context.Function.InvokeAsync(context.Arguments, cancellationToken);
        });

        var options = new ChatOptions { Tools = [Tools.GetWeather] };
        await AskAsync(client, "What is the weather in Basel and in Atlantis?", options);
    }

    // ------------------------------------------------------------------
    // 10. Human in the loop: ApprovalRequiredAIFunction
    // ------------------------------------------------------------------
    public async Task ApprovalAsync()
    {
        ConsoleUi.Title("10. Human in the loop (ApprovalRequiredAIFunction)",
            "Functions wrapped in ApprovalRequiredAIFunction are not executed until the user approves them.");

        var client = WithFunctionInvocation();
        var options = new ChatOptions { Tools = [new ApprovalRequiredAIFunction(Tools.DeleteFile), Tools.GetCurrentTime] };
        var prompt = "What time is it? Then delete the file /tmp/report.txt.";

        ConsoleUi.User(prompt);
        List<ChatMessage> history = [new(ChatRole.User, prompt)];
        var response = await client.GetResponseAsync(history, options);
        ConsoleUi.Messages(response.Messages);

        var approvals = response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().ToList();
        while (approvals.Count > 0)
        {
            history.AddMessages(response);

            List<AIContent> answers = [];
            foreach (var approval in approvals)
            {
                var call = (FunctionCallContent)approval.ToolCall;
                ConsoleUi.Write(ConsoleColor.Yellow, $"  ❓ Allow {call.Name}({ConsoleUi.FormatArgs(call.Arguments)})? [y/n]");
                var approved = Console.ReadKey(intercept: true).KeyChar is 'y' or 'Y';
                ConsoleUi.Info(approved ? "approved" : "rejected");
                answers.Add(approval.CreateResponse(approved, approved ? null : "User declined"));
            }

            history.Add(new ChatMessage(ChatRole.User, answers));
            response = await client.GetResponseAsync(history, options);
            ConsoleUi.Messages(response.Messages);
            approvals = response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().ToList();
        }
    }
}
