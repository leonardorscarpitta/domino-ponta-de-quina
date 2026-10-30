using DominoPontaDeQuina.Web.Api;
using DominoPontaDeQuina.Web.Game;
using static DominoPontaDeQuina.Tests.Fabrica;

namespace DominoPontaDeQuina.Tests;

public class JogadasTests
{
    [Fact]
    public void Abertura_obrigatoria_restringe_a_pedra_indicada()
    {
        var jogadas = Jogadas.Listar([P(1, 2), P(6, 6), P(3, 4)], [], P(6, 6));

        var unica = Assert.Single(jogadas);
        Assert.Equal(P(6, 6), unica.Peca);
        Assert.Equal(0, unica.Pontos);
    }

    [Fact]
    public void Pedra_que_encaixa_nas_duas_pontas_gera_duas_jogadas()
    {
        var jogadas = Jogadas.Listar([P(3, 5)], [P(3, 1), P(1, 5)], null);

        Assert.Equal(2, jogadas.Count);
        Assert.Contains(jogadas, j => j.Lado == LadoJogavel.Esquerda && j.PontasResultantes == new Pontas(5, 5));
        Assert.Contains(jogadas, j => j.Lado == LadoJogavel.Direita && j.PontasResultantes == new Pontas(3, 3));
    }

    [Fact]
    public void Pontos_consideram_a_soma_das_pontas_apos_a_jogada()
    {
        var jogada = Assert.Single(Jogadas.Listar([P(6, 4)], [P(1, 6)], null));

        Assert.Equal(LadoJogavel.Direita, jogada.Lado);
        Assert.Equal(new Pontas(1, 4), jogada.PontasResultantes);
        Assert.Equal(1, jogada.Pontos);
    }

    [Fact]
    public void Sem_encaixe_nao_ha_jogadas() =>
        Assert.Empty(Jogadas.Listar([P(0, 0), P(2, 2)], [P(5, 6)], null));
}
