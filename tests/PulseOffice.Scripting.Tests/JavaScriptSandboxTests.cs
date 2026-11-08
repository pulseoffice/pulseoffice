using PulseOffice.Foundation.Modules;
using PulseOffice.Scripting.JavaScript;
using Xunit;

namespace PulseOffice.Scripting.Tests;

public sealed class SandboxProbe
{
    public string Name => "probe";

    public double Twice(double value) => value * 2;
}

[Collection("scripting")]
public sealed class JavaScriptSandboxTests
{
    private readonly JavaScriptEngine _engine = new();

    private ScriptContext Sandboxed(TimeSpan? timeout = null, long? memory = null) => new()
    {
        Module = ModuleKind.Sheets,
        Globals = new Dictionary<string, object> { ["sheet"] = new SandboxProbe() },
        Permissions = ScriptPermissions.Sandboxed with
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(5),
            MaxMemoryBytes = memory ?? ScriptPermissions.Sandboxed.MaxMemoryBytes,
        },
    };

    public static TheoryData<string> Escapes() =>
    [
        "System.IO.File.ReadAllText('/etc/passwd')",
        "importNamespace('System.IO').File.ReadAllText('/etc/passwd')",
        "require('fs').readFileSync('/etc/passwd')",
        "fetch('http://127.0.0.1/')",
        "new XMLHttpRequest()",
        "process.env.HOME",
        "Function('return this')().process.exit(1)",
        "eval('1+1')",
        "(function(){}).constructor('return 1')()",
        "sheet.GetType().Assembly.Location",
        "sheet.GetType().GetMethod('Twice').Invoke(sheet, [2])",
        "sheet.constructor.constructor('return 1')()",
        "clrHelper.typeOf(sheet)",
        "host.type('System.Environment')",
        "Object.getPrototypeOf(sheet).GetType()",
    ];

    [Theory]
    [MemberData(nameof(Escapes))]
    public async Task A_document_macro_cannot_reach_files_network_processes_environment_or_reflection(string escape)
    {
        var result = await _engine.ExecuteAsync(escape, Sandboxed(), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded, $"«{escape}» succeeded with {result.Value}");
    }

    [Fact]
    public async Task The_exposed_facade_keeps_working_inside_the_sandbox()
    {
        var result = await _engine.ExecuteAsync("sheet.Twice(21) + sheet.Name.length", Sandboxed(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(47d, Convert.ToDouble(result.Value, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task An_infinite_loop_stops_at_the_time_ceiling()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _engine.ExecuteAsync("for(;;){}", Sandboxed(TimeSpan.FromSeconds(1)), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("JS0002", result.Diagnostics[0].Code);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData("var a = []; for(;;) a.push('xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx' + a.length);")]
    [InlineData("var s = 'x'; for(;;) s = s + s;")]
    [InlineData("new Array(1e9).fill(1)")]
    [InlineData("new ArrayBuffer(2147483647)")]
    [InlineData("new ArrayBuffer(1e9); for(;;){}")]
    public async Task A_huge_allocation_stops_at_the_memory_ceiling(string source)
    {
        var result = await _engine.ExecuteAsync(source, Sandboxed(TimeSpan.FromSeconds(20), 32L * 1024 * 1024), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics[0].Code, new[] { "JS0003", "JS0100", "JS0005" });
        Assert.True(GC.GetTotalMemory(false) < 2L * 1024 * 1024 * 1024);
    }

    [Fact]
    public async Task Deep_recursion_stops_without_overflowing_the_stack()
    {
        var result = await _engine.ExecuteAsync("function f(n){ return f(n+1) + 1; } f(0)", Sandboxed(), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("JS0004", result.Diagnostics[0].Code);
    }
    [Theory]
    [InlineData("new ArrayBuffer(1e9)")]
    [InlineData("new Uint8Array(1e9)")]
    [InlineData("new Float64Array(2e8)")]
    [InlineData("Uint8Array.from({ length: 1e9 })")]
    [InlineData("new ArrayBuffer(8, { maxByteLength: 1e9 }).resize(1e9)")]
    [InlineData("new SharedArrayBuffer(1e9)")]
    [InlineData("'x'.repeat(1e9)")]
    [InlineData("'x'.padEnd(1e9)")]
    [InlineData("new Array(1e9).join('x')")]
    public async Task A_single_huge_allocation_is_refused_before_it_happens(string source)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        var total = GC.GetTotalAllocatedBytes(precise: false);

        var result = await _engine.ExecuteAsync(source, Sandboxed(TimeSpan.FromSeconds(20), 32L * 1024 * 1024), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.True(GC.GetTotalAllocatedBytes(precise: false) - total < 256L * 1024 * 1024, $"{GC.GetTotalAllocatedBytes(precise: false) - total} bytes allocated");
        _ = before;
    }

    [Fact]
    public async Task Catastrophic_regex_backtracking_stops_at_the_time_ceiling()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _engine.ExecuteAsync("/^(a+)+$/.test('a'.repeat(64) + 'b')", Sandboxed(TimeSpan.FromSeconds(1)), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"{watch.Elapsed}");
    }

    [Theory]
    [InlineData("print.Method.DeclaringType.Assembly.Location")]
    [InlineData("console.log.Method.Name")]
    [InlineData("print.Target")]
    [InlineData("console.log.DynamicInvoke")]
    public async Task Host_delegates_do_not_expose_reflection(string escape)
    {
        var context = Sandboxed() with { OnOutput = _ => { } };

        var result = await _engine.ExecuteAsync(escape, context, TestContext.Current.CancellationToken);

        Assert.True(!result.Succeeded || result.Value is null, $"«{escape}» succeeded with {result.Value}");
    }

    [Fact]
    public async Task Prototype_pollution_does_not_reach_host_objects()
    {
        var result = await _engine.ExecuteAsync(
            "Object.prototype.Twice = () => 999; Object.prototype.Name = 'evil'; sheet.Twice(1) + sheet.Name", Sandboxed(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal("2probe", result.Value?.ToString());
    }
}
