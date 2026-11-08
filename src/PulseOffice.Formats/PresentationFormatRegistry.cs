using PulseOffice.Formats.Intake;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Presentations;

namespace PulseOffice.Formats;

public interface IPresentationFormatRegistry
{
    IReadOnlyList<PresentationFormat> ReadableFormats { get; }
    IReadOnlyList<PresentationFormat> WritableFormats { get; }

    PresentationFormat Native { get; }

    IPresentationReader? ReaderFor(string path);
    IPresentationWriter? WriterFor(string path);

    IPresentationReader? ReaderForContent(Stream stream);

    OperationResult<Presentation> Read(string path, Stream stream);
}

public sealed class PresentationFormatRegistry(IEnumerable<IPresentationReader> readers, IEnumerable<IPresentationWriter> writers, IDocumentIntake intake)
    : DocumentFormatRegistry<Presentation, PresentationFormat, IPresentationReader, IPresentationWriter>(readers, writers, intake), IPresentationFormatRegistry
{
    protected override string Family => "presentation";

    protected override string UnknownFormatCode => "FMT002";
}
