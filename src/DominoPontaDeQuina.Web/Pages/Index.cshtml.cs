using DominoPontaDeQuina.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DominoPontaDeQuina.Web.Pages;

public sealed class IndexModel : PageModel
{
    public IActionResult OnGet() => User.JogadorId() is null ? RedirectToPage("/Perfis") : RedirectToPage("/Partidas");
}
