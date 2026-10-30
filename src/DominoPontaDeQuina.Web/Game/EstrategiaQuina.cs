using DominoPontaDeQuina.Web.Api;

namespace DominoPontaDeQuina.Web.Game;

public abstract record Decisao
{
    public sealed record Jogar(PecaPartida Peca, LadoJogavel Lado) : Decisao;
    public sealed record Comprar : Decisao;
    public sealed record Passar : Decisao;
    public sealed record Iniciar : Decisao;
    public sealed record Aguardar : Decisao;
}

public interface IEstrategiaJogo
{
    Decisao Decidir(EstadoPartidaParticipante estado, Guid meuJogadorId);
}

/// <summary>
/// Greedy player: takes the move with the most points now, discounted by the best
/// reply the opponent could make with any stone it might hold. Ties go to heavier
/// stones (fewer pips left in hand if the board locks) and doubles (harder to place later).
/// </summary>
public sealed class EstrategiaQuina : IEstrategiaJogo
{
    private const double PesoAmeaca = 0.5;
    private const double PesoPedraPesada = 0.02;
    private const double BonusDupla = 0.1;

    private static readonly IReadOnlyList<PecaPartida> Conjunto =
        [.. from a in Enumerable.Range(0, 7) from b in Enumerable.Range(a, 7 - a) select new PecaPartida(a, b)];

    public Decisao Decidir(EstadoPartidaParticipante estado, Guid meuJogadorId)
    {
        if (estado.Situacao == SituacaoPartida.AguardandoInicio)
            return (estado.Participantes?.Count ?? 0) >= 2 ? new Decisao.Iniciar() : new Decisao.Aguardar();

        if (estado.Situacao != SituacaoPartida.EmAndamento || estado.JogadorDoTurnoId != meuJogadorId)
            return new Decisao.Aguardar();

        switch (estado.ProximaAcao)
        {
            case ProximaAcaoPartida.JogarAbertura:
            case ProximaAcaoPartida.Jogar:
                var melhor = MelhorJogada(estado);
                if (melhor is not null) return new Decisao.Jogar(melhor.Peca, melhor.Lado);
                return new Decisao.Comprar();
            case ProximaAcaoPartida.Comprar:
                return new Decisao.Comprar();
            case ProximaAcaoPartida.Passar:
                return new Decisao.Passar();
            default:
                return new Decisao.Aguardar();
        }
    }

    internal JogadaPossivel? MelhorJogada(EstadoPartidaParticipante estado)
    {
        var jogadas = Jogadas.Listar(estado.MinhaMao, estado.Tabuleiro, estado.PedraAberturaObrigatoria);
        if (jogadas.Count == 0) return null;

        var desconhecidas = PedrasDesconhecidas(estado.MinhaMao, estado.Tabuleiro);
        return jogadas
            .OrderByDescending(j => Avaliar(j, desconhecidas))
            .ThenBy(j => j.Lado)
            .First();
    }

    internal static double Avaliar(JogadaPossivel jogada, IReadOnlyList<PecaPartida> desconhecidas)
    {
        var ameaca = MelhorResposta(jogada.PontasResultantes, desconhecidas.Where(p => !p.MesmaPedra(jogada.Peca)));
        return jogada.Pontos
               - PesoAmeaca * ameaca
               + PesoPedraPesada * jogada.Peca.Soma
               + (jogada.Peca.EhDupla ? BonusDupla : 0);
    }

    private static int MelhorResposta(Pontas pontas, IEnumerable<PecaPartida> pedras)
    {
        var melhor = 0;
        foreach (var p in pedras)
        {
            if (p.Contem(pontas.Esquerda))
                melhor = Math.Max(melhor, Mesa.CalcularPontos((p.ValorA == pontas.Esquerda ? p.ValorB : p.ValorA) + pontas.Direita));
            if (p.Contem(pontas.Direita))
                melhor = Math.Max(melhor, Mesa.CalcularPontos(pontas.Esquerda + (p.ValorA == pontas.Direita ? p.ValorB : p.ValorA)));
        }
        return melhor;
    }

    internal static IReadOnlyList<PecaPartida> PedrasDesconhecidas(IReadOnlyList<PecaPartida>? mao, IReadOnlyList<PecaPartida>? tabuleiro)
    {
        var vistas = (mao ?? []).Concat(tabuleiro ?? []).ToList();
        return Conjunto.Where(p => !vistas.Any(v => v.MesmaPedra(p))).ToList();
    }
}
