using System.Net;
using System.Text;
using System.Text.Json;
using DominoPontaDeQuina.Web.Api;
using Microsoft.Extensions.Logging.Abstractions;
using static DominoPontaDeQuina.Tests.Fabrica;

namespace DominoPontaDeQuina.Tests;

internal sealed class HandlerFalso(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
{
    public List<(HttpRequestMessage Requisicao, string? Corpo)> Recebidas { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var corpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Recebidas.Add((request, corpo));
        return await responder(request);
    }

    public static HttpResponseMessage Json(HttpStatusCode status, object corpo) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(corpo, DominoApiClient.Json), Encoding.UTF8, "application/json") };
}

public class DominoApiClientTests
{
    private static (DominoApiClient Cliente, HandlerFalso Handler) Criar(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new HandlerFalso(r => Task.FromResult(responder(r)));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.teste/") };
        return (new DominoApiClient(http, NullLogger<DominoApiClient>.Instance), handler);
    }

    private static readonly ApiCredenciais Cred = new("token-secreto", new string('a', 64));

    [Fact]
    public async Task Envia_bearer_e_chave_de_ativacao_nas_rotas_do_jogador()
    {
        var (cliente, handler) = Criar(_ => HandlerFalso.Json(HttpStatusCode.OK, Estado()));

        await cliente.ObterEstadoAsync(Cred, Guid.NewGuid(), Eu);

        var req = Assert.Single(handler.Recebidas).Requisicao;
        Assert.Equal("Bearer", req.Headers.Authorization?.Scheme);
        Assert.Equal("token-secreto", req.Headers.Authorization?.Parameter);
        Assert.Equal(new string('a', 64), Assert.Single(req.Headers.GetValues("X-Jogador-Ativacao")));
        Assert.Contains($"jogadorId={Eu}", req.RequestUri!.Query);
    }

    [Fact]
    public async Task Atualizacoes_sem_mudanca_retornam_nulo()
    {
        var (cliente, handler) = Criar(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        var estado = await cliente.ObterAtualizacoesAsync(Cred, Guid.NewGuid(), Eu, 7);

        Assert.Null(estado);
        Assert.Contains("versaoConhecida=7", handler.Recebidas[0].Requisicao.RequestUri!.Query);
    }

    [Fact]
    public async Task Conflito_expoe_estado_autoritativo()
    {
        var autoritativo = Estado(versao: 9);
        var (cliente, _) = Criar(_ => HandlerFalso.Json(HttpStatusCode.Conflict, new RespostaErro("CONFLITO_DE_ESTADO", autoritativo)));

        var ex = await Assert.ThrowsAsync<DominoApiException>(() =>
            cliente.ComprarAsync(Cred, Guid.NewGuid(), new AcaoRequest(3, Guid.NewGuid(), Eu)));

        Assert.Equal(HttpStatusCode.Conflict, ex.Status);
        Assert.Equal("CONFLITO_DE_ESTADO", ex.Codigo);
        Assert.Equal(9, ex.EstadoAutoritativo?.VersaoEstado);
    }

    [Fact]
    public async Task Rejeicao_de_regra_traduz_o_codigo()
    {
        var (cliente, _) = Criar(_ => HandlerFalso.Json(HttpStatusCode.UnprocessableEntity, new RespostaErro("ENCAIXE_INVALIDO", null)));

        var ex = await Assert.ThrowsAsync<DominoApiException>(() =>
            cliente.JogarAsync(Cred, Guid.NewGuid(), new JogarRequest(1, 2, LadoJogavel.Direita, 1, Guid.NewGuid(), Eu)));

        Assert.Equal("Essa pedra não encaixa nessa ponta.", ex.MensagemAmigavel);
    }

    [Fact]
    public async Task Jogar_envia_corpo_no_formato_do_contrato()
    {
        var chave = Guid.NewGuid();
        var (cliente, handler) = Criar(_ => HandlerFalso.Json(HttpStatusCode.OK, new ConfirmacaoAcaoPartida(chave, false, Estado())));

        await cliente.JogarAsync(Cred, Guid.NewGuid(), new JogarRequest(6, 1, LadoJogavel.Esquerda, 2, chave, Eu));

        using var json = JsonDocument.Parse(handler.Recebidas[0].Corpo!);
        var raiz = json.RootElement;
        Assert.Equal(6, raiz.GetProperty("valorA").GetInt32());
        Assert.Equal(1, raiz.GetProperty("valorB").GetInt32());
        Assert.Equal(0, raiz.GetProperty("lado").GetInt32());
        Assert.Equal(2, raiz.GetProperty("versaoEsperada").GetInt64());
        Assert.Equal(chave, raiz.GetProperty("chaveIdempotencia").GetGuid());
        Assert.Equal(Eu, raiz.GetProperty("jogadorId").GetGuid());
    }

    [Fact]
    public async Task Visualizar_le_a_visao_publica_da_partida()
    {
        var partida = Guid.NewGuid();
        var (cliente, handler) = Criar(_ => HandlerFalso.Json(HttpStatusCode.OK,
            new EstadoPartidaObservador(partida, 3, SituacaoPartida.AguardandoInicio, 0, null, [], [], null)));

        var publico = await cliente.VisualizarAsync(Cred, partida);

        Assert.Equal(3, publico.VersaoEstado);
        Assert.Equal($"/api/partidas/{partida}/visualizar", handler.Recebidas[0].Requisicao.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Solicitar_token_com_email_recusado_retorna_falso()
    {
        var (cliente, _) = Criar(_ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("false") });

        Assert.False(await cliente.SolicitarTokenAsync("alguem@gmail.com"));
    }

    [Fact]
    public async Task Limite_de_token_informa_tempo_de_espera()
    {
        var (cliente, _) = Criar(_ =>
        {
            var r = HandlerFalso.Json(HttpStatusCode.TooManyRequests, new RespostaErro("LIMITE_EXCEDIDO", null));
            r.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMinutes(4));
            return r;
        });

        var ex = await Assert.ThrowsAsync<DominoApiException>(() => cliente.SolicitarTokenAsync("rm1@fiap.com.br"));

        Assert.Contains("4 min", ex.MensagemAmigavel);
    }
}
