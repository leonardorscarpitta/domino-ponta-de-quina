using DominoPontaDeQuina.Web.Api;
using DominoPontaDeQuina.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DominoPontaDeQuina.Web.Pages;

/// <summary>Base for pages that act as the selected, activated player profile.</summary>
public abstract class PaginaJogador(CofreAtivacoes cofre) : PageModel
{
    protected CofreAtivacoes Cofre { get; } = cofre;
    protected Guid JogadorId { get; private set; }
    protected ApiCredenciais Credenciais { get; private set; } = null!;

    /// <summary>Returns a redirect when there is no usable profile, otherwise null.</summary>
    protected async Task<IActionResult?> CarregarJogadorAsync()
    {
        if (User.JogadorId() is not { } jogadorId) return RedirectToPage("/Perfis");
        var chave = await Cofre.ObterChaveAsync(User.UsuarioId()!, jogadorId);
        if (chave is null)
        {
            await Sessao.SelecionarJogadorAsync(HttpContext, null);
            return RedirectToPage("/Perfis");
        }
        JogadorId = jogadorId;
        Credenciais = User.Credenciais(chave);
        return null;
    }

    protected async Task<IActionResult> SessaoExpiradaAsync()
    {
        await HttpContext.SignOutAsync();
        return RedirectToPage("/Entrar");
    }
}
