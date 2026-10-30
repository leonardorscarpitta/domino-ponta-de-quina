using DominoPontaDeQuina.Web.Game;
using static DominoPontaDeQuina.Tests.Fabrica;

namespace DominoPontaDeQuina.Tests;

public class MesaTests
{
    [Theory]
    [InlineData(5, 1)]
    [InlineData(10, 2)]
    [InlineData(15, 3)]
    [InlineData(20, 4)]
    [InlineData(7, 0)]
    [InlineData(12, 0)]
    [InlineData(0, 0)]
    public void CalcularPontos_concede_soma_dividida_por_cinco_somente_em_multiplos(int soma, int esperado) =>
        Assert.Equal(esperado, Mesa.CalcularPontos(soma));

    [Fact]
    public void ObterPontas_tabuleiro_vazio_retorna_nulo() =>
        Assert.Null(Mesa.ObterPontas([]));

    [Fact]
    public void ObterPontas_pedra_unica_usa_seus_dois_valores() =>
        Assert.Equal(new Pontas(6, 1), Mesa.ObterPontas([P(6, 1)]));

    [Fact]
    public void Orientar_vira_pedras_que_chegam_invertidas()
    {
        var cadeia = Mesa.Orientar([P(3, 6), P(1, 6), P(1, 4)]);

        Assert.Equal([P(3, 6), P(6, 1), P(1, 4)], cadeia);
        Assert.Equal(new Pontas(3, 4), Mesa.ObterPontas([P(3, 6), P(1, 6), P(1, 4)]));
    }

    [Fact]
    public void Orientar_mantem_cadeia_ja_orientada()
    {
        var tabuleiro = new[] { P(2, 5), P(5, 5), P(5, 0) };
        Assert.Equal(tabuleiro, Mesa.Orientar(tabuleiro));
    }

    [Fact]
    public void Orientar_corrige_primeira_pedra_invertida()
    {
        var cadeia = Mesa.Orientar([P(4, 2), P(4, 6)]);
        Assert.Equal([P(2, 4), P(4, 6)], cadeia);
    }
}
