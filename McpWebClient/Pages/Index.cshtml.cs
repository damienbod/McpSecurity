using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Identity.Web;

namespace McpWebClient.Pages;

[AuthorizeForScopes(ScopeKeySection = "api://96b0f495-3b65-4c8f-a0c6-c3767c3365ed/access_as_user")]
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IHttpClientFactory _clientFactory;
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly IConfiguration _configuration;

    public IndexModel(ILogger<IndexModel> logger,
        IHttpClientFactory clientFactory,
        ITokenAcquisition tokenAcquisition,
        IConfiguration configuration)
    {
        _clientFactory = clientFactory;
        _tokenAcquisition = tokenAcquisition;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        var accessToken = await _tokenAcquisition
            .GetAccessTokenForUserAsync(["api://96b0f495-3b65-4c8f-a0c6-c3767c3365ed/access_as_user"]);

        var accessToken2 = await _tokenAcquisition
           .GetAccessTokenForUserAsync(["api://96b0f495-3b65-4c8f-a0c6-c3767c3365ed/mcp:tools"]);


        var t = "";
    }
}
