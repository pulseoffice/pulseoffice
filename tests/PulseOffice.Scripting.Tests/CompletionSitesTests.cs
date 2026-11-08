using PulseOffice.Scripting;
using Xunit;

namespace PulseOffice.Scripting.Tests;

public sealed class CompletionSitesTests
{
    private readonly CompletionSites _sites = new();

    private CompletionSite At(string source, ScriptLanguage language = ScriptLanguage.JavaScript) =>
        _sites.Find(source, new ScriptPosition(1, source.Length + 1), language);

    [Fact]
    public void DopoUnPuntoSiCompletanoIMembri()
    {
        var site = At("app.sheet.Re");
        Assert.Equal(CompletionSiteKind.Member, site.Kind);
        Assert.Equal("app.sheet", site.Path);
    }

    [Fact]
    public void DentroUnaStringaNonSiCompleta() => Assert.Equal(CompletionSiteKind.None, At("var s = \"app.").Kind);

    [Fact]
    public void DentroUnCommentoNonSiCompleta() => Assert.Equal(CompletionSiteKind.None, At("// app.").Kind);

    [Fact]
    public void DopoUnaStringaChiusaSiCompleta() => Assert.Equal(CompletionSiteKind.Global, At("var s = \"x\"; ").Kind);

    [Fact]
    public void DopoUnNumeroNonSiCompleta() => Assert.Equal(CompletionSiteKind.None, At("var x = 3.").Kind);

    [Fact]
    public void DopoUnaChiamataNonSiPropongonoLeRadici() => Assert.Equal(CompletionSiteKind.None, At("sheet.Range().").Kind);

    [Fact]
    public void InPythonIlCancellettoECommento() =>
        Assert.Equal(CompletionSiteKind.None, At("# app.", ScriptLanguage.Python).Kind);

    [Fact]
    public void UnaStringaCheFinisceConUnaBarraRaddoppiataEChiusa() =>
        Assert.Equal(CompletionSiteKind.Global, At("var s = \"a\\\\\"").Kind);

    [Fact]
    public void UnaStringaTriplaApertaNonEChiusa() =>
        Assert.Equal(CompletionSiteKind.None, At("s = \"\"\"app.\"", ScriptLanguage.Python).Kind);

    [Fact]
    public void DopoUnaStringaTriplaChiusaSiCompleta() =>
        Assert.Equal(CompletionSiteKind.Member, At("s = \"\"\"a\"\"\"; app.", ScriptLanguage.Python).Kind);
}

public sealed class ScriptOutputTests
{
    [Fact]
    public void ChiudereUnaCatturaEsternaNonSpegneQuellaInterna()
    {
        var output = new ScriptOutput();
        var outer = new List<string>();
        var inner = new List<string>();

        var first = output.Capture(outer.Add);
        var second = output.Capture(inner.Add);
        first.Dispose();
        output.Write("a");
        second.Dispose();
        output.Write("b");

        Assert.Equal(["a"], inner);
        Assert.Empty(outer);
    }

    [Fact]
    public void LaCatturaInternaRestituisceQuellaEsterna()
    {
        var output = new ScriptOutput();
        var outer = new List<string>();

        using (output.Capture(outer.Add))
        {
            using (output.Capture(_ => { })) output.Write("x");
            output.Write("y");
        }

        Assert.Equal(["y"], outer);
    }

    [Fact]
    public void INumeriConEsponenteSonoUnSoloGettone()
    {
        var tokens = new ScriptTokenizer().Tokenize("1e-5 + 0xFF", ScriptLanguage.JavaScript, new HashSet<string>());

        Assert.Contains(tokens, token => token.Kind == ScriptTokenKind.Number && token.Start == 0 && token.Length == 4);
        Assert.Contains(tokens, token => token.Kind == ScriptTokenKind.Number && token.Start == 7 && token.Length == 4);
    }

}
