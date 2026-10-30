using DominoPontaDeQuina.Web.Api;

namespace DominoPontaDeQuina.Tests;

internal static class Fabrica
{
    public static readonly Guid Eu = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Outro = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static PecaPartida P(int a, int b) => new(a, b);

    public static EstadoPartidaParticipante Estado(
        IReadOnlyList<PecaPartida>? mao = null,
        IReadOnlyList<PecaPartida>? tabuleiro = null,
        ProximaAcaoPartida acao = ProximaAcaoPartida.Jogar,
        Guid? turno = null,
        SituacaoPartida situacao = SituacaoPartida.EmAndamento,
        PecaPartida? abertura = null,
        int participantes = 2,
        long versao = 1) =>
        new(Guid.NewGuid(), versao, situacao, 1, turno ?? Eu, mao ?? [], tabuleiro ?? [],
            new[] { new ResumoParticipantePartida(Eu, "Eu", 0, (mao ?? []).Count, (turno ?? Eu) == Eu),
                    new ResumoParticipantePartida(Outro, "Outro", 0, 7, (turno ?? Eu) == Outro) }.Take(participantes).ToList(),
            null, acao, abertura, Guid.NewGuid());
}
