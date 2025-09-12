using ToolsLibrary.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services
       .AddMcpServer()
       .WithHttpTransport()
       .WithTools<RandomNumberTools>()
       .WithTools<DateTools>()
       .WithTools<WeatherTools>();

// Add CORS for HTTP transport support in browsers
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddHttpClient();

// Add services to the container.
var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

// Enable CORS
app.UseCors();

app.MapGet("/health", () => $"MCP server running deployed: UTC: {DateTime.UtcNow}");

app.MapMcp("/mcp");

app.Run();
