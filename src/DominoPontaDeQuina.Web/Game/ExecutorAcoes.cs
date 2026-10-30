using DominoPontaDeQuina.Web.Api;

namespace DominoPontaDeQuina.Web.Game;

/// <summary>
/// Sends one player intention to the API. Network failures are retried with the same
/// idempotency key, so a lost response never turns into a duplicated move.
/// </summary>
public sealed class ExecutorAcoes(DominoApiClient api, ILogger<ExecutorAcoes> logger)
{
    private const int Tentativas = 3;

    public async Task<ConfirmacaoAcaoPartida> ExecutarAsync(
        ApiCredenciais cred, Guid partidaId, Guid jogadorId, Decisao decisao, long versao, Guid chave, CancellationToken ct = default)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                return await EnviarAsync(cred, partidaId, jogadorId, decisao, versao, chave, ct);
            }
            catch (Exception ex) when (tentativa < Tentativas && EhFalhaTransitoria(ex, ct))
            {
                logger.LogWarning(ex, "Falha transitória ao enviar {Decisao}; repetindo com a mesma chave {Chave}", decisao.GetType().Name, chave);
                await Task.Delay(TimeSpan.FromMilliseconds(400 * tentativa), ct);
            }
        }
    }

    private Task<ConfirmacaoAcaoPartida> EnviarAsync(
        ApiCredenciais cred, Guid partidaId, Guid jogadorId, Decisao decisao, long versao, Guid chave, CancellationToken ct)
    {
        var acao = new AcaoRequest(versao, chave, jogadorId);
        return decisao switch
        {
            Decisao.Jogar j => api.JogarAsync(cred, partidaId,
                new JogarRequest(j.Peca.ValorA, j.Peca.ValorB, j.Lado, versao, chave, jogadorId), ct),
            Decisao.Comprar => api.ComprarAsync(cred, partidaId, acao, ct),
            Decisao.Passar => api.PassarAsync(cred, partidaId, acao, ct),
            Decisao.Iniciar => api.IniciarAsync(cred, partidaId, acao, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(decisao), decisao, "Decisão sem ação na API.")
        };
    }

    private static bool EhFalhaTransitoria(Exception ex, CancellationToken ct) => ex switch
    {
        HttpRequestException => true,
        TaskCanceledException when !ct.IsCancellationRequested => true,
        DominoApiException api => (int)api.Status >= 500,
        _ => false
    };
}
