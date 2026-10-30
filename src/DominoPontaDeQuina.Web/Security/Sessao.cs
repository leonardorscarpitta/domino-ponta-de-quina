using System.Security.Claims;
using DominoPontaDeQuina.Web.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace DominoPontaDeQuina.Web.Security;

/// <summary>
/// The API token lives only inside the encrypted, HttpOnly auth cookie; it is never rendered
/// into pages or returned to browser scripts.
/// </summary>
public static class Sessao
{
    private const string ClaimToken = "api_token";
    private const string ClaimUsuario = "usuario_id";
    private const string ClaimJogador = "jogador_id";
    private const string ClaimJogadorNome = "jogador_nome";

    public static string? Token(this ClaimsPrincipal u) => u.FindFirstValue(ClaimToken);
    public static string? UsuarioId(this ClaimsPrincipal u) => u.FindFirstValue(ClaimUsuario);
    public static string? Email(this ClaimsPrincipal u) => u.FindFirstValue(ClaimTypes.Email);
    public static string? JogadorNome(this ClaimsPrincipal u) => u.FindFirstValue(ClaimJogadorNome);

    public static Guid? JogadorId(this ClaimsPrincipal u) =>
        Guid.TryParse(u.FindFirstValue(ClaimJogador), out var id) ? id : null;

    public static ApiCredenciais Credenciais(this ClaimsPrincipal u, string? chaveAtivacao = null) =>
        new(u.Token() ?? throw new InvalidOperationException("Usuário sem token."), chaveAtivacao);

    public static Task EntrarAsync(HttpContext ctx, string token, string usuarioId, string email, PerfilJogador? jogador = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimToken, token),
            new(ClaimUsuario, usuarioId),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, email)
        };
        if (jogador is not null)
        {
            claims.Add(new Claim(ClaimJogador, jogador.JogadorId.ToString()));
            claims.Add(new Claim(ClaimJogadorNome, jogador.Nome ?? "Jogador"));
        }

        var identidade = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidade),
            new AuthenticationProperties { IsPersistent = true });
    }

    public static Task SelecionarJogadorAsync(HttpContext ctx, PerfilJogador? jogador)
    {
        var u = ctx.User;
        return EntrarAsync(ctx, u.Token()!, u.UsuarioId()!, u.Email()!, jogador);
    }
}
