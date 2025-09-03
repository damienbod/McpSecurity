using ModelContextProtocol.Server;
using System.ComponentModel;

[McpServerToolType]
public static class DemoTool
{
  [McpServerTool, Description("Echoes the message back to the client.")]
  public static string Echo(string message) => $"Hello from C#: {message}";

  [McpServerTool, Description("Echoes in reverse the message sent by the client.")]
  public static string ReverseEcho(string message) => new string(message.Reverse().ToArray());

  [McpServerTool, Description("Give the current date in UTC")]
  public static string GetCurrentDate() => DateTime.UtcNow.ToString();

  
}
