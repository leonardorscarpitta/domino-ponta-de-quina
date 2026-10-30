using System.Net;

namespace DominoPontaDeQuina.Web.Api;

public sealed class DominoApiException(HttpStatusCode status, string? codigo, EstadoPartidaParticipante? estadoAutoritativo, TimeSpan? retryAfter = null)
    : Exception($"API respondeu {(int)status} {codigo}")
{
    public HttpStatusCode Status { get; } = status;
    public string? Codigo { get; } = codigo;
    public EstadoPartidaParticipante? EstadoAutoritativo { get; } = estadoAutoritativo;
    public TimeSpan? RetryAfter { get; } = retryAfter;

    public bool NaoAutenticado => Status == HttpStatusCode.Unauthorized;

    public string MensagemAmigavel => Codigo switch
    {
        "NAO_AUTENTICADO" => "Sua sessão expirou ou o token foi revogado. Entre novamente.",
        "SEM_PERMISSAO" => "Este usuário ou perfil não tem permissão para essa ação.",
        "JOGADOR_JA_ATIVO" => "Este perfil já está ativo em outro cliente. Libere-o lá antes de usá-lo aqui.",
        "PARTIDA_INDISPONIVEL" => "Partida não encontrada ou indisponível.",
        "CONFLITO_DE_ESTADO" => "A mesa mudou antes da sua ação. O estado oficial foi recarregado; tente novamente.",
        "ESTADO_INVALIDO" => "A partida não está em um estado que permita essa ação.",
        "FORA_DO_TURNO" => "Não é a sua vez.",
        "PECA_AUSENTE" => "Essa pedra não está na sua mão.",
        "ENCAIXE_INVALIDO" => "Essa pedra não encaixa nessa ponta.",
        "PASSAGEM_NAO_PERMITIDA" => "Você só pode passar quando não há cava nem pedra compatível.",
        "LIMITE_EXCEDIDO" => RetryAfter is { } espera
            ? $"Limite de solicitações atingido. Aguarde {Math.Ceiling(espera.TotalMinutes)} min."
            : "Limite de solicitações atingido. Aguarde alguns minutos.",
        "ENTRADA_INVALIDA" => "Dados inválidos. Revise os campos.",
        _ => Status switch
        {
            HttpStatusCode.Unauthorized => "Sua sessão expirou ou o token foi revogado. Entre novamente.",
            HttpStatusCode.Forbidden => "Sem permissão para essa ação.",
            HttpStatusCode.Conflict => "Conflito com o estado oficial da partida.",
            HttpStatusCode.UnprocessableEntity => "Jogada rejeitada pela API. A partida não avançou.",
            HttpStatusCode.TooManyRequests => "Muitas solicitações. Aguarde alguns minutos.",
            _ => "A API do jogo está indisponível no momento. Tente novamente."
        }
    };
}
