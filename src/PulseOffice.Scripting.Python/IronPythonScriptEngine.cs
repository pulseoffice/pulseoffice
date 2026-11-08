using System.Diagnostics;
using System.Text;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using PulseOffice.Foundation.Modules;

namespace PulseOffice.Scripting.Python;

public sealed class IronPythonScriptEngine : IScriptEngine
{
    private readonly PythonLanguage _language = new();

    public ScriptLanguage Language => ScriptLanguage.Python;

    public string DisplayName => "Python (IronPython)";

    public string RuntimeVersion =>
        typeof(IronPython.Hosting.Python).Assembly.GetName().Version?.ToString() ?? "unknown";

    public bool IsSandboxed => false;

    public IReadOnlySet<string> Keywords => _language.Keywords;

    public IReadOnlyList<ScriptDiagnostic> Analyze(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var engine = IronPython.Hosting.Python.CreateEngine();
        var scriptSource = engine.CreateScriptSourceFromString(source, SourceCodeKind.Statements);

        try
        {
            scriptSource.Compile();
            return [];
        }
        catch (SyntaxErrorException failure)
        {
            var position = new ScriptPosition(
                Math.Max(1, failure.Line), Math.Max(1, failure.Column));

            return [ScriptMessages.Syntax(failure.Message, position, "PY0001")];
        }
    }

    public async Task<ScriptResult> ExecuteAsync(
        string source, ScriptContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);

        var syntax = Analyze(source);

        if (syntax.Any(diagnostic => diagnostic.Severity == ScriptDiagnosticSeverity.Error))
            return new ScriptResult(false, null, syntax, TimeSpan.Zero);

        if (!context.Permissions.AllowsUnsandboxedEngines)
            return Failure(ScriptMessages.PythonSandbox(), "PY0003", TimeSpan.Zero);

        var stopwatch = Stopwatch.StartNew();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(context.Permissions.Timeout);
        var token = timeout.Token;

        var running = Task.Run(() =>
        {
            var engine = IronPython.Hosting.Python.CreateEngine();
            IronPython.Hosting.Python.SetTrace(engine, Trace);

            IronPython.Runtime.Exceptions.TracebackDelegate Trace(
                IronPython.Runtime.Exceptions.TraceBackFrame frame, string result, object payload)
            {
                token.ThrowIfCancellationRequested();
                return Trace;
            }

            using var output = new MemoryStream();

            if (context.OnOutput is not null) engine.Runtime.IO.SetOutput(output, Encoding.UTF8);

            var scope = engine.CreateScope();

            foreach (var (name, value) in context.Globals) scope.SetVariable(name, value);

            try
            {
                var compiled = engine
                    .CreateScriptSourceFromString(source, SourceCodeKind.Statements)
                    .Compile();

                var value = compiled.Execute(scope);

                Drain(output, context.OnOutput);

                stopwatch.Stop();

                return new ScriptResult(
                    true, value, Array.Empty<ScriptDiagnostic>(), stopwatch.Elapsed);
            }
            catch (OperationCanceledException)
            {
                Drain(output, context.OnOutput);
                stopwatch.Stop();
                return ct.IsCancellationRequested
                    ? Failure(ScriptMessages.Cancelled(), "PY0004", stopwatch.Elapsed)
                    : Failure(ScriptMessages.Timeout(context.Permissions.Timeout), "PY0002", stopwatch.Elapsed);
            }
            catch (Exception failure)
            {
                Drain(output, context.OnOutput);
                stopwatch.Stop();

                var position = FailurePosition(engine, failure);

                return new ScriptResult(
                    false, null,
                    [ScriptMessages.Failure(failure.Message, position, "PY0100")],
                    stopwatch.Elapsed,
                    failure.StackTrace);
            }
        }, CancellationToken.None);

        var limit = context.Permissions.Timeout + TimeSpan.FromSeconds(2);
        if (await Task.WhenAny(running, Task.Delay(limit, CancellationToken.None)).ConfigureAwait(false) == running)
            return await running.ConfigureAwait(false);

        return Failure(ScriptMessages.Timeout(context.Permissions.Timeout), "PY0002", stopwatch.Elapsed);

        static ScriptPosition FailurePosition(Microsoft.Scripting.Hosting.ScriptEngine engine, Exception failure)
        {
            var line = engine.GetService<Microsoft.Scripting.Hosting.ExceptionOperations>()
                .GetStackFrames(failure)
                .Select(frame => frame.GetFileLineNumber())
                .LastOrDefault(number => number > 0);

            return new ScriptPosition(Math.Max(1, line), 1);
        }

        static void Drain(MemoryStream stream, Action<string>? sink)
        {
            if (sink is null || stream.Length == 0) return;

            var text = Decode(stream.ToArray());

            if (text.Length > 0) sink(text);
        }

        static ScriptResult Failure(ScriptMessage message, string code, TimeSpan elapsed) =>
            new(false, null, [message.Diagnostic(code)], elapsed);
    }

    public IReadOnlyList<CompletionItem> Complete(
        string source, ScriptPosition position, ScriptApiCatalog api, ModuleKind module) =>
        _language.Complete(source, position, api, module);

    internal static string Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        var zeros = 0;

        for (var index = 1; index < bytes.Length; index += 2)
            if (bytes[index] == 0)
                zeros++;

        return zeros * 2 > bytes.Length / 2
            ? Encoding.Unicode.GetString(bytes)
            : Encoding.UTF8.GetString(bytes);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static class PythonEngineMarker;
}
