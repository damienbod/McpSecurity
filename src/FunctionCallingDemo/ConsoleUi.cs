using System.Text.Json;
using Microsoft.Extensions.AI;

namespace FunctionCallingDemo;

/// <summary>
/// Helpers to print the demo flow nicely to the console.
/// </summary>
public static class ConsoleUi
{
    private static readonly Lock _lock = new();

    public static void Title(string title, string description)
    {
        Console.WriteLine();
        Write(ConsoleColor.Cyan, new string('═', 80));
        Write(ConsoleColor.Cyan, $"  {title}");
        Write(ConsoleColor.DarkCyan, $"  {description}");
        Write(ConsoleColor.Cyan, new string('═', 80));
    }

    public static void Step(string text)
    {
        Console.WriteLine();
        Write(ConsoleColor.Yellow, $"▶ {text}");
        Write(ConsoleColor.DarkYellow, new string('─', 80));
    }

    public static void Info(string text) => Write(ConsoleColor.DarkGray, $"  ℹ {text}");

    public static void Error(string text) => Write(ConsoleColor.Red, $"  ✖ {text}");

    public static void User(string prompt) => Write(ConsoleColor.Green, $"👤 User: {prompt}");

    public static void ToolExecuting(string name, string args) =>
        Write(ConsoleColor.Magenta, $"     ⚙ [executing {name}({args}) on thread {Environment.CurrentManagedThreadId} at {DateTime.Now:HH:mm:ss.fff}]");

    /// <summary>
    /// Prints every message produced by the model and the function invocation loop.
    /// </summary>
    public static void Messages(IEnumerable<ChatMessage> messages)
    {
        foreach (var message in messages)
        {
            foreach (var content in message.Contents)
            {
                switch (content)
                {
                    case FunctionCallContent call:
                        Write(ConsoleColor.Blue, $"  🔧 Function call:   {call.Name}({FormatArgs(call.Arguments)})  [id: {call.CallId}]");
                        break;
                    case FunctionResultContent result:
                        Write(ConsoleColor.DarkBlue, $"  📦 Function result: {JsonSerializer.Serialize(result.Result)}  [id: {result.CallId}]");
                        if (result.Exception is not null)
                        {
                            Write(ConsoleColor.DarkRed, $"     (exception captured locally: {result.Exception.GetType().Name}: {result.Exception.Message})");
                        }
                        break;
                    case ToolApprovalRequestContent approval when approval.ToolCall is FunctionCallContent call:
                        Write(ConsoleColor.DarkYellow, $"  ✋ Approval needed: {call.Name}({FormatArgs(call.Arguments)})");
                        break;
                    case TextContent textContent when !string.IsNullOrWhiteSpace(textContent.Text):
                        Write(ConsoleColor.White, $"🤖 Assistant: {textContent.Text}");
                        break;
                }
            }
        }
    }

    public static string FormatArgs(IDictionary<string, object?>? args) =>
        args is null ? "" : string.Join(", ", args.Select(a => $"{a.Key}={a.Value}"));

    public static void Write(ConsoleColor color, string text)
    {
        lock (_lock)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }
}
