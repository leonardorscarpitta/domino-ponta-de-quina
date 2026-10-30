using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace DominoPontaDeQuina.Tests;

public sealed class IntegracaoHttpTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly string _dados = Path.Combine(Path.GetTempPath(), "domino-it-" + Guid.NewGuid());
    private readonly WebApplicationFactory<Program> _fabrica;

    public IntegracaoHttpTests(WebApplicationFactory<Program> fabrica) =>
        _fabrica = fabrica.WithWebHostBuilder(b =>
        {
            b.UseEnvironment(Environments.Development);
            b.UseSetting("Armazenamento:Diretorio", _dados);
            b.UseSetting("DominoApi:BaseUrl", "http://127.0.0.1:9");
        });

    private HttpClient Cliente() => _fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Theory]
    [InlineData("/Perfis")]
    [InlineData("/Partidas")]
    [InlineData("/Partida/6f1c1d7e-4a43-4a3b-9a4e-3c0a1f0b2d11")]
    public async Task Paginas_do_jogo_exigem_autenticacao(string url)
    {
        var resposta = await Cliente().GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.StartsWith("/Entrar", resposta.Headers.Location!.PathAndQuery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Endpoints_da_mesa_respondem_401_sem_sessao()
    {
        var resposta = await Cliente().GetAsync("/bff/partidas/6f1c1d7e-4a43-4a3b-9a4e-3c0a1f0b2d11/estado");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Tela_de_entrada_envia_cabecalhos_de_seguranca()
    {
        var resposta = await Cliente().GetAsync("/Entrar");

        resposta.EnsureSuccessStatusCode();
        Assert.Contains("script-src 'self'", resposta.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("DENY", resposta.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("nosniff", resposta.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Formulario_sem_antiforgery_e_rejeitado()
    {
        var resposta = await Cliente().PostAsync("/Entrar?handler=Solicitar",
            new FormUrlEncodedContent([new("Email", "rm1@fiap.com.br")]));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Theory]
    [InlineData("bar", "bar")]
    [InlineData("poker", "poker")]
    [InlineData("\"><script>", "poker")]
    public async Task Tema_vem_do_cookie_somente_se_for_conhecido(string cookie, string esperado)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Get, "/Entrar");
        requisicao.Headers.Add("Cookie", $"domino.tema={Uri.EscapeDataString(cookie)}");

        var html = await (await Cliente().SendAsync(requisicao)).Content.ReadAsStringAsync();

        Assert.Contains($"data-tema=\"{esperado}\"", html);
        Assert.DoesNotContain("<script>", html.Replace("<script type=\"module\"", ""));
    }

    [Fact]
    public async Task Producao_recusa_http_e_aceita_https_vindo_do_proxy()
    {
        using var producao = _fabrica.WithWebHostBuilder(b =>
        {
            b.UseEnvironment(Environments.Production);
            b.UseSetting("FORWARDEDHEADERS_ENABLED", "true");
        });
        var cliente = producao.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var http = await cliente.GetAsync("/Entrar");
        var viaProxy = new HttpRequestMessage(HttpMethod.Get, "/Entrar");
        viaProxy.Headers.Add("X-Forwarded-Proto", "https");
        var https = await cliente.SendAsync(viaProxy);

        Assert.Equal(HttpStatusCode.BadRequest, http.StatusCode);
        Assert.Equal(HttpStatusCode.OK, https.StatusCode);
        Assert.Contains("secure", https.Headers.GetValues("Set-Cookie").Single(), StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dados)) Directory.Delete(_dados, recursive: true);
    }
}
