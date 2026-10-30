using DominoPontaDeQuina.Web.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace DominoPontaDeQuina.Tests;

public sealed class CofreAtivacoesTests : IDisposable
{
    private readonly string _diretorio = Path.Combine(Path.GetTempPath(), "cofre-" + Guid.NewGuid());
    private readonly IDataProtectionProvider _provedor;

    public CofreAtivacoesTests() =>
        _provedor = DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(_diretorio, "keys")));

    private CofreAtivacoes Criar() =>
        new(_provedor, Options.Create(new ArmazenamentoOptions { Diretorio = _diretorio }));

    [Fact]
    public void Chave_gerada_tem_64_hexadecimais_aleatorios()
    {
        var a = CofreAtivacoes.GerarChave();
        var b = CofreAtivacoes.GerarChave();

        Assert.Matches("^[0-9a-f]{64}$", a);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public async Task Chave_e_reaproveitada_e_sobrevive_a_reinicio()
    {
        var jogador = Guid.NewGuid();
        var primeira = await Criar().ObterOuCriarChaveAsync("usuario", jogador);

        var aposReinicio = await Criar().ObterOuCriarChaveAsync("usuario", jogador);

        Assert.Equal(primeira, aposReinicio);
    }

    [Fact]
    public async Task Chave_nao_fica_em_texto_puro_no_disco()
    {
        var chave = await Criar().ObterOuCriarChaveAsync("usuario", Guid.NewGuid());

        var conteudo = await File.ReadAllTextAsync(Path.Combine(_diretorio, "ativacoes.json"));

        Assert.DoesNotContain(chave, conteudo);
    }

    [Fact]
    public async Task Chaves_sao_isoladas_por_usuario()
    {
        var jogador = Guid.NewGuid();
        var cofre = Criar();
        await cofre.ObterOuCriarChaveAsync("usuario-a", jogador);

        Assert.Null(await cofre.ObterChaveAsync("usuario-b", jogador));
    }

    [Fact]
    public async Task Remover_chave_preserva_partidas_registradas()
    {
        var jogador = Guid.NewGuid();
        var partida = Guid.NewGuid();
        var cofre = Criar();
        await cofre.ObterOuCriarChaveAsync("u", jogador);
        await cofre.RegistrarPartidaAsync("u", jogador, partida);

        await cofre.RemoverChaveAsync("u", jogador);

        Assert.Null(await cofre.ObterChaveAsync("u", jogador));
        Assert.Equal([partida], await cofre.ListarPartidasAsync("u", jogador));
    }

    public void Dispose()
    {
        if (Directory.Exists(_diretorio)) Directory.Delete(_diretorio, recursive: true);
    }
}
