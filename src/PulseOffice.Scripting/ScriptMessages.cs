using System.Globalization;

namespace PulseOffice.Scripting;

public sealed record ScriptMessage(string Fallback, string? Key = null, params object[] Arguments)
{
    public ScriptDiagnostic Diagnostic(string code) =>
        new(ScriptDiagnosticSeverity.Error, Fallback, new ScriptPosition(1, 1), new ScriptPosition(1, 1), code)
        {
            MessageKey = Key,
            MessageArguments = Arguments,
        };
}

public static class ScriptMessages
{
    public const string TimeoutKey = "macro.error.timeout";
    public const string CancelledKey = "macro.error.cancelled";
    public const string MemoryKey = "macro.error.memory";
    public const string RecursionKey = "macro.error.recursion";
    public const string PythonSandboxKey = "macro.error.pythonSandbox";
    public const string PythonUnavailableKey = "macro.error.pythonUnavailable";
    public const string SyntaxKey = "macro.error.syntax";
    public const string FailureKey = "macro.error.failure";
    public const string InternalKey = "macro.error.internal";

    public static ScriptMessage Timeout(TimeSpan limit)
    {
        var seconds = Math.Round(limit.TotalSeconds);
        return new ScriptMessage(string.Create(CultureInfo.InvariantCulture, $"Execution stopped after {seconds:0} seconds."), TimeoutKey, seconds);
    }

    public static ScriptMessage Cancelled() => new("Execution cancelled.", CancelledKey);

    public static ScriptMessage Memory() => new("The macro exceeded its memory limit.", MemoryKey);

    public static ScriptMessage PythonSandbox() =>
        new("Python macros cannot run in the sandbox: open them in the macro editor and run them from there.", PythonSandboxKey);

    public static ScriptMessage PythonUnavailable() =>
        new("No Python installation is available for CPython macros: install Python, or allow the download of a standalone Python, or choose IronPython in the macro settings.", PythonUnavailableKey);

    public static ScriptMessage Internal(string detail) => new(detail, InternalKey);

    public static ScriptMessage Recursion() => new("Recursion too deep.", RecursionKey);

    public static ScriptDiagnostic Syntax(string detail, ScriptPosition position, string code) => Located(SyntaxKey, detail, position, code);

    public static ScriptDiagnostic Failure(string detail, ScriptPosition position, string code) => Located(FailureKey, detail, position, code);

    private static ScriptDiagnostic Located(string key, string detail, ScriptPosition position, string code) =>
        new(ScriptDiagnosticSeverity.Error, detail, position, position, code)
        {
            MessageKey = key,
            MessageArguments = [position.Line],
        };
}
