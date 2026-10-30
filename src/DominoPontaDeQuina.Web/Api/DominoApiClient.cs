using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DominoPontaDeQuina.Web.Api;

public sealed record ApiCredenciais(string Token, string? ChaveAtivacao = null);

public sealed class DominoApiOptions
{
    public const string Secao = "DominoApi";
    public required Uri BaseUrl { get; set; }
    public int TimeoutSegundos { get; set; } = 15;
}

public sealed class DominoApiClient(HttpClient http, ILogger<DominoApiClient> logger)
{
    private const string CabecalhoAtivacao = "X-Jogador-Ativacao";
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<bool> SolicitarTokenAsync(string email, CancellationToken ct = default)
    {
        using var resposta = await http.PostAsJsonAsync("api/identidade/token", new SolicitarTokenRequest(email), Json, ct);
        if (resposta.StatusCode == HttpStatusCode.BadRequest) return false;
        await GarantirSucessoAsync(resposta, ct);
        return true;
    }

    public Task<SessaoResponse> ObterSessaoAsync(ApiCredenciais cred, CancellationToken ct = default) =>
        EnviarAsync<SessaoResponse>(HttpMethod.Get, "api/identidade/sessao", cred, null, ct);

    public async Task RevogarTokenAsync(ApiCredenciais cred, CancellationToken ct = default)
    {
        using var req = Montar(HttpMethod.Post, "api/identidade/tokens/revogacao", cred, null);
        using var resposta = await http.SendAsync(req, ct);
        await GarantirSucessoAsync(resposta, ct);
    }

    public Task<List<PerfilJogador>> ListarJogadoresAsync(ApiCredenciais cred, CancellationToken ct = default) =>
        EnviarAsync<List<PerfilJogador>>(HttpMethod.Get, "api/jogadores", cred, null, ct);

    public Task<PerfilJogador> CriarJogadorAsync(ApiCredenciais cred, string nome, CancellationToken ct = default) =>
        EnviarAsync<PerfilJogador>(HttpMethod.Post, "api/jogadores", cred, new CriarJogadorRequest(nome), ct);

    public Task<PerfilJogador> AtivarJogadorAsync(ApiCredenciais cred, Guid jogadorId, string chaveAtivacao, CancellationToken ct = default) =>
        EnviarAsync<PerfilJogador>(HttpMethod.Post, $"api/jogadores/{jogadorId}/ativar-cliente", cred, new AtivarJogadorRequest(chaveAtivacao), ct);

    public Task<PerfilJogador> DesativarJogadorAsync(ApiCredenciais cred, Guid jogadorId, CancellationToken ct = default) =>
        EnviarAsync<PerfilJogador>(HttpMethod.Delete, $"api/jogadores/{jogadorId}/desativar-cliente", cred, null, ct);

    public Task<List<PartidaDisponivel>> ListarPartidasAsync(ApiCredenciais cred, CancellationToken ct = default) =>
        EnviarAsync<List<PartidaDisponivel>>(HttpMethod.Get, "api/partidas", cred, null, ct);

    public Task<EstadoPartidaParticipante> CriarPartidaAsync(ApiCredenciais cred, CriarPartidaRequest corpo, CancellationToken ct = default) =>
        EnviarAsync<EstadoPartidaParticipante>(HttpMethod.Post, "api/partidas", cred, corpo, ct);

    public Task<ConfirmacaoAcaoPartida> EntrarAsync(ApiCredenciais cred, Guid partidaId, AcaoRequest corpo, CancellationToken ct = default) =>
        EnviarAsync<ConfirmacaoAcaoPartida>(HttpMethod.Post, $"api/partidas/{partidaId}/entrar", cred, corpo, ct);

    public Task<ConfirmacaoAcaoPartida> IniciarAsync(ApiCredenciais cred, Guid partidaId, AcaoRequest corpo, CancellationToken ct = default) =>
        EnviarAsync<ConfirmacaoAcaoPartida>(HttpMethod.Post, $"api/partidas/{partidaId}/iniciar", cred, corpo, ct);

    public Task<ConfirmacaoAcaoPartida> JogarAsync(ApiCredenciais cred, Guid partidaId, JogarRequest corpo, CancellationToken ct = default) =>
        EnviarAsync<ConfirmacaoAcaoPartida>(HttpMethod.Post, $"api/partidas/{partidaId}/jogar", cred, corpo, ct);

    public Task<ConfirmacaoAcaoPartida> ComprarAsync(ApiCredenciais cred, Guid partidaId, AcaoRequest corpo, CancellationToken ct = default) =>
        EnviarAsync<ConfirmacaoAcaoPartida>(HttpMethod.Post, $"api/partidas/{partidaId}/comprar", cred, corpo, ct);

