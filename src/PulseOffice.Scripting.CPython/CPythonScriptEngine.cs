using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PulseOffice.Foundation.Modules;
using Python.Runtime;

namespace PulseOffice.Scripting.CPython;

public sealed class CPythonScriptEngine(
    ICPythonHost host,
    IPythonRuntimeLocator locator,
    IStandalonePythonStore standalone,
    IPythonDownloadConsent consent,
    ILogger<CPythonScriptEngine> logger) : IScriptEngine
{
    private const string Capture = """
        import sys as __pulse_sys, io as __pulse_io
        __pulse_buffer = __pulse_io.StringIO()
        __pulse_saved = (__pulse_sys.stdout, __pulse_sys.stderr)
        __pulse_sys.stdout = __pulse_sys.stderr = __pulse_buffer
        __pulse_ticks = [0]
        def __pulse_trace(frame, event, arg):
            __pulse_ticks[0] += 1
            if __pulse_ticks[0] % 512 == 0 and __pulse_stop.IsCancellationRequested:
                raise KeyboardInterrupt
            return __pulse_trace
        __pulse_sys.settrace(__pulse_trace)
        """;

    private const string Release = """
        __pulse_sys.settrace(None)
        __pulse_sys.stdout, __pulse_sys.stderr = __pulse_saved
        """;

    private readonly PythonLanguage _language = new();

    public ScriptLanguage Language => ScriptLanguage.Python;

    public string DisplayName => "Python (CPython)";

    public string RuntimeVersion => host.Active?.Version.ToString() ?? "not started";

    public bool IsSandboxed => false;

    public IReadOnlySet<string> Keywords => _language.Keywords;

    public IReadOnlyList<ScriptDiagnostic> Analyze(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (host.Active is null) return [];

        using (Py.GIL())
        {
            try
            {
                using var builtins = Py.Import("builtins");
                using var compiled = builtins.InvokeMethod("compile", new PyString(source), new PyString("<macro>"), new PyString("exec"));
                return [];
            }
            catch (PythonException failure)
            {
                var position = Position(failure);
                return [ScriptMessages.Syntax(failure.Message, position, "PY0001")];
            }
        }
    }

    public async Task<ScriptResult> ExecuteAsync(string source, ScriptContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Permissions.AllowsUnsandboxedEngines)
            return Failure(ScriptMessages.PythonSandbox(), "PY0003", TimeSpan.Zero);

        if (!await EnsureStartedAsync(ct).ConfigureAwait(false))
            return Failure(ScriptMessages.PythonUnavailable(), "PY0005", TimeSpan.Zero);

        var syntax = Analyze(source);
        if (syntax.Any(diagnostic => diagnostic.Severity == ScriptDiagnosticSeverity.Error))
            return new ScriptResult(false, null, syntax, TimeSpan.Zero);

        var running = Task.Run(() => Run(source, context, ct), CancellationToken.None);
        var limit = context.Permissions.Timeout + TimeSpan.FromSeconds(2);
        if (await Task.WhenAny(running, Task.Delay(limit, CancellationToken.None)).ConfigureAwait(false) == running)
            return await running.ConfigureAwait(false);

        logger.LogWarning("A CPython macro ignored the interruption after {Timeout} and keeps running in the background", context.Permissions.Timeout);
        return Failure(ScriptMessages.Timeout(context.Permissions.Timeout), "PY0002", limit);
    }

    public IReadOnlyList<CompletionItem> Complete(string source, ScriptPosition position, ScriptApiCatalog api, ModuleKind module) =>
        _language.Complete(source, position, api, module);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<bool> EnsureStartedAsync(CancellationToken ct)
    {
        if (host.Active is not null) return true;

        var runtime = locator.Locate();
        if (runtime is null)
        {
            try
            {
                runtime = await standalone.InstallAsync(consent, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
            {
                logger.LogError(ex, "The standalone Python could not be installed");
                return false;
            }
        }

        return runtime is not null && host.Start(runtime);
    }

    private ScriptResult Run(string source, ScriptContext context, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(context.Permissions.Timeout);

        using (Py.GIL())
        {
            using var scope = Py.CreateScope();
            foreach (var (name, value) in context.Globals) scope.Set(name, value);
            scope.Set("__pulse_stop", timeout.Token);

            var interrupter = new RunInterrupter(PythonEngine.GetPythonThreadID());
            using var registration = timeout.Token.Register(() => _ = Task.Run(interrupter.InterruptAsync));

            scope.Exec(Capture);
            try
            {
                scope.Exec(source);
                interrupter.Finish();
                return new ScriptResult(true, null, [], Elapsed(stopwatch));
            }
            catch (PythonException failure) when (timeout.IsCancellationRequested && failure.Type.Name == "KeyboardInterrupt")
            {
                interrupter.Finish();
                return ct.IsCancellationRequested
                    ? Failure(ScriptMessages.Cancelled(), "PY0004", Elapsed(stopwatch))
                    : Failure(ScriptMessages.Timeout(context.Permissions.Timeout), "PY0002", Elapsed(stopwatch));
            }
            catch (PythonException failure)
            {
                interrupter.Finish();
                var position = Position(failure);
                return new ScriptResult(false, null,
                    [ScriptMessages.Failure($"{failure.Type.Name}: {failure.Message}", position, "PY0100")],
                    Elapsed(stopwatch), failure.StackTrace);
            }
            finally
            {
                interrupter.Finish();
                ReleaseCapture(scope);
                if (context.OnOutput is { } sink && scope.Get("__pulse_buffer").InvokeMethod("getvalue").As<string>() is { Length: > 0 } text) sink(text);
            }
        }
    }

    private void ReleaseCapture(PyModule scope)
    {
        try
        {
            scope.Exec(Release);
        }
        catch (PythonException failure) when (failure.Type.Name == "KeyboardInterrupt")
        {
            scope.Exec(Release);
        }
    }

    private sealed class RunInterrupter(ulong thread)
    {
        private readonly Lock _gate = new();
        private bool _finished;

        public void Finish()
        {
            lock (_gate) _finished = true;
        }

        public async Task InterruptAsync()
        {
            while (Interrupt())
                await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
        }

        private bool Interrupt()
        {
            using (Py.GIL())
            {
                lock (_gate)
                {
                    if (_finished) return false;
                    PythonEngine.Interrupt(thread);
                    return true;
                }
            }
        }
    }

    private TimeSpan Elapsed(Stopwatch stopwatch)
    {
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private ScriptPosition Position(PythonException failure)
    {
        using (Py.GIL())
        {
            var value = failure.Value;
            if (value is not null && value.HasAttr("lineno") && value.GetAttr("lineno") is { } line && !line.IsNone())
                return new ScriptPosition(Math.Max(1, line.As<int>()),
                    value.HasAttr("offset") && value.GetAttr("offset") is { } offset && !offset.IsNone() ? Math.Max(1, offset.As<int>()) : 1);

            if (failure.Traceback is { } traceback)
            {
                var frame = traceback;
                while (frame.HasAttr("tb_next") && frame.GetAttr("tb_next") is { } next && !next.IsNone()) frame = next;
                if (frame.HasAttr("tb_lineno")) return new ScriptPosition(Math.Max(1, frame.GetAttr("tb_lineno").As<int>()), 1);
            }

            return new ScriptPosition(1, 1);
        }
    }

    private ScriptResult Failure(ScriptMessage message, string code, TimeSpan elapsed) =>
        new(false, null, [message.Diagnostic(code)], elapsed);
}
