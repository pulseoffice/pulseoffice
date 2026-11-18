using PulseOffice.Agent.Hosting;
using PulseOffice.Foundation.Modules;
using PulseOffice.Scripting;
using PulseOffice.Scripting.JavaScript;
using Xunit;

namespace PulseOffice.Agent.Tests;

public sealed class Round192ScriptStopTests
{
    private sealed class StillRunningEngine(Task stopped) : IScriptEngine
    {
        public ScriptLanguage Language => ScriptLanguage.JavaScript;

        public string DisplayName => "Still running";

        public string RuntimeVersion => "1";

        public bool IsSandboxed => true;

        public IReadOnlySet<string> Keywords { get; } = new HashSet<string>();

        public IReadOnlyList<ScriptDiagnostic> Analyze(string source) => [];

        public Task<ScriptResult> ExecuteAsync(string source, ScriptContext context, CancellationToken ct = default) =>
            Task.FromResult(new ScriptResult(false, null, [], TimeSpan.Zero) { Stopped = stopped });

        public IReadOnlyList<CompletionItem> Complete(string source, ScriptPosition position, ScriptApiCatalog api, ModuleKind module) => [];

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class SheetGlobals : IModuleScriptGlobals
    {
        public ModuleKind Module => ModuleKind.Sheets;

        public IReadOnlyDictionary<string, object> Globals { get; } = new Dictionary<string, object>();
    }

    public sealed class Gate
    {
        public ManualResetEventSlim Entered { get; } = new();

        public ManualResetEventSlim Release { get; } = new();

        public void Wait()
        {
            Entered.Set();
            Release.Wait(TimeSpan.FromSeconds(20));
        }
    }

    [Fact]
    public async Task The_agent_call_ends_only_after_the_script_has_really_stopped()
    {
        var stopped = new TaskCompletionSource();
        var scripts = new ModuleAgentScripts([new StillRunningEngine(stopped.Task)], [new SheetGlobals()], new ScriptExecutionPermissions());

        var run = scripts.RunAsync(ModuleKind.Sheets, "1", AgentAccessLevel.Edit, TestContext.Current.CancellationToken);
        await Task.Yield();

        Assert.False(run.IsCompleted);
        stopped.SetResult();
        Assert.False((await run).Succeeded);
    }

    [Fact]
    public async Task A_script_past_its_time_cap_inside_a_host_call_reports_when_it_really_stops()
    {
        var gate = new Gate();
        var engine = new JavaScriptEngine();
        await engine.ExecuteAsync("1", new ScriptContext { Module = ModuleKind.Sheets, Globals = new Dictionary<string, object>() },
            TestContext.Current.CancellationToken);

        var result = await engine.ExecuteAsync("gate.Wait()", new ScriptContext
        {
            Module = ModuleKind.Sheets,
            Globals = new Dictionary<string, object> { ["gate"] = gate },
            Permissions = ScriptPermissions.Sandboxed with { Timeout = TimeSpan.FromMilliseconds(500) },
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.True(gate.Entered.IsSet);
        Assert.False(result.Stopped.IsCompleted);
        gate.Release.Set();
        await result.Stopped.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }
}
