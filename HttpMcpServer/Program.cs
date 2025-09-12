using ToolsLibrary.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services
       .AddMcpServer()
       .WithHttpTransport(o => o.Stateless = true)
       .WithTools<RandomNumberTools>()
       .WithTools<DateTools>()
       .WithTools<WeatherTools>();

builder.Services.AddHttpClient();

// Add services to the container.
var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.MapGet("/ping", () => $"MCP server running UTC: {DateTime.UtcNow}");

app.MapMcp("/mcp");

app.Run();
