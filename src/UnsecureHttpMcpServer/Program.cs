using ToolsLibrary.Prompts;
using ToolsLibrary.Resources;
using ToolsLibrary.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services
       .AddMcpServer()
       .WithHttpTransport()
       .WithPrompts<PromptExamples>()
       .WithResources<DocumentationResource>()
       .WithTools<RandomNumberTools>()
       .WithTools<SamplingTool>()
       .WithTools<DateTools>();

// Add services to the container.
var app = builder.Build();

// map the mcp endpoit
app.MapMcp("/mcp");

await app.RunAsync();
