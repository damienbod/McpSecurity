using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Identity.Web;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text.Json;

namespace McpWebClient.Pages;

[AuthorizeForScopes(ScopeKeySection = "api://96b0f495-3b65-4c8f-a0c6-c3767c3365ed/access_as_user")]
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly LlmPromptService _llmPromptService;
    private readonly IHttpClientFactory _clientFactory;
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly IConfiguration _configuration;

    [BindProperty]
    public string? PromptResults { get; set; }

    [BindProperty]
    [Required]
    public string Prompt { get; set; } = "Please generate a random number with the range of -10 and 10";


    public IndexModel(ILogger<IndexModel> logger,
        IHttpClientFactory clientFactory,
        ITokenAcquisition tokenAcquisition,
        IConfiguration configuration,
        LlmPromptService llmPromptService)
    {
        _clientFactory = clientFactory;
        _tokenAcquisition = tokenAcquisition;
        _configuration = configuration;
        _logger = logger;
        _llmPromptService = llmPromptService;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        // we have an access token
        var accessToken = await _tokenAcquisition
           .GetAccessTokenForUserAsync(["api://96b0f495-3b65-4c8f-a0c6-c3767c3365ed/mcp:tools"]);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            // Something failed. Redisplay the form.
            return await OnGetAsync();
        }

        // we have an access token
        var accessToken = await _tokenAcquisition
           .GetAccessTokenForUserAsync(["api://96b0f495-3b65-4c8f-a0c6-c3767c3365ed/mcp:tools"]);

        await _llmPromptService.Setup(_clientFactory, accessToken);

        //var prompt = "Please generate a random number";
        //var prompt = "Please generate a random number with the range of -10 and 10";
        //var prompt = "Please generate a random number based from the current date";
        //var prompt = "Please generate five random numbers?";
        //var prompt = "Please generate two random numbers. Use these numbers to generate a third random number within the range of the first two.";

        PromptResults = await _llmPromptService.Chat(Prompt);

        // Redisplay the form.
        return await OnGetAsync();
    }

  
}
