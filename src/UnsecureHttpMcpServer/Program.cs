using ModelContextProtocol.AspNetCore;
using ToolsLibrary.Data;
using ToolsLibrary.Prompts;
using ToolsLibrary.Resources;
using ToolsLibrary.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SalesDataStore>();

builder.Services
       .AddMcpServer()
       .WithHttpTransport(options =>
       {
           // 2026-07-28 clients are served statelessly (no Mcp-Session-Id), older initialize clients still get sessions
           options.SessionMode = HttpServerSessionMode.StatefulForInitializeClients;
       })
       .WithPrompts<PromptExamples>()
       .WithResources<DocumentationResource>()
       .WithTools<RandomNumberTools>()
       .WithTools<DateTools>()
       .WithTools<SalesTools>();

// Add services to the container.
var app = builder.Build();

// map the mcp endpoit
app.MapMcp("/mcp");

await app.RunAsync();
