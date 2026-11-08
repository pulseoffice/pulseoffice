using PulseOffice.Foundation.Modules;
using PulseOffice.Scripting;
using PulseOffice.Scripting.JavaScript;
using PulseOffice.Scripting.Python;
using Xunit;

namespace PulseOffice.Scripting.Tests;

[Collection("scripting")]
public class ScriptEngineTests
{
    public static TheoryData<string> Motori()
    {
        var data = new TheoryData<string>();
        data.Add("javascript");
        data.Add("python");
        return data;
    }

    private static IScriptEngine Engine(string nome) =>
        nome == "javascript" ? new JavaScriptEngine() : new IronPythonScriptEngine();

    private static ScriptContext Contesto(
        Action<string>? output = null, IReadOnlyDictionary<string, object>? globals = null) =>
        new()
        {
            Module = ModuleKind.Sheets,
            Globals = globals ?? new Dictionary<string, object>(),
            OnOutput = output,
            Permissions = ScriptPermissions.Sandboxed with { AllowsUnsandboxedEngines = true },
        };

    [Theory]
    [MemberData(nameof(Motori))]
    public async Task UnaSommaSiEsegue(string motore)
    {
        var engine = Engine(motore);
        var sorgente = motore == "javascript" ? "2 + 3" : "risultato = 2 + 3";

        var result = await engine.ExecuteAsync(
            sorgente, Contesto(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
    }

    [Theory]
    [MemberData(nameof(Motori))]
    public async Task UnErroreDiSintassiNonEsegueNulla(string motore)
    {
        var engine = Engine(motore);
        var sorgente = motore == "javascript" ? "function ( {" : "def (:";

        var result = await engine.ExecuteAsync(
            sorgente, Contesto(), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, d => d.Severity == ScriptDiagnosticSeverity.Error);
    }

    [Theory]
    [MemberData(nameof(Motori))]
    public async Task LaStampaArrivaAChiAscolta(string motore)
    {
        var engine = Engine(motore);
        var raccolto = new List<string>();

        var sorgente = motore == "javascript" ? "print('ciao')" : "print('ciao')";

        var result = await engine.ExecuteAsync(
            sorgente, Contesto(text => raccolto.Add(text)), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.True(
            raccolto.Any(text => text.Contains("ciao", StringComparison.Ordinal)),
            $"motore={motore} raccolto=[{string.Join("|", raccolto)}]");
    }

    [Fact]
    public async Task UnaVariabileGlobaleArrivaAlloScript()
    {
        var engine = new IronPythonScriptEngine();
        var raccolto = new List<string>();

        var result = await engine.ExecuteAsync(
            "print(numero * 2)",
            Contesto(raccolto.Add, new Dictionary<string, object> { ["numero"] = 21 }),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.Contains(raccolto, text => text.Contains("42", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LAnnullamentoDellUtenteNonSiPresentaComeTempoScaduto()
    {
        var engine = new JavaScriptEngine();
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        var result = await engine.ExecuteAsync(
            "while (true) {}",
            new ScriptContext
            {
                Module = ModuleKind.Sheets,
                Globals = new Dictionary<string, object>(),
                Permissions = ScriptPermissions.Sandboxed with { Timeout = TimeSpan.FromSeconds(10) },
            },
            cancel.Token);

        Assert.False(result.Succeeded);
        Assert.Equal(ScriptMessages.CancelledKey, result.Diagnostics[0].MessageKey);
    }

    [Fact]
    public async Task UnAnnullamentoGiaAvvenutoNonLanciaEccezioni()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        var result = await new JavaScriptEngine().ExecuteAsync(
            "1+1",
            new ScriptContext { Module = ModuleKind.Sheets, Globals = new Dictionary<string, object>() },
            cancel.Token);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task IlCicloInfinitoSiFermaDaSolo()
    {
        var engine = new JavaScriptEngine();

        var result = await engine.ExecuteAsync(
            "while (true) {}",
            new ScriptContext
            {
                Module = ModuleKind.Sheets,
                Globals = new Dictionary<string, object>(),
                Permissions = ScriptPermissions.Sandboxed with { Timeout = TimeSpan.FromSeconds(1) },
            },
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ScriptMessages.TimeoutKey, result.Diagnostics[0].MessageKey);
        Assert.Equal(1.0, Assert.Single(result.Diagnostics[0].MessageArguments));
    }

    [Fact]
    public async Task IlCicloInfinitoInPythonSiFermaDaSolo()
    {
        var engine = new IronPythonScriptEngine();

        var result = await engine.ExecuteAsync(
            "while True:\n    pass",
            new ScriptContext
            {
                Module = ModuleKind.Sheets,
                Globals = new Dictionary<string, object>(),
                Permissions = ScriptPermissions.Sandboxed with { Timeout = TimeSpan.FromSeconds(1), AllowsUnsandboxedEngines = true },
            },
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("PY0002", result.Diagnostics[0].Code);
        Assert.Equal(ScriptMessages.TimeoutKey, result.Diagnostics[0].MessageKey);
    }

    [Theory]
    [MemberData(nameof(Motori))]
    public async Task LErroreInEsecuzioneRiportaLaRigaVera(string nome)
    {
        var sorgente = nome == "javascript" ? "var a = 1;\nvar b = 2;\nnonDefinita();" : "a = 1\nb = 2\nnon_definita()";

        var result = await Engine(nome).ExecuteAsync(sorgente, Contesto(), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(3, result.Diagnostics[0].Start.Line);
    }

    [Fact]
    public async Task PythonNonGiraNellaSandboxDelleMacroDelDocumento()
    {
        var result = await new IronPythonScriptEngine().ExecuteAsync(
            "import clr", new ScriptContext { Module = ModuleKind.Sheets, Globals = new Dictionary<string, object>() },
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("PY0003", result.Diagnostics[0].Code);
    }
}

public class PythonOutputDecodingTests
{
    [Fact]
    public void UnFlussoUtf16SiLeggeLoStesso()
    {
        var bytes = System.Text.Encoding.Unicode.GetBytes("ciao");

        Assert.Equal("ciao", IronPythonScriptEngine.Decode(bytes));
    }

    [Fact]
    public void UnFlussoUtf8SiLeggeComeTale()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("però");

        Assert.Equal("però", IronPythonScriptEngine.Decode(bytes));
    }

    [Fact]
    public void IPreamboliVengonoTolti()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(System.Text.Encoding.UTF8.GetBytes("ok"))
            .ToArray();

        Assert.Equal("ok", IronPythonScriptEngine.Decode(bytes));
    }
}
