using System.Text.Json;
using System.Text.Json.Serialization;
using DominoPontaDeQuina.Web.Api;
using DominoPontaDeQuina.Web.Game;
using DominoPontaDeQuina.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace DominoPontaDeQuina.Web.Pages;

public sealed class PartidaModel(DominoApiClient api, CofreAtivacoes cofre, GerenciadorAutonomo autonomo) : PaginaJogador(cofre)
{
    internal static readonly JsonSerializerOptions JsonVisao = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public VisaoMesa? Visao { get; private set; }
    /// <summary>Set when this profile is not in the match yet but can still join it.</summary>
    public EstadoPartidaObservador? Convite { get; private set; }
    public string EstadoJson { get; private set; } = "{}";
    public string? Erro { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        if (id is null) return RedirectToPage("/Partidas");
        if (await CarregarJogadorAsync() is { } redirecionar) return redirecionar;

        try
        {
            var estado = await api.ObterEstadoAsync(Credenciais, id.Value, JogadorId, ct);
            ResultadoHistoricoPartida? historico = null;
            if (estado.Situacao != SituacaoPartida.AguardandoInicio)
            {
                try { historico = await api.ObterResultadoAsync(Credenciais, id.Value, JogadorId, ct); }
                catch (DominoApiException) { }
            }

            Visao = VisaoMesa.Montar(estado, JogadorId, historico, autonomo.EstaAtivo(id.Value, JogadorId));
            EstadoJson = JsonSerializer.Serialize(Visao, JsonVisao);
            await Cofre.RegistrarPartidaAsync(User.UsuarioId()!, JogadorId, id.Value);
        }
        catch (DominoApiException ex) when (ex.NaoAutenticado)
        {
            return await SessaoExpiradaAsync();
        }
        catch (DominoApiException ex) when (ex.Status == System.Net.HttpStatusCode.NotFound)
        {
            await PrepararConviteAsync(id.Value, ct);
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

    private async Task PrepararConviteAsync(Guid partidaId, CancellationToken ct)
    {
        try
        {
            var publico = await api.VisualizarAsync(Credenciais, partidaId, ct);
            if (publico.Situacao == SituacaoPartida.AguardandoInicio && (publico.Participantes?.Count ?? 0) < 2)
                Convite = publico;
            else
                Erro = "Você não participa desta partida e ela já não aceita novos jogadores.";
        }
        catch (DominoApiException)
        {
            Erro = "Partida não encontrada. Confira o código.";
        }
    }
}
