using System.Security.Claims;
using System.Text.Json;
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
    private readonly ILogger<SalesAssistantModel> _logger;

    [BindProperty]
    public string Prompt { get; set; } = string.Empty;

    [BindProperty]
    public bool IsNewConversation { get; set; } = true;

    public SalesResponse? SalesData { get; private set; }
    public SalesNotesView? NotesData { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? NotesErrorMessage { get; private set; }

    public SalesAssistantModel(SalesAssistantService salesService, IHttpClientFactory clientFactory,
        ILogger<SalesAssistantModel> logger)
    {
        _salesService = salesService;
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        SalesData = await _salesService.GetCurrentAsync(GetUserKey());
        IsNewConversation = SalesData is null;
        return Page();
    }

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
            SalesData = await _salesService.GetCurrentAsync(GetUserKey());
        }

        return Page();
    }

    public async Task<IActionResult> OnPostApprovalAsync(string approvalId, bool approved)
    {
        string? noteCustomerId = null;
        try
        {
            var userKey = GetUserKey();
            var current = await _salesService.GetCurrentAsync(userKey);
            var pending = current?.PendingApprovals.FirstOrDefault(a => a.Id == approvalId);
            if (approved && pending?.ToolName == "add_customer_follow_up_note")
            {
                using var args = JsonDocument.Parse(pending.Arguments);
                if (args.RootElement.TryGetProperty("customerId", out var customerId))
                    noteCustomerId = customerId.GetString();
            }
            SalesData = await _salesService.RespondToApprovalAsync(userKey, approvalId, approved, _clientFactory);
            IsNewConversation = false;
        }
        catch (MicrosoftIdentityWebChallengeUserException)
        {
            throw;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            SalesData = await _salesService.GetCurrentAsync(GetUserKey());
        }
        if (noteCustomerId is not null && SalesData is not null && SalesData.PendingApprovals.Count == 0)
            await LoadNotesAsync(noteCustomerId);
        return Page();
    }

    public async Task<IActionResult> OnPostNotesAsync(string? customerId)
    {
        SalesData = await _salesService.GetCurrentAsync(GetUserKey());
        IsNewConversation = SalesData is null;
        await LoadNotesAsync(customerId);
        return Page();
    }

    private async Task LoadNotesAsync(string? customerId)
    {
        try
        {
            NotesData = await _salesService.GetNotesAsync(customerId, _clientFactory);
        }
        catch (MicrosoftIdentityWebChallengeUserException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load sales follow-up notes.");
            NotesErrorMessage = $"Could not load follow-up notes: {ex.Message}";
        }
    }

    public async Task<IActionResult> OnPostClearAsync()
    {
        await _salesService.ClearAsync(GetUserKey());
        return RedirectToPage();
    }

    private string GetUserKey()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
            ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")
            ?? throw new InvalidOperationException("The signed-in user has no stable identifier.");
        return $"{claim.Issuer}|{claim.Value}";
    }
}
