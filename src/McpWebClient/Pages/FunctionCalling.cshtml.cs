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
    public string SelectedToolModeValue { get; set; } = "Auto";

    public ChatToolMode SelectedToolMode => SelectedToolModeValue switch
    {
        "None" => ChatToolMode.None,
        _ => ChatToolMode.Auto
    };

    [BindProperty]
    public List<PendingFunctionCall> PendingFunctions { get; set; } = new();
    
    [BindProperty]
    public string? ErrorMessage { get; set; }

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
            ErrorMessage = "Model validation failed.";
            _logger.LogWarning("Model state invalid: {errors}", string.Join("; ", ModelState.Values.SelectMany(v => v.Errors)));
            return Page();
        }

        try
        {
            await EnsureChatServiceSetupAsync();
            var userKey = GetUserKey();

            _logger.LogInformation("Processing prompt: {Prompt} with ToolMode: {ToolMode}", Prompt, SelectedToolModeValue);
            var response = await _chatService.BeginChatAsync(userKey, Prompt);
            
            PromptResults = response.FinalAnswer;
            PendingFunctions = response.PendingFunctions ?? new();
            
            _logger.LogInformation("Response received: {FinalAnswer}, PendingFunctions: {Count}", 
                response.FinalAnswer, response.PendingFunctions?.Count ?? 0);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
            _logger.LogError(ex, "Error processing prompt: {Message}", ex.Message);
        }

        return Page();
    }

    public IActionResult OnPostClear()
    {
        _chatService.Clear(GetUserKey());
        PendingFunctions = new();
        PromptResults = null;
        ErrorMessage = null;
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