    public Task<ConfirmacaoAcaoPartida> PassarAsync(ApiCredenciais cred, Guid partidaId, AcaoRequest corpo, CancellationToken ct = default) =>
        EnviarAsync<ConfirmacaoAcaoPartida>(HttpMethod.Post, $"api/partidas/{partidaId}/passar", cred, corpo, ct);

    public Task<EstadoPartidaParticipante> ObterEstadoAsync(ApiCredenciais cred, Guid partidaId, Guid jogadorId, CancellationToken ct = default) =>
        EnviarAsync<EstadoPartidaParticipante>(HttpMethod.Get, $"api/partidas/{partidaId}?jogadorId={jogadorId}", cred, null, ct);

    /// <summary>Returns null when the API answers 204 (nothing changed since <paramref name="versaoConhecida"/>).</summary>
    public async Task<EstadoPartidaParticipante?> ObterAtualizacoesAsync(ApiCredenciais cred, Guid partidaId, Guid jogadorId, long? versaoConhecida, CancellationToken ct = default)
    {
        var url = $"api/partidas/{partidaId}/atualizacoes?jogadorId={jogadorId}";
        if (versaoConhecida is { } v) url += $"&versaoConhecida={v}";
        using var req = Montar(HttpMethod.Get, url, cred, null);
        using var resposta = await http.SendAsync(req, ct);
        if (resposta.StatusCode == HttpStatusCode.NoContent) return null;
        await GarantirSucessoAsync(resposta, ct);
        return await LerAsync<EstadoPartidaParticipante>(resposta, ct);
    }

    /// <summary>Public view (no hands) available to any authenticated user, even non-participants.</summary>
    public Task<EstadoPartidaObservador> VisualizarAsync(ApiCredenciais cred, Guid partidaId, CancellationToken ct = default) =>
        EnviarAsync<EstadoPartidaObservador>(HttpMethod.Get, $"api/partidas/{partidaId}/visualizar", cred, null, ct);

    public Task<ResultadoHistoricoPartida> ObterResultadoAsync(ApiCredenciais cred, Guid partidaId, Guid jogadorId, CancellationToken ct = default) =>
        EnviarAsync<ResultadoHistoricoPartida>(HttpMethod.Get, $"api/partidas/{partidaId}/resultado?jogadorId={jogadorId}", cred, null, ct);

    private async Task<T> EnviarAsync<T>(HttpMethod metodo, string url, ApiCredenciais cred, object? corpo, CancellationToken ct)
    {
        using var req = Montar(metodo, url, cred, corpo);
        using var resposta = await http.SendAsync(req, ct);
        await GarantirSucessoAsync(resposta, ct);
        return await LerAsync<T>(resposta, ct);
    }

    private static HttpRequestMessage Montar(HttpMethod metodo, string url, ApiCredenciais cred, object? corpo)
    {
        var req = new HttpRequestMessage(metodo, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cred.Token);
        if (!string.IsNullOrEmpty(cred.ChaveAtivacao))
            req.Headers.TryAddWithoutValidation(CabecalhoAtivacao, cred.ChaveAtivacao);
        if (corpo is not null)
            req.Content = JsonContent.Create(corpo, corpo.GetType(), options: Json);
        return req;
    }

    private static async Task<T> LerAsync<T>(HttpResponseMessage resposta, CancellationToken ct) =>
        await resposta.Content.ReadFromJsonAsync<T>(Json, ct)
        ?? throw new DominoApiException(resposta.StatusCode, "RESPOSTA_VAZIA", null);

    private async Task GarantirSucessoAsync(HttpResponseMessage resposta, CancellationToken ct)
    {
        if (resposta.IsSuccessStatusCode) return;

        RespostaErro? erro = null;
        try
        {
            if (resposta.Content.Headers.ContentLength is not 0)
                erro = await resposta.Content.ReadFromJsonAsync<RespostaErro>(Json, ct);
        }
        catch (JsonException)
        {
            // Framework-level validation errors don't follow the RespostaErro envelope.
        }

        var retryAfter = resposta.Headers.RetryAfter?.Delta;
        logger.LogWarning("API {Metodo} {Url} -> {Status} {Codigo}",
            resposta.RequestMessage?.Method, resposta.RequestMessage?.RequestUri?.AbsolutePath, (int)resposta.StatusCode, erro?.Codigo);
        throw new DominoApiException(resposta.StatusCode, erro?.Codigo, erro?.EstadoAutoritativo, retryAfter);
    }
}
