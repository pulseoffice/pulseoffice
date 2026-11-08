using PulseOffice.Foundation.Modules;
using PulseOffice.Scripting.JavaScript;
using Xunit;

namespace PulseOffice.Scripting.Tests;

public sealed class FileProbe(IScriptExecutionPermissions permissions)
{
    public bool Write(string path)
    {
        permissions.Demand(ScriptCapability.FileSystemWrite);
        return path.Length > 0;
    }
}

public sealed class SlowProbe
{
    public int Wait(int milliseconds)
    {
        Thread.Sleep(milliseconds);
        return milliseconds;
    }
}

[Collection("scripting")]
public sealed class JavaScriptWatchdogTests
{
    [Fact]
    public async Task Una_chiamata_che_non_torna_non_trattiene_il_chiamante_oltre_il_tetto_di_tempo()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var result = await new JavaScriptEngine().ExecuteAsync("probe.Wait(9000)", new ScriptContext
        {
            Module = ModuleKind.Sheets,
            Globals = new Dictionary<string, object> { ["probe"] = new SlowProbe() },
            Permissions = ScriptPermissions.Sandboxed with { Timeout = TimeSpan.FromSeconds(1) },
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("JS0002", result.Diagnostics.Single().Code);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(6), watch.Elapsed.ToString());
    }
}

[Collection("scripting")]
public sealed class ScriptExecutionPermissionsTests
{
    private readonly ScriptExecutionPermissions _permissions = new();

    private ScriptContext Context(ScriptPermissions permissions) => new()
    {
        Module = ModuleKind.Sheets,
        Globals = new Dictionary<string, object> { ["page"] = new FileProbe(_permissions) },
        Permissions = permissions with { Timeout = TimeSpan.FromSeconds(5) },
    };

    [Fact]
    public async Task Una_macro_in_sandbox_non_raggiunge_i_file_attraverso_una_facciata()
    {
        var engine = new PermissionScopedScriptEngine(new JavaScriptEngine(), _permissions);

        var result = await engine.ExecuteAsync("page.Write('/tmp/x.pdf')", Context(ScriptPermissions.Sandboxed), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Una_macro_con_permesso_di_scrittura_passa_e_il_permesso_non_sopravvive_all_esecuzione()
    {
        var engine = new PermissionScopedScriptEngine(new JavaScriptEngine(), _permissions);

        var result = await engine.ExecuteAsync("page.Write('/tmp/x.pdf')", Context(ScriptPermissions.Trusted), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.False(_permissions.Current.FileSystemWrite);
    }

    [Fact]
    public void Fuori_da_ogni_esecuzione_vale_la_sandbox()
    {
        Assert.Throws<ScriptPermissionException>(() => _permissions.Demand(ScriptCapability.FileSystemWrite));
        Assert.Throws<ScriptPermissionException>(() => _permissions.Demand(ScriptCapability.Network));
        Assert.Throws<ScriptPermissionException>(() => _permissions.Demand(ScriptCapability.LaunchProcesses));
    }

    [Fact]
    public void Gli_ambiti_annidati_ripristinano_il_precedente()
    {
        using (_permissions.Enter(ScriptPermissions.Trusted))
        {
            using (_permissions.Enter(ScriptPermissions.Sandboxed))
                Assert.False(_permissions.Current.FileSystemWrite);
            Assert.True(_permissions.Current.FileSystemWrite);
        }
        Assert.False(_permissions.Current.FileSystemWrite);
    }
}
