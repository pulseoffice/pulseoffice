namespace PulseOffice.Foundation.Diagnostics;

public enum Severity { Info, Warning, Error }

public sealed record Diagnostic(Severity Severity, string Code, string Message, string? Location = null)
{
    public static Diagnostic Info(string code, string message, string? location = null) => new(Severity.Info, code, message, location);
    public static Diagnostic Warning(string code, string message, string? location = null) => new(Severity.Warning, code, message, location);
    public static Diagnostic Error(string code, string message, string? location = null) => new(Severity.Error, code, message, location);

    public IReadOnlyList<object?> Arguments { get; init; } = [];

    public string MessageKey => "diagnostic." + Code.ToLowerInvariant();

    public Diagnostic With(params object?[] arguments) => this with { Arguments = arguments };

    public override string ToString() => Location is null ? $"[{Code}] {Message}" : $"[{Code}] {Message} ({Location})";
}

public sealed class OperationResult<T>
{
    private readonly List<Diagnostic> _diagnostics;

    private OperationResult(T? value, bool succeeded, List<Diagnostic> diagnostics)
    {
        Value = value;
        Succeeded = succeeded;
        _diagnostics = diagnostics;
    }

    public T? Value { get; }
    public bool Succeeded { get; }
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    public bool HasWarnings => _diagnostics.Any(d => d.Severity == Severity.Warning);
    public bool HasErrors => _diagnostics.Any(d => d.Severity == Severity.Error);

    public static OperationResult<T> Ok(T value, IEnumerable<Diagnostic>? diagnostics = null) =>
        new(value, true, diagnostics?.ToList() ?? []);

    public static OperationResult<T> Fail(params Diagnostic[] diagnostics) =>
        new(default, false, [.. diagnostics]);

    public T Require() => Succeeded && Value is not null
    ? Value
    : throw new InvalidOperationException(_diagnostics.FirstOrDefault(d => d.Severity == Severity.Error)?.Message
                                          ?? "The operation failed.");
}
