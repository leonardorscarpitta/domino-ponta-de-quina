namespace DominoPontaDeQuina.Web.Ui;

/// <summary>Visual themes. The choice is a plain, non-sensitive preference cookie, validated against this list.</summary>
public static class Temas
{
    public const string Cookie = "domino.tema";
    public const string Padrao = "poker";

    public static readonly IReadOnlyDictionary<string, string> Disponiveis = new Dictionary<string, string>
    {
        ["poker"] = "Mesa de Poker",
        ["bar"] = "Mesa de Bar"
    };

    public static string Resolver(string? valor) =>
        valor is not null && Disponiveis.ContainsKey(valor) ? valor : Padrao;
}
