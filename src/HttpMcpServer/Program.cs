using Microsoft.Identity.Web;
using ToolsLibrary.Prompts;
using ToolsLibrary.Resources;
using ToolsLibrary.Tools;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);
var httpMcpServerUrl = builder.Configuration["HttpMcpServerUrl"];

var authority = $"https://login.microsoftonline.com/{builder.Configuration["AzureAd:TenantId"]!}/v2.0";

builder.Services.AddAuthentication()
.AddMcp(options =>
{
    options.ResourceMetadata = new()
    {
        ResourceName = "HttpMcpServer demo server",
        Resource = new Uri($"{httpMcpServerUrl!}/mcp"),
        AuthorizationServers = [new Uri(authority)],
        ResourceDocumentation = new Uri($"{httpMcpServerUrl}/health"),
        ScopesSupported = [builder.Configuration["McpScope"]],
    };
});

builder.Services.AddAuthorization();

builder.Services
       .AddMcpServer()
       .WithHttpTransport()
       .WithPrompts<PromptExamples>()
       .WithResources<DocumentationResource>()
       .WithTools<RandomNumberTools>()
       .WithTools<DateTools>();

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

// change to scp or scope if not using magic namespaces from MS
// The scope must be validate as we want to force only delegated access tokens
// The scope is requires to only allow access tokens intended for this API
builder.Services.AddAuthorizationBuilder()
  .AddPolicy("mcp_tools", policy =>
        policy.RequireClaim("http://schemas.microsoft.com/identity/claims/scope", "mcp:tools"));

// Add services to the container.
var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

// Enable CORS
app.UseCors();

app.MapGet("/health", () => $"Secure MCP server running deployed: UTC: {DateTime.UtcNow}, use /mcp path to use the tools");

app.UseAuthentication();
app.UseAuthorization();

app.MapMcp("/mcp").RequireAuthorization("mcp_tools");

app.Run();
