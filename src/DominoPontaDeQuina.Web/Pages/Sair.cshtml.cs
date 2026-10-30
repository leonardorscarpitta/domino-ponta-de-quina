using DominoPontaDeQuina.Web.Api;
using DominoPontaDeQuina.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DominoPontaDeQuina.Web.Pages;

public sealed class SairModel(DominoApiClient api, ILogger<SairModel> logger) : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Index");

    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Entrar");
    }

    /// <summary>Revokes the API token everywhere, not just in this browser.</summary>
    public async Task<IActionResult> OnPostRevogarAsync(CancellationToken ct)
    {
        try
        {
            await api.RevogarTokenAsync(User.Credenciais(), ct);
        }
        catch (Exception ex) when (ex is DominoApiException or HttpRequestException)
        {
            logger.LogWarning(ex, "Falha ao revogar token");
        }
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Entrar");
    }
}
