using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Formats.Intake;

public interface IDocumentIntake
{
    OperationResult<T> Read<T>(Stream stream, Func<Stream, OperationResult<T>> read);

    bool Accepts(Stream stream, Func<Stream, bool> probe);
}

public sealed class DocumentIntake(IIncomingDocumentInspector inspector) : IDocumentIntake
{
    public DocumentIntake() : this(new IncomingDocumentInspector())
    {
    }

    public OperationResult<T> Read<T>(Stream stream, Func<Stream, OperationResult<T>> read)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(read);

        if (inspector.Inspect(stream) is { } rejection) return OperationResult<T>.Fail(rejection);

        try
        {
            return read(stream);
        }
        catch (OutOfMemoryException)
        {
            return OperationResult<T>.Fail(Diagnostic.Error("FMT014", "The file is too large to open with the available memory."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return OperationResult<T>.Fail(Damaged(exception));
        }
    }

    public bool Accepts(Stream stream, Func<Stream, bool> probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        try
        {
            return probe(stream);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return false;
        }
    }

    private Diagnostic Damaged(Exception exception) =>
        Diagnostic.Error("FMT010", $"The file is damaged and cannot be read ({exception.GetType().Name} in {exception.TargetSite?.DeclaringType?.FullName}.{exception.TargetSite?.Name}).");
}
