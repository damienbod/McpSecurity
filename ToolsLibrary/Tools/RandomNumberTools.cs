using ModelContextProtocol.Server;
using System.ComponentModel;

/// <summary>
/// Sample MCP tools for demonstration purposes.
/// These tools can be invoked by MCP clients to perform various operations.
/// </summary>
[McpServerToolType]
public class RandomNumberTools
{
    [McpServerTool]
    [Description("Generates a random number between the specified minimum and maximum values.")]
    public int GetRandomNumber(
        [Description("Minimum value (inclusive)")] int min = 0,
        [Description("Maximum value (exclusive)")] int max = 100)
    {
        return Random.Shared.Next(min, max);
    }

    [McpServerTool]
    [Description("Generates a random number based on a date.")]
    public int GetRandomNumberFromDateTime(
        [Description("The date to generate random number from")] DateTime? datetime = null)
    {
        if (datetime == null)
        {
            datetime = DateTime.Now;
        }

        var min = (int)datetime.Value.Ticks % 100;
        var max = min + 1_000;

        return Random.Shared.Next(min, max);
    }
}

