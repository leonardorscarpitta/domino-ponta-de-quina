using System.Net;
using DominoPontaDeQuina.Web.Api;
using DominoPontaDeQuina.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DominoPontaDeQuina.Web.Pages;

public sealed class PerfisModel(DominoApiClient api, CofreAtivacoes cofre) : PageModel
{
    public IReadOnlyList<PerfilJogador> Perfis { get; private set; } = [];
    public HashSet<Guid> ComChaveLocal { get; } = [];
    [TempData] public string? Sucesso { get; set; }
    [TempData] public string? Erro { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            await CarregarAsync(ct);
        }
        catch (DominoApiException ex) when (ex.NaoAutenticado)
        {
            return await ExpirouAsync();
        }
        catch (DominoApiException ex)
        {
            Erro = ex.MensagemAmigavel;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostCriarAsync([FromForm] string? nome, CancellationToken ct)
    {
        nome = nome?.Trim();
        if (string.IsNullOrEmpty(nome) || nome.Length > 150)
        {
            Erro = "O nome deve ter entre 1 e 150 caracteres.";
            return RedirectToPage();
        }
        return await ExecutarAsync(async () =>
        {
            var perfil = await api.CriarJogadorAsync(User.Credenciais(), nome, ct);
            Sucesso = $"Perfil \"{perfil.Nome}\" criado. Agora ative-o para jogar.";
        });
    }

    public Task<IActionResult> OnPostAtivarAsync([FromForm] Guid jogadorId, CancellationToken ct) =>
        ExecutarAsync(async () =>
        {
            var usuarioId = User.UsuarioId()!;
            var chave = await cofre.ObterOuCriarChaveAsync(usuarioId, jogadorId);
            var perfil = await api.AtivarJogadorAsync(User.Credenciais(), jogadorId, chave, ct);
            await Sessao.SelecionarJogadorAsync(HttpContext, perfil);
            Sucesso = $"Perfil \"{perfil.Nome}\" ativo neste cliente.";
        }, sucesso: "/Partidas");

    public Task<IActionResult> OnPostLiberarAsync([FromForm] Guid jogadorId, CancellationToken ct) =>
        ExecutarAsync(async () =>
        {
            var usuarioId = User.UsuarioId()!;
            var chave = await cofre.ObterChaveAsync(usuarioId, jogadorId);
            if (chave is null)
            {
                Erro = "Este cliente não possui a chave de ativação desse perfil.";
                return;
            }
            var perfil = await api.DesativarJogadorAsync(User.Credenciais(chave), jogadorId, ct);
            await cofre.RemoverChaveAsync(usuarioId, jogadorId);
            if (User.JogadorId() == jogadorId) await Sessao.SelecionarJogadorAsync(HttpContext, null);
            Sucesso = $"Perfil \"{perfil.Nome}\" liberado. Ele pode ser ativado em outro cliente.";
        });

    private async Task CarregarAsync(CancellationToken ct)
    {
        Perfis = await api.ListarJogadoresAsync(User.Credenciais(), ct);
        foreach (var p in Perfis)
            if (await cofre.ObterChaveAsync(User.UsuarioId()!, p.JogadorId) is not null)
                ComChaveLocal.Add(p.JogadorId);
    }

    private async Task<IActionResult> ExecutarAsync(Func<Task> acao, string? sucesso = null)
    {
        try
        {
            await acao();
            return Erro is null && sucesso is not null ? Redirect(sucesso) : RedirectToPage();
        }
        catch (DominoApiException ex) when (ex.NaoAutenticado)
        {
            return await ExpirouAsync();
        }
        catch (DominoApiException ex)
        {
            Erro = ex.Status == HttpStatusCode.Conflict
                ? "Este perfil já está ativo em outro cliente. Libere-o no cliente original e tente de novo."
                : ex.MensagemAmigavel;
            return RedirectToPage();
        }
        catch (HttpRequestException)
        {
            Erro = "A API do jogo está indisponível no momento.";
            return RedirectToPage();
        }
    }

    private async Task<IActionResult> ExpirouAsync()
    {
        await HttpContext.SignOutAsync();
        return RedirectToPage("/Entrar");
    }
}
