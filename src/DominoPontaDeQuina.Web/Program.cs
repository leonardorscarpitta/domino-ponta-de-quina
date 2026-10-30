using System.Threading.RateLimiting;
using DominoPontaDeQuina.Web.Api;
using DominoPontaDeQuina.Web.Bff;
using DominoPontaDeQuina.Web.Game;
using DominoPontaDeQuina.Web.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<DominoApiOptions>()
    .Bind(builder.Configuration.GetSection(DominoApiOptions.Secao))
    .Validate(o => o.BaseUrl is { IsAbsoluteUri: true, Scheme: "https" or "http" }, "DominoApi:BaseUrl deve ser uma URL absoluta.")
    .ValidateOnStart();
builder.Services.Configure<ArmazenamentoOptions>(builder.Configuration.GetSection(ArmazenamentoOptions.Secao));

var diretorioDados = builder.Configuration[$"{ArmazenamentoOptions.Secao}:Diretorio"] ?? "data";
builder.Services.AddDataProtection()
    .SetApplicationName("DominoPontaDeQuina")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(diretorioDados, "keys")));

builder.Services.AddHttpClient<DominoApiClient>((sp, http) =>
{
    var opcoes = sp.GetRequiredService<IOptions<DominoApiOptions>>().Value;
    http.BaseAddress = new Uri(opcoes.BaseUrl.ToString().TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(opcoes.TimeoutSegundos);
});

builder.Services.AddSingleton<CofreAtivacoes>();
builder.Services.AddSingleton<IEstrategiaJogo, EstrategiaQuina>();
builder.Services.AddSingleton<GerenciadorAutonomo>();
builder.Services.AddScoped<ExecutorAcoes>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "domino.sessao";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        o.LoginPath = "/Entrar";
        o.LogoutPath = "/Sair";
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
        o.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/bff")) ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            else ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.Name = "domino.af";
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("entrar", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(10) }));
});

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/");
    o.Conventions.AllowAnonymousToPage("/Entrar");
    o.Conventions.AllowAnonymousToPage("/Error");
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data: blob:; " +
                              "connect-src 'self'; font-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    h.XContentTypeOptions = "nosniff";
    h.XFrameOptions = "DENY";
    h["Referrer-Policy"] = "no-referrer";
    h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapEndpointsPartida();

app.Run();

public partial class Program;
