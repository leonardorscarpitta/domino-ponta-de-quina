using DominoPontaDeQuina.Web.Api;
using DominoPontaDeQuina.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace DominoPontaDeQuina.Web.Pages;

[EnableRateLimiting("entrar")]
public sealed class EntrarModel(DominoApiClient api, ILogger<EntrarModel> logger) : PageModel
{
    public string? Email { get; private set; }
    public string? Sucesso { get; private set; }
    public string? Erro { get; private set; }

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? RedirectToPage("/Index") : Page();

    public async Task<IActionResult> OnPostSolicitarAsync([FromForm] string? email, CancellationToken ct)
    {
        Email = email?.Trim();
        if (!EmailFiapValido(Email))
        {
            Erro = "Use um e-mail válido terminado em @fiap.com.br.";
            return Page();
        }

        try
        {
            Sucesso = await api.SolicitarTokenAsync(Email!, ct)
                ? "Solicitação enviada. Se o e-mail for elegível, o token chegará em instantes."
                : "A API recusou este e-mail. Use seu endereço @fiap.com.br.";
        }
        catch (DominoApiException ex)
        {
            Erro = ex.MensagemAmigavel;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "API indisponível ao solicitar token");
            Erro = "A API do jogo está indisponível no momento.";
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAutenticarAsync([FromForm] string? email, [FromForm] string? token, CancellationToken ct)
    {
        Email = email?.Trim();
        token = token?.Trim();
        if (token is { Length: > 7 } && token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            token = token[7..].Trim();

        if (!EmailFiapValido(Email) || string.IsNullOrEmpty(token) || token.Length > 512)
        {
            Erro = "Informe o e-mail FIAP e o token recebido.";
            return Page();
        }

        try
        {
            var sessao = await api.ObterSessaoAsync(new ApiCredenciais(token), ct);
            if (string.IsNullOrEmpty(sessao.UsuarioId))
            {
                Erro = "Token não reconhecido pela API.";
                return Page();
            }
            await Sessao.EntrarAsync(HttpContext, token, sessao.UsuarioId, Email!);
            return RedirectToPage("/Perfis");
        }
        catch (DominoApiException ex)
        {
            Erro = ex.NaoAutenticado ? "Token inválido ou revogado. Solicite um novo." : ex.MensagemAmigavel;
            return Page();
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "API indisponível ao autenticar");
            Erro = "A API do jogo está indisponível no momento.";
            return Page();
        }
    }

    internal static bool EmailFiapValido(string? email) =>
        email is { Length: <= 254 }
        && System.Net.Mail.MailAddress.TryCreate(email, out var endereco)
        && endereco.Address == email
        && endereco.Host.Equals("fiap.com.br", StringComparison.OrdinalIgnoreCase);
}
