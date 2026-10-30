using DominoPontaDeQuina.Web.Api;

namespace DominoPontaDeQuina.Web.Game;

public sealed record VisaoJogador(Guid JogadorId, string Nome, int Pontuacao, int Pedras, bool EhVez);

public sealed record VisaoPeca(int ValorA, int ValorB);

public sealed record VisaoJogada(int ValorA, int ValorB, LadoJogavel Lado, int Pontos);

public sealed record VisaoEvento(string Jogador, bool SouEu, string Texto, int Pontos);

/// <summary>What the table page needs, already filtered to the requesting player's view.</summary>
public sealed record VisaoMesa(
    Guid PartidaId,
    long Versao,
    SituacaoPartida Situacao,
    int Rodada,
    ProximaAcaoPartida ProximaAcao,
    bool MinhaVez,
    VisaoJogador? Eu,
    VisaoJogador? Oponente,
    IReadOnlyList<VisaoPeca> Mao,
    IReadOnlyList<VisaoPeca> Cadeia,
    int IndiceAbertura,
    Pontas? Pontas,
    IReadOnlyList<VisaoJogada> Jogadas,
    int Cava,
    VisaoPeca? AberturaObrigatoria,
    Guid? VencedorId,
    IReadOnlyList<VisaoEvento> Eventos,
    bool Autonomo)
{
    private const int TotalPedras = 28;
    private const int MaxEventos = 14;

    public static VisaoMesa Montar(EstadoPartidaParticipante estado, Guid meuId, ResultadoHistoricoPartida? historico, bool autonomo)
    {
        var participantes = estado.Participantes ?? [];
        var nomes = participantes.ToDictionary(p => p.JogadorId, p => p.Nome ?? "Jogador");
        var eu = participantes.FirstOrDefault(p => p.JogadorId == meuId);
        var oponente = participantes.FirstOrDefault(p => p.JogadorId != meuId);
        var cadeia = Mesa.Orientar(estado.Tabuleiro);
        var minhaVez = estado.JogadorDoTurnoId == meuId && estado.Situacao == SituacaoPartida.EmAndamento;

        var jogadas = minhaVez && estado.ProximaAcao is ProximaAcaoPartida.Jogar or ProximaAcaoPartida.JogarAbertura
            ? Game.Jogadas.Listar(estado.MinhaMao, estado.Tabuleiro, estado.PedraAberturaObrigatoria)
                .Select(j => new VisaoJogada(j.Peca.ValorA, j.Peca.ValorB, j.Lado, j.Pontos)).ToList()
            : [];

        var cava = estado.Situacao == SituacaoPartida.EmAndamento
            ? Math.Max(0, TotalPedras - cadeia.Count - participantes.Sum(p => p.QuantidadePedrasNaMao))
            : 0;

        return new VisaoMesa(
            estado.PartidaId,
            estado.VersaoEstado,
            estado.Situacao,
            estado.NumeroRodada,
            estado.ProximaAcao,
            minhaVez,
            eu is null ? null : Converter(eu),
            oponente is null ? null : Converter(oponente),
            (estado.MinhaMao ?? []).Select(p => new VisaoPeca(p.ValorA, p.ValorB)).ToList(),
            cadeia.Select(p => new VisaoPeca(p.ValorA, p.ValorB)).ToList(),
            LocalizarAbertura(cadeia, historico),
            Mesa.ObterPontas(estado.Tabuleiro),
            jogadas,
            cava,
            estado.PedraAberturaObrigatoria is { } a ? new VisaoPeca(a.ValorA, a.ValorB) : null,
            estado.VencedorId,
            DescreverEventos(historico, meuId, nomes),
            autonomo);
    }

    /// <summary>
    /// Index of this round's opening stone in the chain, so the 3D layout grows from it.
    /// Every play adds exactly one stone, so the opening is the n-th most recent play.
    /// </summary>
    internal static int LocalizarAbertura(IReadOnlyList<PecaPartida> cadeia, ResultadoHistoricoPartida? historico)
    {
        if (cadeia.Count == 0) return 0;
        var fallback = cadeia.Count / 2;

        var jogadas = (historico?.Eventos ?? [])
            .Where(e => e.PecaJogada is not null)
            .OrderBy(e => e.RecebidoEmUtc)
            .ToList();
        if (jogadas.Count < cadeia.Count) return fallback;

        var abertura = jogadas[^cadeia.Count].PecaJogada!;
        for (var i = 0; i < cadeia.Count; i++)
            if (cadeia[i].MesmaPedra(abertura)) return i;
        return fallback;
    }

    private static IReadOnlyList<VisaoEvento> DescreverEventos(ResultadoHistoricoPartida? historico, Guid meuId, Dictionary<Guid, string> nomes) =>
        (historico?.Eventos ?? [])
            .OrderByDescending(e => e.RecebidoEmUtc)
            .Take(MaxEventos)
            .Select(e => new VisaoEvento(
                e.JogadorId == meuId ? "Você" : nomes.GetValueOrDefault(e.JogadorId, "Oponente"),
                e.JogadorId == meuId,
                e switch
                {
                    { PecaJogada: { } p } => $"jogou [{p.ValorA}|{p.ValorB}] na {(e.Lado == LadoJogavel.Direita ? "direita" : "esquerda")}",
                    { PassouVez: true } => "passou a vez",
                    _ => "comprou uma pedra"
                },
                e.PontosConcedidos))
            .ToList();

    private static VisaoJogador Converter(ResumoParticipantePartida p) =>
        new(p.JogadorId, p.Nome ?? "Jogador", p.Pontuacao, p.QuantidadePedrasNaMao, p.EhVez);
}
