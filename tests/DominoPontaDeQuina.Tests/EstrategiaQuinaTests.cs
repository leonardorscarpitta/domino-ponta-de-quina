using DominoPontaDeQuina.Web.Api;
using DominoPontaDeQuina.Web.Game;
using static DominoPontaDeQuina.Tests.Fabrica;

namespace DominoPontaDeQuina.Tests;

public class EstrategiaQuinaTests
{
    private readonly EstrategiaQuina _estrategia = new();

    [Fact]
    public void Escolhe_a_jogada_que_marca_mais_pontos()
    {
        // Board ends 5 and 5. [5|0] -> 0+5 = 5 (1 pt); [5|5] -> 5+5 = 10 (2 pts).
        var estado = Estado(mao: [P(5, 0), P(5, 5), P(2, 3)], tabuleiro: [P(5, 1), P(1, 5)]);

        var decisao = Assert.IsType<Decisao.Jogar>(_estrategia.Decidir(estado, Eu));

        Assert.Equal(P(5, 5), decisao.Peca);
    }

    [Fact]
    public void Joga_a_abertura_obrigatoria()
    {
        var estado = Estado(mao: [P(1, 2), P(6, 6)], acao: ProximaAcaoPartida.JogarAbertura, abertura: P(6, 6));

        var decisao = Assert.IsType<Decisao.Jogar>(_estrategia.Decidir(estado, Eu));

        Assert.Equal(P(6, 6), decisao.Peca);
    }

    [Fact]
    public void Compra_quando_a_api_pede_compra() =>
        Assert.IsType<Decisao.Comprar>(_estrategia.Decidir(Estado(mao: [P(0, 0)], tabuleiro: [P(5, 6)], acao: ProximaAcaoPartida.Comprar), Eu));

    [Fact]
    public void Passa_somente_quando_a_api_indica() =>
        Assert.IsType<Decisao.Passar>(_estrategia.Decidir(Estado(mao: [P(0, 0)], tabuleiro: [P(5, 6)], acao: ProximaAcaoPartida.Passar), Eu));

    [Fact]
    public void Aguarda_fora_do_proprio_turno() =>
        Assert.IsType<Decisao.Aguardar>(_estrategia.Decidir(Estado(mao: [P(5, 5)], tabuleiro: [P(5, 1)], turno: Outro), Eu));

    [Fact]
    public void Inicia_quando_a_mesa_tem_dois_participantes() =>
        Assert.IsType<Decisao.Iniciar>(_estrategia.Decidir(Estado(situacao: SituacaoPartida.AguardandoInicio, acao: ProximaAcaoPartida.Aguardar), Eu));

    [Fact]
    public void Aguarda_adversario_antes_de_iniciar() =>
        Assert.IsType<Decisao.Aguardar>(_estrategia.Decidir(Estado(situacao: SituacaoPartida.AguardandoInicio, acao: ProximaAcaoPartida.Aguardar, participantes: 1), Eu));

    [Fact]
    public void Nada_a_fazer_com_partida_encerrada() =>
        Assert.IsType<Decisao.Aguardar>(_estrategia.Decidir(Estado(situacao: SituacaoPartida.Encerrada), Eu));

    [Fact]
    public void Entre_jogadas_sem_pontos_evita_deixar_quina_para_o_adversario()
    {
        // Ends 2|1. Both options score 0 for me:
        // [2|6] on the left leaves 6|1, where a [1|4] gives the opponent 10 (2 pts);
        // [1|0] on the right leaves 2|0, where the opponent's best reply is 5 (1 pt).
        var estado = Estado(mao: [P(2, 6), P(1, 0)], tabuleiro: [P(2, 1)]);

        var melhor = _estrategia.MelhorJogada(estado)!;

        Assert.Equal(P(1, 0), melhor.Peca);
    }

    [Fact]
    public void Pedras_desconhecidas_excluem_mao_e_mesa()
    {
        var desconhecidas = EstrategiaQuina.PedrasDesconhecidas([P(0, 0), P(6, 5)], [P(1, 2)]);

        Assert.Equal(25, desconhecidas.Count);
        Assert.DoesNotContain(desconhecidas, p => p.MesmaPedra(P(5, 6)));
    }
}
