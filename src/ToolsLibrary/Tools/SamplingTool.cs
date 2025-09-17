using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ToolsLibrary.Tools
{
    public class SamplingTool
    {
        [McpServerTool]
        [Description("Returns a random string")]
        public async Task<string> GetSomeRandomString(IMcpServer mcpServer)
        {
            var result = await mcpServer.SampleAsync(new CreateMessageRequestParams()
            {
                IncludeContext = ContextInclusion.AllServers,
                Messages = new List<SamplingMessage>()
                {
                    new SamplingMessage()
                    {
                        Content = new TextContentBlock(){ Text = "Please generate a random string" },
                        Role = Role.Assistant,
                    },
                }
            });

            return (result.Content as TextContentBlock)?.Text ?? "No response";
        }
    }

}
