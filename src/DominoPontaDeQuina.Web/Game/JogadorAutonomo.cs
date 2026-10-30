using System.Collections.Concurrent;
using System.Net;
using DominoPontaDeQuina.Web.Api;

namespace DominoPontaDeQuina.Web.Game;

/// <summary>
/// Runs autonomous players on the server, one loop per (match, profile). They keep playing
/// even if the browser tab closes, and stop when the match ends or is turned off.
/// </summary>
public sealed class GerenciadorAutonomo(IServiceScopeFactory escopos, IEstrategiaJogo estrategia, ILogger<GerenciadorAutonomo> logger)
    : IDisposable
{
    private static readonly TimeSpan IntervaloConsulta = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan PausaEntreJogadas = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan PausaAposErro = TimeSpan.FromSeconds(3);

    private readonly ConcurrentDictionary<(Guid Partida, Guid Jogador), CancellationTokenSource> _ativos = new();

    public bool EstaAtivo(Guid partidaId, Guid jogadorId) => _ativos.ContainsKey((partidaId, jogadorId));

    public void Ativar(ApiCredenciais cred, Guid partidaId, Guid jogadorId)
    {
        var cts = new CancellationTokenSource();
        if (!_ativos.TryAdd((partidaId, jogadorId), cts))
        {
            cts.Dispose();
            return;
        }
        _ = Task.Run(() => ExecutarAsync(cred, partidaId, jogadorId, cts));
    }

    public void Desativar(Guid partidaId, Guid jogadorId)
    {
        if (_ativos.TryRemove((partidaId, jogadorId), out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    private async Task ExecutarAsync(ApiCredenciais cred, Guid partidaId, Guid jogadorId, CancellationTokenSource cts)
    {
        var ct = cts.Token;
        using var escopo = escopos.CreateScope();
        var api = escopo.ServiceProvider.GetRequiredService<DominoApiClient>();
        var executor = escopo.ServiceProvider.GetRequiredService<ExecutorAcoes>();
        EstadoPartidaParticipante? estado = null;
        logger.LogInformation("Jogador autônomo iniciado: partida {Partida}, perfil {Jogador}", partidaId, jogadorId);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    estado = await api.ObterAtualizacoesAsync(cred, partidaId, jogadorId, estado?.VersaoEstado, ct) ?? estado;
                    if (estado is null) { await Task.Delay(IntervaloConsulta, ct); continue; }
                    if (estado.Situacao == SituacaoPartida.Encerrada) break;

                    var decisao = estrategia.Decidir(estado, jogadorId);
                    if (decisao is Decisao.Aguardar) { await Task.Delay(IntervaloConsulta, ct); continue; }

                    await Task.Delay(PausaEntreJogadas, ct);
                    var confirmacao = await executor.ExecutarAsync(cred, partidaId, jogadorId, decisao, estado.VersaoEstado, Guid.NewGuid(), ct);
                    logger.LogInformation("Autônomo {Jogador} executou {Decisao} na partida {Partida}", jogadorId, Descrever(decisao), partidaId);
                    estado = confirmacao.Estado ?? estado;
                }
                catch (DominoApiException ex) when (ex.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
                {
                    logger.LogWarning("Autônomo encerrado na partida {Partida}: {Codigo}", partidaId, ex.Codigo);
                    break;
                }
                catch (DominoApiException ex)
                {
                    // 409 brings the authoritative state; anything else forces a full refresh.
                    logger.LogWarning("Autônomo rejeitado na partida {Partida}: {Status} {Codigo}", partidaId, (int)ex.Status, ex.Codigo);
                    estado = ex.EstadoAutoritativo;
                    await Task.Delay(PausaAposErro, ct);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
                {
                    logger.LogWarning(ex, "Falha de rede no autônomo da partida {Partida}", partidaId);
                    await Task.Delay(PausaAposErro, ct);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro inesperado no autônomo da partida {Partida}", partidaId);
        }
        finally
        {
            if (_ativos.TryRemove(new KeyValuePair<(Guid, Guid), CancellationTokenSource>((partidaId, jogadorId), cts))) cts.Dispose();
            logger.LogInformation("Jogador autônomo finalizado: partida {Partida}, perfil {Jogador}", partidaId, jogadorId);
        }
    }

    private static string Descrever(Decisao d) => d switch
    {
        Decisao.Jogar j => $"jogar [{j.Peca.ValorA}|{j.Peca.ValorB}] {j.Lado}",
        _ => d.GetType().Name
    };

    public void Dispose()
    {
        foreach (var cts in _ativos.Values) cts.Cancel();
    }
}
