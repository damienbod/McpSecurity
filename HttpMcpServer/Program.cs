using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore.Authentication;
using ToolsLibrary.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);

var serverUrl = "https://localhost:5001";
if(!builder.Environment.IsDevelopment())
{
    serverUrl = "https://mcpoauthsecurity-hag0drckepathyb6.westeurope-01.azurewebsites.net";
}

builder.Services.AddAuthentication()
.AddMcp(options =>
{
    options.ResourceMetadata = new()
    {
        Resource = new Uri(serverUrl),
        ResourceDocumentation = new Uri("https://docs.example.com/api/weather"),
        //AuthorizationServers = { new Uri(inMemoryOAuthServerUrl) },
        ScopesSupported = ["mcp:tools", "api://96b0f495-3b65-4c8f-a0c6-c3767c3365ed/access_as_user"],
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

app.MapGet("/health", () => $"MCP server running deployed: UTC: {DateTime.UtcNow}");

app.UseAuthentication();
app.UseAuthorization();

app.MapMcp("/mcp").RequireAuthorization();

app.Run();
