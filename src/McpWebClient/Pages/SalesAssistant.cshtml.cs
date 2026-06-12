using System.Security.Claims;
using McpWebClient.AiServices;
using McpWebClient.AiServices.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Identity.Web;

namespace McpWebClient.Pages;

[AuthorizeForScopes(ScopeKeySection = "McpSalesScope")]
public class SalesAssistantModel : PageModel
{
    private readonly SalesAssistantService _salesService;
    private readonly IHttpClientFactory _clientFactory;

    [BindProperty]
    public string Prompt { get; set; } = string.Empty;

    [BindProperty]
    public bool IsNewConversation { get; set; } = true;

    public SalesResponse? SalesData { get; private set; }
    public string? ErrorMessage { get; private set; }

    public SalesAssistantModel(SalesAssistantService salesService, IHttpClientFactory clientFactory)
    {
        _salesService = salesService;
        _clientFactory = clientFactory;
    }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Prompt))
            return Page();

        try
        {
            var userKey = GetUserKey();
            SalesData = IsNewConversation
                ? await _salesService.BeginAsync(userKey, Prompt, _clientFactory)
                : await _salesService.ContinueAsync(userKey, Prompt, _clientFactory);

            IsNewConversation = false;
        }
        catch (MicrosoftIdentityWebChallengeUserException)
        {
            // Let Microsoft.Identity.Web handle incremental consent challenges.
            throw;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        return Page();
    }

    public IActionResult OnPostClear()
    {
        _salesService.Clear(GetUserKey());
        return RedirectToPage();
    }

    private string GetUserKey() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "anonymous";
}
