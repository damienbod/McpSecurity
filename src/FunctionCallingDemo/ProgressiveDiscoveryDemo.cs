using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace FunctionCallingDemo;

/// <summary>
/// Compares advertising a full catalog with discovering functions as they are needed.
/// </summary>
public static class ProgressiveDiscoveryDemo
{
    private const int MaxRequests = 12;
    private const string Prompt = "Use the tools to tell me the current time and the weather in Basel.";

    public static async Task RunAsync(IChatClient client)
    {
        ConsoleUi.Title("11. Progressive tool discovery",
            "Compare a full tool catalog with keyword discovery; token totals include every model request.");

        var catalog = CreateCatalog();
        ConsoleUi.Info($"Catalog: {catalog.Count} functions ({catalog.Count - 2} simulated), same prompt and catalog for both runs.");

        ConsoleUi.Step("Baseline: advertise the entire catalog");
        var baseline = await RunScenarioAsync(client, catalog, progressive: false);

        ConsoleUi.Step("Progressive: start with search_tools, then expose discovered functions");
        var progressive = await RunScenarioAsync(client, catalog, progressive: true);

        ConsoleUi.Step("Comparison");
        PrintSummary("Baseline", baseline);
        PrintSummary("Progressive", progressive);
        if (baseline.Completed && progressive.Completed &&
            baseline.CalledBothFunctions && progressive.CalledBothFunctions &&
            baseline.InputTokens is > 0 && progressive.InputTokens is not null)
        {
            var change = 100.0 * (baseline.InputTokens.Value - progressive.InputTokens.Value) / baseline.InputTokens.Value;
            ConsoleUi.Info($"Input token reduction: {change:F1}% (negative means discovery used more).");
        }
        else
        {
            ConsoleUi.Info("Input token reduction unavailable: both runs need complete input usage, final answers, and both requested function calls.");
        }
        ConsoleUi.Info("Usage includes tool schemas, discovery results, conversation history, and all model round trips.");
    }

    private static List<AIFunction> CreateCatalog()
    {
        List<AIFunction> catalog = [Tools.GetWeather, Tools.GetCurrentTime];
        string[] domains =
        [
            "accounting", "inventory", "shipping", "marketing", "payroll",
            "recruitment", "manufacturing", "maintenance", "compliance", "purchasing"
        ];
        string[] operations =
        [
            "find_record", "list_records", "create_record", "update_record", "archive_record",
            "summarize_records", "validate_record", "export_records", "get_status", "get_history"
        ];

        foreach (var domain in domains)
        {
            foreach (var operation in operations)
            {
                var name = $"{domain}_{operation}";
                var description = $"Simulated {domain} function to {operation.Replace('_', ' ')}. " +
                    $"Use this for {domain} business records and related administrative workflows; " +
                    "provide a specific identifier or search term as the subject." +
                    "Add another few lines to make the description longer and consume even more tokens." +
                    "Add another few lines to make the description longer and consume even more tokens." +
                    "Add another few lines to make the description longer and consume even more tokens." +
                    "Add another few lines to make the description longer and consume even more tokens." +
                    "Add another few lines to make the description longer and consume even more tokens." +
                    "Add another few lines to make the description longer and consume even more tokens." +
                    "Add another few lines to make the description longer and consume even more tokens." +
                    "Add another few lines to make the description longer and consume even more tokens." +
                    "Add another few lines to make the description longer and consume even more tokens." +
                    "Add another few lines to make the description longer and consume even more tokens." +
                    "Add another few lines to make the description longer and consume even more tokens." +
                    "Add another few lines to make the description longer and consume even more tokens." +
                    "Add another few lines to make the description longer and consume even more tokens." +
                    "Add another few lines to make the description longer and consume even more tokens." +
                    "Add another few lines to make the description longer and consume even more tokens." +
                    "Add another few lines to make the description longer and consume even more tokens." +
                    "Add another few lines to make the description longer and consume even more tokens." +
                    "Add another few lines to make the description longer and consume even more tokens." +
                    "Add another few lines to make the description longer and consume even more tokens.";
                catalog.Add(AIFunctionFactory.Create(
                    (string subject) => $"Simulated {name} result for {subject}. ",
                    name, description));
            }
        }

        return catalog;
    }

