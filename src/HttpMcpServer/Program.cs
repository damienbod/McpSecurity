using Microsoft.Identity.Web;
using ToolsLibrary.Data;
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
        Resource = $"{httpMcpServerUrl!}/mcp",
        AuthorizationServers = [authority],
        ResourceDocumentation = $"{httpMcpServerUrl}/health",
        ScopesSupported = [
            builder.Configuration["McpSalesScope"]!,
            builder.Configuration["McpDemoScope"]!
        ],
    };
});

builder.Services.AddAuthorization();

builder.Services.AddSingleton<SalesDataStore>();
builder.Services.AddTransient<SalesTools>();

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithPrompts<PromptExamples>()
    .WithResources<DocumentationResource>()
    .WithTools<RandomNumberTools>()
    .WithTools<DateTools>()
    .WithTools<SalesTools>();

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
builder.Services.AddHttpContextAccessor();

static bool HasScope(Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext context, string requiredScope)
{
    var scopeClaimValues = context.User
        .FindAll("http://schemas.microsoft.com/identity/claims/scope")
        .Select(c => c.Value)
        .Concat(context.User.FindAll("scp").Select(c => c.Value));

    return scopeClaimValues
        .SelectMany(v => v.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        .Any(s => string.Equals(s, requiredScope, StringComparison.Ordinal));
}

// The scope must be validated to force delegated access tokens intended for this API.
builder.Services.AddAuthorizationBuilder()
  .AddPolicy("mcp_any", policy =>
        policy.RequireAssertion(context => HasScope(context, "mcp:sales") || HasScope(context, "mcp:demo")))
  .AddPolicy("mcp_sales", policy =>
        policy.RequireAssertion(context => HasScope(context, "mcp:sales")))
  .AddPolicy("mcp_demo", policy =>
        policy.RequireAssertion(context => HasScope(context, "mcp:demo")));

// Add services to the container.
var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

// Enable CORS
app.UseCors();

app.MapGet("/health", () => $"Secure MCP server running deployed: UTC: {DateTime.UtcNow}, use /mcp path to use the tools");

app.UseAuthentication();
app.UseAuthorization();

app.MapMcp("/mcp").RequireAuthorization("mcp_any");

app.Run();
