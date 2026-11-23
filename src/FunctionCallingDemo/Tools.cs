using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace FunctionCallingDemo;

/// <summary>
/// Simple local tools used by the demos. Each tool prints when it is executed.
/// </summary>
public static class Tools
{
    public static AIFunction GetWeather { get; } = AIFunctionFactory.Create(
        [Description("Gets the current weather for a city.")]
        async (string city) =>
        {
            ConsoleUi.ToolExecuting("get_weather", $"city={city}");
            await Task.Delay(1000); // simulate a slow API call
            var temperature = city.Sum(c => c) % 30;
            return $"{temperature}°C and sunny in {city}";
        },
        "get_weather");

    public static AIFunction GetCurrentTime { get; } = AIFunctionFactory.Create(
        [Description("Gets the current local date and time.")]
        () =>
        {
            ConsoleUi.ToolExecuting("get_current_time", "");
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        },
        "get_current_time");

    public static AIFunction Add { get; } = AIFunctionFactory.Create(
        [Description("Adds two numbers.")]
        (int a, int b) =>
        {
            ConsoleUi.ToolExecuting("add", $"a={a}, b={b}");
            return a + b;
        },
        "add");

    public static AIFunction GetStockPrice { get; } = AIFunctionFactory.Create(
        [Description("Gets the current stock price for a ticker symbol.")]
        (string ticker) =>
        {
            ConsoleUi.ToolExecuting("get_stock_price", $"ticker={ticker}");
            return $"{ticker}: 123.45 USD";
        },
        "get_stock_price");

    public static AIFunction BrokenDatabase { get; } = AIFunctionFactory.Create(
        [Description("Looks up a customer by id in the database.")]
        string (int customerId) =>
        {
            ConsoleUi.ToolExecuting("lookup_customer", $"customerId={customerId}");
            throw new InvalidOperationException("Connection to SQL server 'db-prod-01' failed (password expired).");
        },
        "lookup_customer");

    public static AIFunction DeleteFile { get; } = AIFunctionFactory.Create(
        [Description("Deletes a file on the server.")]
        (string path) =>
        {
            ConsoleUi.ToolExecuting("delete_file", $"path={path}");
            return $"File '{path}' deleted (simulated).";
        },
        "delete_file");

    public static AIFunction EscalateToHuman { get; } = AIFunctionFactory.Create(
        [Description("Escalates the conversation to a human support agent.")]
        (string reason) =>
        {
            ConsoleUi.ToolExecuting("escalate_to_human", $"reason={reason}");

            // Ask the FunctionInvokingChatClient to stop the loop after this call
            FunctionInvokingChatClient.CurrentContext!.Terminate = true;
            return "Ticket #4711 created. A human will take over.";
        },
        "escalate_to_human");
}
