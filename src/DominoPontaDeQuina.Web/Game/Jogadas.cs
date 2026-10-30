using DominoPontaDeQuina.Web.Api;

namespace DominoPontaDeQuina.Web.Game;

public sealed record JogadaPossivel(PecaPartida Peca, LadoJogavel Lado, Pontas PontasResultantes)
{
    public int Pontos => Mesa.CalcularPontos(PontasResultantes.Soma);
}

/// <summary>
/// Client-side preview of the legal moves. The API is still the authority:
/// these are only used to highlight stones and to feed the autonomous player.
/// </summary>
public static class Jogadas
{
    public static IReadOnlyList<JogadaPossivel> Listar(
        IReadOnlyList<PecaPartida>? mao,
        IReadOnlyList<PecaPartida>? tabuleiro,
        PecaPartida? aberturaObrigatoria)
    {
        if (mao is null || mao.Count == 0) return [];

        var pontas = Mesa.ObterPontas(tabuleiro);
        if (pontas is null)
        {
            var candidatas = aberturaObrigatoria is null
                ? mao
                : mao.Where(p => p.MesmaPedra(aberturaObrigatoria));
            return candidatas
                .Select(p => new JogadaPossivel(p, LadoJogavel.Esquerda, new Pontas(p.ValorA, p.ValorB)))
                .ToList();
        }

        var (esquerda, direita) = pontas.Value;
        var jogadas = new List<JogadaPossivel>();
        foreach (var peca in mao)
        {
            if (peca.Contem(esquerda))
                jogadas.Add(new JogadaPossivel(peca, LadoJogavel.Esquerda, new Pontas(OutroLado(peca, esquerda), direita)));
            if (peca.Contem(direita))
                jogadas.Add(new JogadaPossivel(peca, LadoJogavel.Direita, new Pontas(esquerda, OutroLado(peca, direita))));
        }
        return jogadas;
    }

    private static int OutroLado(PecaPartida peca, int encaixe) => peca.ValorA == encaixe ? peca.ValorB : peca.ValorA;
}
