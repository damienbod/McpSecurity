using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ToolsLibrary.Prompts;

[McpServerPromptType]
public class PromptExamples
{
    [McpServerPrompt, Description("Generates a single random number")]
    public static ChatMessage GenerateRandomNumber() =>
        new(ChatRole.User, "Please generate a random number!");

    [McpServerPrompt, Description("Generates multiple random number")]
    public static ChatMessage GenerateMultipleRandomNumber(int n) =>
        new(ChatRole.User, $"Please generate {n} random numbers!");

    [McpServerPrompt, Description("Generates a single random number within a range")]
    public static ChatMessage GenerateRandomNumberInRange(int min, int max) =>
        new(ChatRole.User, $"Please generate a random number with the ragne of {min} and {max}!");

    [McpServerPrompt, Description("Generates a single random number from some magic algortihm based on the current date")]
    public static ChatMessage GenerateRandomNumberFromCurrentDate() =>
        new(ChatRole.User, $"Please generate a random number based from the current date");

    [McpServerPrompt, Description("Nested generation of random numbers")]
    public static ChatMessage GenerateNestedRandomNumbers() =>
        new(ChatRole.User, "Please generate two random numbers. Use these numbers to generate a third random number within the range of the first two.");
}