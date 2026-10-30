using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace DominoPontaDeQuina.Web.Security;

public sealed class ArmazenamentoOptions
{
    public const string Secao = "Armazenamento";
    public string Diretorio { get; set; } = "data";
}

/// <summary>
/// Keeps each profile's activation key (encrypted at rest with Data Protection) and the
/// matches it joined. The key must survive restarts: the API never expires an activation,
/// so losing it would lock the profile to this client until it is released elsewhere.
/// </summary>
public sealed class CofreAtivacoes
{
    private readonly IDataProtector _protetor;
    private readonly string _arquivo;
    private readonly SemaphoreSlim _trava = new(1, 1);

    public CofreAtivacoes(IDataProtectionProvider provedor, Microsoft.Extensions.Options.IOptions<ArmazenamentoOptions> opcoes)
    {
        _protetor = provedor.CreateProtector("DominoPontaDeQuina.ChaveAtivacao.v1");
        Directory.CreateDirectory(opcoes.Value.Diretorio);
        _arquivo = Path.Combine(opcoes.Value.Diretorio, "ativacoes.json");
    }

    /// <summary>32 random bytes as 64 lowercase hex chars, as the API requires.</summary>
    public static string GerarChave() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    public async Task<string?> ObterChaveAsync(string usuarioId, Guid jogadorId)
    {
        var dados = await LerAsync();
        return dados.TryGetValue(Id(usuarioId, jogadorId), out var registro) && registro.ChaveProtegida is { } protegida
            ? _protetor.Unprotect(protegida)
            : null;
    }

    public async Task<string> ObterOuCriarChaveAsync(string usuarioId, Guid jogadorId)
    {
        await _trava.WaitAsync();
        try
        {
            var dados = await LerSemTravaAsync();
            var id = Id(usuarioId, jogadorId);
            if (dados.TryGetValue(id, out var registro) && registro.ChaveProtegida is { } protegida)
                return _protetor.Unprotect(protegida);

            var chave = GerarChave();
            dados[id] = (registro ?? new Registro()) with { ChaveProtegida = _protetor.Protect(chave) };
            await GravarSemTravaAsync(dados);
            return chave;
        }
        finally { _trava.Release(); }
    }

    public Task RemoverChaveAsync(string usuarioId, Guid jogadorId) =>
        AlterarAsync(usuarioId, jogadorId, r => r with { ChaveProtegida = null });

    public Task RegistrarPartidaAsync(string usuarioId, Guid jogadorId, Guid partidaId) =>
        AlterarAsync(usuarioId, jogadorId, r => r.Partidas.Contains(partidaId)
            ? r
            : r with { Partidas = [partidaId, .. r.Partidas.Take(19)] });

    public async Task<IReadOnlyList<Guid>> ListarPartidasAsync(string usuarioId, Guid jogadorId)
    {
        var dados = await LerAsync();
        return dados.TryGetValue(Id(usuarioId, jogadorId), out var r) ? r.Partidas : [];
    }

    private async Task AlterarAsync(string usuarioId, Guid jogadorId, Func<Registro, Registro> alterar)
    {
        await _trava.WaitAsync();
        try
        {
            var dados = await LerSemTravaAsync();
            var id = Id(usuarioId, jogadorId);
            dados[id] = alterar(dados.GetValueOrDefault(id) ?? new Registro());
            await GravarSemTravaAsync(dados);
        }
        finally { _trava.Release(); }
    }

    private async Task<Dictionary<string, Registro>> LerAsync()
    {
        await _trava.WaitAsync();
        try { return await LerSemTravaAsync(); }
        finally { _trava.Release(); }
    }

    private async Task<Dictionary<string, Registro>> LerSemTravaAsync()
    {
        if (!File.Exists(_arquivo)) return [];
        await using var fluxo = File.OpenRead(_arquivo);
        return await JsonSerializer.DeserializeAsync<Dictionary<string, Registro>>(fluxo) ?? [];
    }

    private async Task GravarSemTravaAsync(Dictionary<string, Registro> dados)
    {
        var temporario = _arquivo + ".tmp";
        await using (var fluxo = File.Create(temporario))
            await JsonSerializer.SerializeAsync(fluxo, dados);
        File.Move(temporario, _arquivo, overwrite: true);
    }

    private static string Id(string usuarioId, Guid jogadorId) => $"{usuarioId}:{jogadorId}";

    private sealed record Registro
    {
        public string? ChaveProtegida { get; init; }
        public List<Guid> Partidas { get; init; } = [];
    }
}
