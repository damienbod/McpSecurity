using ClientLibrary;
using FunctionCallingDemo;
using Microsoft.Extensions.Configuration;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json")
    .AddUserSecrets<Program>()
    .Build();

if (string.IsNullOrWhiteSpace(config["OpenAI:Endpoint"]) ||
    string.IsNullOrWhiteSpace(config["OpenAI:ModelId"]) ||
    string.IsNullOrWhiteSpace(config["OpenAI:ApiKey"]))
{
    ConsoleUi.Error("Please set OpenAI:Endpoint, OpenAI:ModelId and OpenAI:ApiKey in secrets.json (dotnet user-secrets).");
    PauseBeforeExit();
    return;
}

var baseClient = ChatClientHelper.GetChatClient(config);
var demos = new Demos(baseClient);

var allDemos = new Dictionary<string, Func<Task>>
{
    ["auto-invoke"] = demos.AutoInvokeAsync,
    ["manual-invoke"] = demos.ManualInvokeAsync,
    ["tool-mode"] = demos.ToolModeAsync,
    ["parallel"] = demos.ParallelAsync,
    ["max-iterations"] = demos.MaxIterationsAsync,
    ["errors"] = demos.ErrorsAsync,
    ["unknown-calls"] = demos.UnknownCallsAsync,
    ["terminate"] = demos.TerminateAsync,
    ["function-invoker"] = demos.FunctionInvokerAsync,
    ["approval"] = demos.ApprovalAsync,
    ["progressive-discovery"] = () => ProgressiveDiscoveryDemo.RunAsync(baseClient),
};

var demo = args.FirstOrDefault() ?? "all";

if (demo == "all")
{
    foreach (var run in allDemos.Values)
    {
        await run();
    }
}
else if (allDemos.TryGetValue(demo, out var run))
{
    await run();
}
else
{
    ConsoleUi.Error($"Unknown demo '{demo}'. Available: all, {string.Join(", ", allDemos.Keys)}");
}

Console.WriteLine();
PauseBeforeExit();

static void PauseBeforeExit()
{
    if (Console.IsInputRedirected)
    {
        return;
    }

    Console.WriteLine("Press any key to exit...");
    Console.ReadKey(intercept: true);
}
