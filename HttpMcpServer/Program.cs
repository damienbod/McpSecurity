using Microsoft.Identity.Web;
using ToolsLibrary.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);

var httpMcpServerUrl = "https://localhost:5001";
if (!builder.Environment.IsDevelopment())
{
    httpMcpServerUrl = "https://mcpoauthsecurity-hag0drckepathyb6.westeurope-01.azurewebsites.net";
}

builder.Services.AddAuthentication()
.AddMcp(options =>
{
    options.ResourceMetadata = new()
    {
        Resource = new Uri(httpMcpServerUrl),
        ResourceDocumentation = new Uri("https://mcpoauthsecurity-hag0drckepathyb6.westeurope-01.azurewebsites.net/health"),
        //AuthorizationServers = { new Uri(inMemoryOAuthServerUrl) },
        ScopesSupported = ["mcp:tools"],
    };
});

builder.Services.AddAuthorization();

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

app.MapGet("/health", () => $"Secure MCP server running deployed: UTC: {DateTime.UtcNow}, use /mcp path to use the tools");

app.UseAuthentication();
app.UseAuthorization();

app.MapMcp("/mcp").RequireAuthorization();

app.Run();
