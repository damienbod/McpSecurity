using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using McpWebClient.AiServices.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.AI;
using Microsoft.Identity.Web;

namespace McpWebClient.Pages;

[AuthorizeForScopes(ScopeKeySection = "McpDemoScope")]
public class FunctionCallingModel : PageModel
{
    private readonly ILogger<FunctionCallingModel> _logger;
    private readonly ChatService _chatService;
    private readonly IHttpClientFactory _clientFactory;

    [BindProperty]
    public string? PromptResults { get; set; }

    [BindProperty]
    [Required]
    public string Prompt { get; set; } = "What is the current date?";

    [BindProperty]
    [Required]
    public ChatToolMode SelectedToolMode { get; set; } = ChatToolMode.Auto;

    public List<PendingFunctionCall> PendingFunctions { get; set; } = new();
    public string? ErrorMessage { get; private set; }

    public FunctionCallingModel(ILogger<FunctionCallingModel> logger,
        IHttpClientFactory clientFactory,
        ChatService chatService)
    {
        _clientFactory = clientFactory;
        _logger = logger;
        _chatService = chatService;
    }

    public IActionResult OnGet()
    {
        return Page();
    }

    private string GetUserKey() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "anonymous";

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return OnGet();
        }

        try
        {
            await EnsureChatServiceSetupAsync();
            var userKey = GetUserKey();

            var response = await _chatService.BeginChatAsync(userKey, Prompt);
            PromptResults = response.FinalAnswer;
            PendingFunctions = response.PendingFunctions;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _logger.LogError(ex, "Error processing prompt");
        }

        return Page();
    }

    public IActionResult OnPostClear()
    {
        _chatService.Clear(GetUserKey());
        return RedirectToPage();
    }

    private async Task EnsureChatServiceSetupAsync()
    {
        _chatService.SetToolMode(SelectedToolMode);
        _chatService.SetFunctionCallingMode(FunctionCallingMode.Local);
        _chatService.SetApprovalMode(ApprovalMode.Auto);
        await _chatService.EnsureSetupAsync(_clientFactory);
    }
}
