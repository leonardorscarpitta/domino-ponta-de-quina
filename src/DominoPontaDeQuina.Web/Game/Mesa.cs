using DominoPontaDeQuina.Web.Api;

namespace DominoPontaDeQuina.Web.Game;

public readonly record struct Pontas(int Esquerda, int Direita)
{
    public int Soma => Esquerda + Direita;
}

/// <summary>Reads the board sent by the API as a left-to-right chain.</summary>
public static class Mesa
{
    /// <summary>
    /// Returns the chain with every stone oriented so that ValorB of one stone touches ValorA of the next.
    /// The API may or may not send stones already oriented, so each one is flipped when needed.
    /// </summary>
    public static IReadOnlyList<PecaPartida> Orientar(IReadOnlyList<PecaPartida>? tabuleiro)
    {
        if (tabuleiro is null || tabuleiro.Count == 0) return [];
        if (tabuleiro.Count == 1) return [tabuleiro[0]];

        var resultado = new List<PecaPartida>(tabuleiro.Count);
        var primeira = tabuleiro[0];
        var segunda = tabuleiro[1];
        resultado.Add(segunda.Contem(primeira.ValorB) ? primeira : primeira.Invertida());

        for (var i = 1; i < tabuleiro.Count; i++)
        {
            var encaixe = resultado[^1].ValorB;
            var peca = tabuleiro[i];
            resultado.Add(peca.ValorA == encaixe ? peca : peca.Invertida());
        }

        return resultado;
    }

    public static Pontas? ObterPontas(IReadOnlyList<PecaPartida>? tabuleiro)
    {
        var cadeia = Orientar(tabuleiro);
        if (cadeia.Count == 0) return null;
        return new Pontas(cadeia[0].ValorA, cadeia[^1].ValorB);
    }

    /// <summary>Points scored by the ends sum: sum / 5 when divisible by 5, otherwise zero.</summary>
    public static int CalcularPontos(int somaPontas) =>
        somaPontas > 0 && somaPontas % 5 == 0 ? somaPontas / 5 : 0;
}
