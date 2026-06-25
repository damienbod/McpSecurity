using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using McpWebClient.AiServices.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.AI;
using Microsoft.Identity.Web;

namespace McpWebClient.Pages;

[AuthorizeForScopes(ScopeKeySection = "McpDemoScope")]
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly ChatService _chatService;
    private readonly IHttpClientFactory _clientFactory;

    [BindProperty]
    public string? PromptResults { get; set; }

    [BindProperty]
    [Required]
    public string Prompt { get; set; } = "Please generate a random number based from the current date";

    [BindProperty]
    [Required]
    public ApprovalMode SelectedMode { get; set; } = ApprovalMode.Auto;

    public List<PendingFunctionCall> PendingFunctions { get; set; } = new();

    public IndexModel(ILogger<IndexModel> logger,
        IHttpClientFactory clientFactory,
        ITokenAcquisition tokenAcquisition,
        ChatService llmPromptService)
    {
        _clientFactory = clientFactory;
        _logger = logger;
        _chatService = llmPromptService;
    }

    public IActionResult OnGet()
    {
        // No special processing on GET � state is managed via post backs
        return Page();
    }

    private string GetUserKey()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "anonymous";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return OnGet();
        }

        await EnsureChatServiceSetupAsync();

        // Begin a fresh chat with the prompt
        var response = await _chatService.BeginChatAsync(GetUserKey(), Prompt);

        return await GetActionResultFromResponseAsync(response);
    }

    public async Task<IActionResult> OnPostApproveAsync(string functionId)
    {
        await EnsureChatServiceSetupAsync();

        var response = await _chatService.ApproveFunctionAsync(GetUserKey(), functionId);

        return await GetActionResultFromResponseAsync(response);
    }

    public async Task<IActionResult> OnPostDeclineAsync(string functionId)
    {
        await EnsureChatServiceSetupAsync();

        var response = await _chatService.DeclineFunctionAsync(GetUserKey(), functionId);

        return await GetActionResultFromResponseAsync(response);
    }

    private async Task EnsureChatServiceSetupAsync()
    {
        _chatService.SetApprovalMode(SelectedMode);
        // Always uses Confidential OIDC MCP (McpSecure) — token acquired via ITokenAcquisition
        _chatService.SetFunctionCallingMode(FunctionCallingMode.McpSecure);
        _chatService.SetToolMode(ChatToolMode.Auto);
        await _chatService.EnsureSetupAsync(_clientFactory);
    }

    private async Task<IActionResult> GetActionResultFromResponseAsync(PromptResponse response)
    {
        PromptResults = response.FinalAnswer;
        PendingFunctions = response.PendingFunctions;
        return Page();
    }
}
