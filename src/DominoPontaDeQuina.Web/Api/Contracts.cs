namespace DominoPontaDeQuina.Web.Api;

// Mirrors the external API contract (swagger v1). JSON is camelCase with numeric enums.

public enum SituacaoPartida
{
    AguardandoInicio = 0,
    EmAndamento = 1,
    Encerrada = 2
}

public enum ProximaAcaoPartida
{
    Aguardar = 0,
    JogarAbertura = 1,
    Jogar = 2,
    Comprar = 3,
    Passar = 4,
    Encerrar = 5
}

public enum LadoJogavel
{
    Esquerda = 0,
    Direita = 1
}

public sealed record PecaPartida(int ValorA, int ValorB)
{
    public bool EhDupla => ValorA == ValorB;
    public int Soma => ValorA + ValorB;
    public bool Contem(int valor) => ValorA == valor || ValorB == valor;
    public bool MesmaPedra(PecaPartida outra) =>
        (ValorA == outra.ValorA && ValorB == outra.ValorB) || (ValorA == outra.ValorB && ValorB == outra.ValorA);
    public PecaPartida Invertida() => new(ValorB, ValorA);
}

public sealed record SolicitarTokenRequest(string Email);

public sealed record SessaoResponse(string? UsuarioId);

public sealed record PerfilJogador(Guid JogadorId, string? Nome, bool Ativo, DateTimeOffset? AtivadoEmUtc);

public sealed record CriarJogadorRequest(string Nome);

public sealed record AtivarJogadorRequest(string ChaveAtivacao);

public sealed record PartidaDisponivel(Guid PartidaId, int PontuacaoAlvo, long VersaoEstado);

public sealed record CriarPartidaRequest(int PontuacaoAlvo, Guid JogadorId);

public sealed record AcaoRequest(long VersaoEsperada, Guid ChaveIdempotencia, Guid JogadorId);

public sealed record JogarRequest(int ValorA, int ValorB, LadoJogavel Lado, long VersaoEsperada, Guid ChaveIdempotencia, Guid JogadorId);

public sealed record ResumoParticipantePartida(Guid JogadorId, string? Nome, int Pontuacao, int QuantidadePedrasNaMao, bool EhVez);

public sealed record EstadoPartidaParticipante(
    Guid PartidaId,
    long VersaoEstado,
    SituacaoPartida Situacao,
    int NumeroRodada,
    Guid? JogadorDoTurnoId,
    IReadOnlyList<PecaPartida>? MinhaMao,
    IReadOnlyList<PecaPartida>? Tabuleiro,
    IReadOnlyList<ResumoParticipantePartida>? Participantes,
    Guid? VencedorId,
    ProximaAcaoPartida ProximaAcao,
    PecaPartida? PedraAberturaObrigatoria,
    Guid ParticipacaoId);

public sealed record ConfirmacaoAcaoPartida(Guid ChaveIdempotencia, bool AcaoIdempotente, EstadoPartidaParticipante? Estado);

public sealed record EventoHistoricoPartida(
    Guid EventoId,
    Guid JogadorId,
    PecaPartida? PecaJogada,
    LadoJogavel? Lado,
    bool PassouVez,
    int PontosConcedidos,
    DateTimeOffset RecebidoEmUtc);

public sealed record ResultadoHistoricoPartida(
    Guid PartidaId,
    SituacaoPartida Situacao,
    long VersaoEstado,
    Guid? VencedorId,
    IReadOnlyList<ResumoParticipantePartida>? Participantes,
    IReadOnlyList<EventoHistoricoPartida>? Eventos);

public sealed record RespostaErro(string? Codigo, EstadoPartidaParticipante? EstadoAutoritativo);