    private static async Task<ScenarioResult> RunScenarioAsync(
        IChatClient client, IReadOnlyList<AIFunction> catalog, bool progressive)
    {
        var discovered = new Dictionary<string, AIFunction>(StringComparer.Ordinal);
        var called = new HashSet<string>(StringComparer.Ordinal);
        var search = AIFunctionFactory.Create(
            [Description("Searches the local tool catalog by keywords. Returns matching tool names and descriptions and makes them available on the next request.")]
        (string query) =>
            {
                var matches = SearchCatalog(catalog, query);
                foreach (var match in matches)
                {
                    discovered.TryAdd(match.Name, match);
                }

                return matches.Select(tool => new { tool.Name, tool.Description }).ToArray();
            },
            "search_tools");

        List<ChatMessage> history = [new(ChatRole.User, Prompt)];
        var stopwatch = Stopwatch.StartNew();
        long input = 0, output = 0;
        var inputComplete = true;
        var outputComplete = true;
        var requests = 0;
        var toolCalls = 0;
        var searches = 0;
        var completed = false;
        ConsoleUi.User(Prompt);

        while (requests < MaxRequests)
        {
            // Snapshot the advertised functions: a search cannot authorize a call in the same response.
            var available = progressive
                ? new[] { search }.Concat(discovered.Values).ToArray()
                : catalog.ToArray();
            var options = new ChatOptions { Tools = [.. available] };
            var response = await client.GetResponseAsync(history, options);
            requests++;
            if (response.Usage?.InputTokenCount is long inputCount) input += inputCount;
            else inputComplete = false;
            if (response.Usage?.OutputTokenCount is long outputCount) output += outputCount;
            else outputComplete = false;

            ConsoleUi.Info($"Request {requests}: {available.Length} advertised functions, " +
                $"input tokens: {response.Usage?.InputTokenCount.ToString() ?? "unavailable"}, " +
                $"output tokens: {response.Usage?.OutputTokenCount.ToString() ?? "unavailable"}");
            ConsoleUi.Messages(response.Messages);
            history.AddMessages(response);

            var calls = response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().ToList();
            if (calls.Count == 0)
            {
                completed = response.FinishReason == ChatFinishReason.Stop &&
                    response.Messages.SelectMany(message => message.Contents)
                        .OfType<TextContent>().Any(content => !string.IsNullOrWhiteSpace(content.Text));
                break;
            }

            foreach (var call in calls)
            {
                var function = available.FirstOrDefault(tool => tool.Name == call.Name);
                object? result;
                if (function is null)
                {
                    ConsoleUi.Error($"Model requested unavailable function '{call.Name}'; it was not executed.");
                    result = $"Function '{call.Name}' is not available. Search for it first.";
                }
                else
                {
                    result = await function.InvokeAsync(new AIFunctionArguments(call.Arguments));
                    called.Add(function.Name);
                    toolCalls++;
                    if (function.Name == search.Name) searches++;
                }

                var toolMessage = new ChatMessage(ChatRole.Tool, [new FunctionResultContent(call.CallId, result)]);
                ConsoleUi.Messages([toolMessage]);
                history.Add(toolMessage);
            }
        }

        if (!completed)
        {
            ConsoleUi.Error($"No completed final answer (finish reason or {MaxRequests}-request limit).");
        }

        return new ScenarioResult(
            inputComplete ? input : null, outputComplete ? output : null, requests,
            toolCalls, searches, stopwatch.ElapsedMilliseconds, completed,
            called.Contains(Tools.GetWeather.Name) && called.Contains(Tools.GetCurrentTime.Name));
    }

    private static List<AIFunction> SearchCatalog(IReadOnlyList<AIFunction> catalog, string query)
    {
        var terms = Regex.Matches(query.ToLowerInvariant(), "[a-z0-9]+")
            .Select(match => match.Value)
            .Where(term => term.Length > 2)
            .Distinct()
            .ToArray();

        return catalog
            .Select(tool => new
            {
                Tool = tool,
                Score = terms.Count(term => tool.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (tool.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
            })
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Tool.Name, StringComparer.Ordinal)
            .Take(5)
            .Select(match => match.Tool)
            .ToList();
    }

    private static void PrintSummary(string name, ScenarioResult result) =>
        ConsoleUi.Info($"{name}: input={result.InputTokens?.ToString() ?? "unavailable"}, " +
            $"output={result.OutputTokens?.ToString() ?? "unavailable"}, " +
            $"requests={result.Requests}, tool calls={result.ToolCalls} (searches={result.Searches}), " +
            $"elapsed={result.ElapsedMilliseconds} ms, " +
            $"final answer={(result.Completed ? "yes" : "no")}, " +
            $"both requested functions called={(result.CalledBothFunctions ? "yes" : "no")}");

    private sealed record ScenarioResult(long? InputTokens, long? OutputTokens, int Requests,
        int ToolCalls, int Searches, long ElapsedMilliseconds, bool Completed, bool CalledBothFunctions);
}
