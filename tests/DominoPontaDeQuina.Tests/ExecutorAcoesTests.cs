using System.Net;
using System.Text.Json;
using DominoPontaDeQuina.Web.Api;
using DominoPontaDeQuina.Web.Game;
using Microsoft.Extensions.Logging.Abstractions;
using static DominoPontaDeQuina.Tests.Fabrica;

namespace DominoPontaDeQuina.Tests;

public class ExecutorAcoesTests
{
    private static readonly ApiCredenciais Cred = new("t", new string('b', 64));

    private static ExecutorAcoes Criar(HandlerFalso handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.teste/") };
        return new ExecutorAcoes(new DominoApiClient(http, NullLogger<DominoApiClient>.Instance), NullLogger<ExecutorAcoes>.Instance);
    }

    [Fact]
    public async Task Falha_de_rede_repete_com_a_mesma_chave_de_idempotencia()
    {
        var chamadas = 0;
        var handler = new HandlerFalso(_ =>
        {
            if (++chamadas == 1) throw new HttpRequestException("conexão caiu");
            return Task.FromResult(HandlerFalso.Json(HttpStatusCode.OK, new ConfirmacaoAcaoPartida(Guid.Empty, true, Estado())));
        });
        var chave = Guid.NewGuid();

        var confirmacao = await Criar(handler).ExecutarAsync(Cred, Guid.NewGuid(), Eu, new Decisao.Comprar(), 4, chave);

        Assert.True(confirmacao.AcaoIdempotente);
        Assert.Equal(2, handler.Recebidas.Count);
        var chaves = handler.Recebidas.Select(r => JsonDocument.Parse(r.Corpo!).RootElement.GetProperty("chaveIdempotencia").GetGuid());
        Assert.All(chaves, c => Assert.Equal(chave, c));
    }

    [Fact]
    public async Task Rejeicao_de_regra_nao_e_repetida()
    {
        var handler = new HandlerFalso(_ => Task.FromResult(
            HandlerFalso.Json(HttpStatusCode.UnprocessableEntity, new RespostaErro("PASSAGEM_NAO_PERMITIDA", null))));

        await Assert.ThrowsAsync<DominoApiException>(() =>
            Criar(handler).ExecutarAsync(Cred, Guid.NewGuid(), Eu, new Decisao.Passar(), 1, Guid.NewGuid()));

        Assert.Single(handler.Recebidas);
    }

    [Theory]
    [InlineData("comprar")]
    [InlineData("passar")]
    [InlineData("iniciar")]
    [InlineData("jogar")]
    public async Task Cada_decisao_usa_a_rota_correspondente(string rota)
    {
        var handler = new HandlerFalso(_ => Task.FromResult(
            HandlerFalso.Json(HttpStatusCode.OK, new ConfirmacaoAcaoPartida(Guid.Empty, false, Estado()))));
        Decisao decisao = rota switch
        {
            "comprar" => new Decisao.Comprar(),
            "passar" => new Decisao.Passar(),
            "iniciar" => new Decisao.Iniciar(),
            _ => new Decisao.Jogar(P(1, 2), LadoJogavel.Direita)
        };

        await Criar(handler).ExecutarAsync(Cred, Guid.NewGuid(), Eu, decisao, 1, Guid.NewGuid());

        Assert.EndsWith($"/{rota}", handler.Recebidas[0].Requisicao.RequestUri!.AbsolutePath);
    }
}
