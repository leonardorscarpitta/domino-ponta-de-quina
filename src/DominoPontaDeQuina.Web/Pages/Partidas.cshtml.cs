using DominoPontaDeQuina.Web.Api;
using DominoPontaDeQuina.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace DominoPontaDeQuina.Web.Pages;

public sealed class PartidasModel(DominoApiClient api, CofreAtivacoes cofre) : PaginaJogador(cofre)
{
    private static readonly int[] AlvosPermitidos = [50, 100, 150];

    public IReadOnlyList<PartidaDisponivel> Disponiveis { get; private set; } = [];
    public IReadOnlyList<Guid> Minhas { get; private set; } = [];
    [TempData] public string? Sucesso { get; set; }
    [TempData] public string? Erro { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (await CarregarJogadorAsync() is { } redirecionar) return redirecionar;
        Minhas = await Cofre.ListarPartidasAsync(User.UsuarioId()!, JogadorId);
        try
        {
            Disponiveis = await api.ListarPartidasAsync(Credenciais, ct);
        }
        catch (DominoApiException ex) when (ex.NaoAutenticado)
        {
            return await SessaoExpiradaAsync();
        }
        catch (DominoApiException ex)
        {
            Erro = ex.MensagemAmigavel;
        }
        catch (HttpRequestException)
        {
            Erro = "A API do jogo está indisponível no momento.";
        }
        return Page();
    }

    public async Task<IActionResult> OnPostCriarAsync([FromForm] int pontuacaoAlvo, CancellationToken ct)
    {
        if (await CarregarJogadorAsync() is { } redirecionar) return redirecionar;
        if (!AlvosPermitidos.Contains(pontuacaoAlvo))
        {
            Erro = "Escolha uma pontuação alvo válida.";
            return RedirectToPage();
        }
        return await ExecutarAsync(async () =>
        {
            var estado = await api.CriarPartidaAsync(Credenciais, new CriarPartidaRequest(pontuacaoAlvo, JogadorId), ct);
            return estado.PartidaId;
        });
    }

    /// <summary>
    /// Joins as the second participant. Without a known version (join by code or from the table page),
    /// the current one is read from the public observer view first.
    /// </summary>
    public async Task<IActionResult> OnPostEntrarAsync([FromForm] Guid partidaId, [FromForm] long? versao, CancellationToken ct)
    {
        if (await CarregarJogadorAsync() is { } redirecionar) return redirecionar;
        if (partidaId == Guid.Empty)
        {
            Erro = "Informe um código de partida válido.";
            return RedirectToPage();
        }
        if ((await Cofre.ListarPartidasAsync(User.UsuarioId()!, JogadorId)).Contains(partidaId))
            return Redirect($"/Partida/{partidaId}");

        return await ExecutarAsync(async () =>
        {
            var versaoEsperada = versao ?? (await api.VisualizarAsync(Credenciais, partidaId, ct)).VersaoEstado;
            await api.EntrarAsync(Credenciais, partidaId, new AcaoRequest(versaoEsperada, Guid.NewGuid(), JogadorId), ct);
            return partidaId;
        });
    }

    private async Task<IActionResult> ExecutarAsync(Func<Task<Guid>> acao)
    {
        try
        {
            var partidaId = await acao();
            await Cofre.RegistrarPartidaAsync(User.UsuarioId()!, JogadorId, partidaId);
            return Redirect($"/Partida/{partidaId}");
        }
        catch (DominoApiException ex) when (ex.NaoAutenticado)
        {
            return await SessaoExpiradaAsync();
        }
        catch (DominoApiException ex)
        {
            Erro = ex.Codigo == "ESTADO_INVALIDO"
                ? "A API recusou: a mesa já começou ou está cheia, ou o perfil \"" + User.JogadorNome() +
                  "\" provavelmente já está em outra partida em aberto. Termine-a ou use outro perfil."
                : ex.MensagemAmigavel;
            return RedirectToPage();
        }
        catch (HttpRequestException)
        {
            Erro = "A API do jogo está indisponível no momento.";
            return RedirectToPage();
        }
    }
}
