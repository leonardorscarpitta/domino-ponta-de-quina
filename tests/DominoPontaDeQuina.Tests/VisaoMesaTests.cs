using DominoPontaDeQuina.Web.Api;
using DominoPontaDeQuina.Web.Game;
using DominoPontaDeQuina.Web.Pages;
using static DominoPontaDeQuina.Tests.Fabrica;

namespace DominoPontaDeQuina.Tests;

public class VisaoMesaTests
{
    private static EventoHistoricoPartida Jogada(PecaPartida p, int segundos) =>
        new(Guid.NewGuid(), Eu, p, LadoJogavel.Direita, false, 0, DateTimeOffset.UnixEpoch.AddSeconds(segundos));

    [Fact]
    public void Abertura_e_a_n_esima_jogada_mais_recente()
    {
        var cadeia = Mesa.Orientar([P(1, 6), P(6, 6), P(6, 2)]);
        var historico = new ResultadoHistoricoPartida(Guid.Empty, SituacaoPartida.EmAndamento, 5, null, [],
            [Jogada(P(3, 3), 1), Jogada(P(6, 6), 10), Jogada(P(6, 2), 11), Jogada(P(1, 6), 12)]);

        Assert.Equal(1, VisaoMesa.LocalizarAbertura(cadeia, historico));
    }

    [Fact]
    public void Sem_historico_usa_o_meio_da_cadeia() =>
        Assert.Equal(1, VisaoMesa.LocalizarAbertura(Mesa.Orientar([P(1, 6), P(6, 6), P(6, 2)]), null));

    [Fact]
    public void Visao_nao_expoe_jogadas_fora_do_turno_e_calcula_cava()
    {
        var estado = Estado(mao: [P(6, 1), P(2, 2)], tabuleiro: [P(1, 3)], turno: Outro);

        var visao = VisaoMesa.Montar(estado, Eu, null, autonomo: false);

        Assert.False(visao.MinhaVez);
        Assert.Empty(visao.Jogadas);
        Assert.Equal(28 - 1 - 2 - 7, visao.Cava);
        Assert.Equal("Outro", visao.Oponente?.Nome);
    }

    [Fact]
    public void Visao_lista_jogadas_com_pontos_no_meu_turno()
    {
        // Ends 0|5: [5|0] on the left leaves 5|5 (2 pts); on the right leaves 0|0 (0 pts).
        var estado = Estado(mao: [P(5, 0), P(3, 3)], tabuleiro: [P(0, 2), P(2, 5)]);

        var visao = VisaoMesa.Montar(estado, Eu, null, autonomo: false);

        Assert.Equal(2, visao.Jogadas.Count);
        Assert.Contains(visao.Jogadas, j => j is { ValorA: 5, ValorB: 0, Lado: LadoJogavel.Esquerda, Pontos: 2 });
        Assert.Contains(visao.Jogadas, j => j is { ValorA: 5, ValorB: 0, Lado: LadoJogavel.Direita, Pontos: 0 });
    }

    [Theory]
    [InlineData("rm555460@fiap.com.br", true)]
    [InlineData("RM1@FIAP.COM.BR", true)]
    [InlineData("aluno@gmail.com", false)]
    [InlineData("aluno@fiap.com.br.evil.com", false)]
    [InlineData("Nome <aluno@fiap.com.br>", false)]
    [InlineData("", false)]
    public void Valida_somente_email_fiap(string email, bool valido) =>
        Assert.Equal(valido, EntrarModel.EmailFiapValido(email));
}
