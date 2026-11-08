using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Foundation.Modules;
using PulseOffice.Scripting.CPython;
using Xunit;

namespace PulseOffice.Scripting.Tests;

public sealed class CPythonHostFixture
{
    public CPythonHost Host { get; } = new(NullLogger<CPythonHost>.Instance);
}

public sealed class CPythonScriptEngineTests(CPythonHostFixture fixture) : IClassFixture<CPythonHostFixture>
{
    private sealed class RefusingConsent : IPythonDownloadConsent
    {
        public int Asked { get; private set; }

        public IProgress<ProvisioningProgress> Progress { get; } = new Progress<ProvisioningProgress>();

        public Task<bool> AllowAsync(PythonDownloadOffer offer, CancellationToken ct = default)
        {
            Asked++;
            return Task.FromResult(false);
        }
    }

    private sealed class NoStandalone : IStandalonePythonStore
    {
        public int Installs { get; private set; }

        public string Root => Path.GetTempPath();

        public PythonRuntimeDescriptor? Installed() => null;

        public async Task<PythonRuntimeDescriptor?> InstallAsync(IPythonDownloadConsent consent, CancellationToken ct = default)
        {
            Installs++;
            await consent.AllowAsync(new PythonDownloadOffer("3.13", "example", 0, Root), ct);
            return null;
        }
    }

    private sealed class FixedLocator(PythonRuntimeDescriptor? runtime) : IPythonRuntimeLocator
    {
        public PythonRuntimeDescriptor? Locate() => runtime;
    }

    private readonly ScriptPermissions _trusted = ScriptPermissions.Sandboxed with { AllowsUnsandboxedEngines = true, Timeout = TimeSpan.FromSeconds(2) };

    private CPythonScriptEngine Engine(IPythonRuntimeLocator locator, NoStandalone? store = null, RefusingConsent? consent = null, ICPythonHost? host = null) =>
        new(host ?? fixture.Host, locator, store ?? new NoStandalone(), consent ?? new RefusingConsent(), NullLogger<CPythonScriptEngine>.Instance);

    private PythonRuntimeDescriptor? Installed() => new PythonDiscovery().FindBest();

    private ScriptContext Context(Action<string>? output = null, ScriptPermissions? permissions = null) => new()
    {
        Module = ModuleKind.Sheets,
        Globals = new Dictionary<string, object> { ["factor"] = 6 },
        Permissions = permissions ?? _trusted,
        OnOutput = output,
    };

    [Fact]
    public async Task Fuori_dal_sandbox_non_parte_come_IronPython()
    {
        var result = await Engine(new FixedLocator(null)).ExecuteAsync("print(1)", Context(permissions: ScriptPermissions.Sandboxed), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("PY0003", result.Diagnostics.Single().Code);
    }

    [Fact]
    public async Task Senza_Python_chiede_il_consenso_e_se_rifiutato_non_scarica_nulla()
    {
        var store = new NoStandalone();
        var consent = new RefusingConsent();

        var result = await Engine(new FixedLocator(null), store, consent, new CPythonHost(NullLogger<CPythonHost>.Instance)).ExecuteAsync("print(1)", Context(), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("PY0005", result.Diagnostics.Single().Code);
        Assert.Equal(1, consent.Asked);
    }

    [Fact]
    public async Task Con_un_Python_installato_esegue_legge_le_globali_e_raccoglie_l_output()
    {
        if (Installed() is not { } runtime) return;
        var output = new List<string>();

        var result = await Engine(new FixedLocator(runtime)).ExecuteAsync("import math\nprint(factor * 7, math.sqrt(16))", Context(output.Add), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("42 4.0\n", string.Concat(output));
    }

    [Fact]
    public async Task Con_un_Python_installato_errori_e_sintassi_hanno_la_riga()
    {
        if (Installed() is not { } runtime) return;
        var engine = Engine(new FixedLocator(runtime));

        var failed = await engine.ExecuteAsync("x = 1\ny = x / 0\n", Context(), TestContext.Current.CancellationToken);
        Assert.False(failed.Succeeded);
        Assert.Contains("ZeroDivisionError", failed.Diagnostics.Single().Message, StringComparison.Ordinal);
        Assert.Equal(2, failed.Diagnostics.Single().Start.Line);

        var syntax = engine.Analyze("x = 1\nif x\n  pass\n");
        Assert.Equal(2, syntax.Single().Start.Line);
    }

    [Fact]
    public async Task Con_un_Python_installato_il_tetto_di_tempo_ferma_un_ciclo_infinito()
    {
        if (Installed() is not { } runtime) return;

        var result = await Engine(new FixedLocator(runtime)).ExecuteAsync("while True:\n    pass\n", Context(), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("PY0002", result.Diagnostics.Single().Code);
    }

    [Fact]
    public async Task Con_un_Python_installato_il_tetto_di_tempo_ferma_anche_chi_inghiotte_l_interruzione()
    {
        if (Installed() is not { } runtime) return;
        var engine = Engine(new FixedLocator(runtime));

        var result = await engine.ExecuteAsync(
            "while True:\n    try:\n        while True:\n            pass\n    except BaseException:\n        pass\n",
            Context(), TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("PY0002", result.Diagnostics.Single().Code);

        var after = await engine.ExecuteAsync("print(factor)", Context(), TestContext.Current.CancellationToken);
        Assert.True(after.Succeeded, string.Join("; ", after.Diagnostics.Select(d => d.Message)));
    }
}
