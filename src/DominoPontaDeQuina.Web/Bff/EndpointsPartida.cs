using System.Net;
using System.Security.Claims;
using DominoPontaDeQuina.Web.Api;
using DominoPontaDeQuina.Web.Game;
using DominoPontaDeQuina.Web.Security;
using Microsoft.AspNetCore.Antiforgery;

namespace DominoPontaDeQuina.Web.Bff;

public sealed record ComandoAcao(string Tipo, long Versao, Guid ChaveIdempotencia, int? ValorA, int? ValorB, LadoJogavel? Lado);

public sealed record ComandoAutonomo(bool Ativo);

public sealed record RespostaFalha(string Mensagem, string? Codigo, VisaoMesa? Visao);

/// <summary>
/// Same-origin JSON endpoints used by the table page. The browser never talks to the
/// game API directly: credentials stay on the server.
/// </summary>
public static class EndpointsPartida
{
    public static void MapEndpointsPartida(this WebApplication app)
    {
        var grupo = app.MapGroup("/bff/partidas/{partidaId:guid}").RequireAuthorization();
        grupo.MapGet("/estado", ObterEstadoAsync);
        grupo.MapPost("/acoes", ExecutarAcaoAsync);
        grupo.MapPost("/autonomo", AlternarAutonomoAsync);
    }

    private sealed record Contexto(ApiCredenciais Cred, Guid JogadorId);

    private static async Task<Contexto?> ResolverAsync(ClaimsPrincipal user, CofreAtivacoes cofre)
    {
        if (user.JogadorId() is not { } jogadorId || user.UsuarioId() is not { } usuarioId) return null;
        var chave = await cofre.ObterChaveAsync(usuarioId, jogadorId);
        return chave is null ? null : new Contexto(user.Credenciais(chave), jogadorId);
    }

    private static async Task<IResult> ObterEstadoAsync(
        Guid partidaId, long? versao, ClaimsPrincipal user, CofreAtivacoes cofre, DominoApiClient api,
        GerenciadorAutonomo autonomo, CancellationToken ct)
    {
        if (await ResolverAsync(user, cofre) is not { } ctx) return SemPerfil();
        try
        {
            var estado = await api.ObterAtualizacoesAsync(ctx.Cred, partidaId, ctx.JogadorId, versao, ct);
            if (estado is null) return Results.NoContent();
            return Results.Ok(await MontarVisaoAsync(api, ctx, estado, autonomo, ct));
        }
        catch (DominoApiException ex)
        {
            return Falha(ex, null);
        }
    }

    private static async Task<IResult> ExecutarAcaoAsync(
        Guid partidaId, ComandoAcao comando, HttpContext http, IAntiforgery antiforgery, CofreAtivacoes cofre,
        DominoApiClient api, ExecutorAcoes executor, GerenciadorAutonomo autonomo, CancellationToken ct)
    {
        if (!await antiforgery.IsRequestValidAsync(http)) return AntiforgeryInvalido();
        if (await ResolverAsync(http.User, cofre) is not { } ctx) return SemPerfil();
        if (autonomo.EstaAtivo(partidaId, ctx.JogadorId))
            return Results.Conflict(new RespostaFalha("O jogador autônomo está no controle. Desative-o para jogar.", "AUTONOMO_ATIVO", null));
        if (comando.ChaveIdempotencia == Guid.Empty)
            return Results.BadRequest(new RespostaFalha("Chave de idempotência ausente.", "ENTRADA_INVALIDA", null));

        Decisao? decisao = comando.Tipo switch
        {
            "jogar" when comando is { ValorA: >= 0 and <= 6, ValorB: >= 0 and <= 6, Lado: LadoJogavel.Esquerda or LadoJogavel.Direita } =>
                new Decisao.Jogar(new PecaPartida(comando.ValorA.Value, comando.ValorB.Value), comando.Lado.Value),
            "comprar" => new Decisao.Comprar(),
            "passar" => new Decisao.Passar(),
            "iniciar" => new Decisao.Iniciar(),
            _ => null
        };
        if (decisao is null)
            return Results.BadRequest(new RespostaFalha("Ação inválida.", "ENTRADA_INVALIDA", null));

        try
        {
            var confirmacao = await executor.ExecutarAsync(ctx.Cred, partidaId, ctx.JogadorId, decisao, comando.Versao, comando.ChaveIdempotencia, ct);
            var estado = confirmacao.Estado ?? await api.ObterEstadoAsync(ctx.Cred, partidaId, ctx.JogadorId, ct);
            return Results.Ok(await MontarVisaoAsync(api, ctx, estado, autonomo, ct));
        }
        catch (DominoApiException ex)
        {
            VisaoMesa? visao = null;
            if (ex.EstadoAutoritativo is { } autoritativo)
                visao = await MontarVisaoAsync(api, ctx, autoritativo, autonomo, ct);
            return Falha(ex, visao);
        }
    }

    private static async Task<IResult> AlternarAutonomoAsync(
        Guid partidaId, ComandoAutonomo comando, HttpContext http, IAntiforgery antiforgery, CofreAtivacoes cofre,
        GerenciadorAutonomo autonomo)
    {
        if (!await antiforgery.IsRequestValidAsync(http)) return AntiforgeryInvalido();
        if (await ResolverAsync(http.User, cofre) is not { } ctx) return SemPerfil();

        if (comando.Ativo) autonomo.Ativar(ctx.Cred, partidaId, ctx.JogadorId);
        else autonomo.Desativar(partidaId, ctx.JogadorId);
        return Results.Ok(new { ativo = autonomo.EstaAtivo(partidaId, ctx.JogadorId) });
    }

    private static async Task<VisaoMesa> MontarVisaoAsync(
        DominoApiClient api, Contexto ctx, EstadoPartidaParticipante estado, GerenciadorAutonomo autonomo, CancellationToken ct)
    {
        ResultadoHistoricoPartida? historico = null;
        if (estado.Situacao != SituacaoPartida.AguardandoInicio)
        {
            try { historico = await api.ObterResultadoAsync(ctx.Cred, estado.PartidaId, ctx.JogadorId, ct); }
            catch (DominoApiException) { /* history is cosmetic; the table still renders without it */ }
        }
        return VisaoMesa.Montar(estado, ctx.JogadorId, historico, autonomo.EstaAtivo(estado.PartidaId, ctx.JogadorId));
    }

    private static IResult AntiforgeryInvalido() =>
        Results.BadRequest(new RespostaFalha("Sessão da página expirou. Recarregue a página.", "ANTIFORGERY", null));

    private static IResult SemPerfil() =>
        Results.Json(new RespostaFalha("Selecione e ative um perfil de jogador antes de jogar.", "SEM_PERFIL", null),
            statusCode: StatusCodes.Status412PreconditionFailed);

    private static IResult Falha(DominoApiException ex, VisaoMesa? visao)
    {
        var status = ex.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound
            or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity or HttpStatusCode.TooManyRequests or HttpStatusCode.BadRequest
            ? (int)ex.Status
            : StatusCodes.Status502BadGateway;
        return Results.Json(new RespostaFalha(ex.MensagemAmigavel, ex.Codigo, visao), statusCode: status);
    }
}
