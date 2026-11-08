using PulseOffice.Formats.Intake;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Sheets;

namespace PulseOffice.Formats;

public interface ISpreadsheetFormatRegistry
{
    IReadOnlyList<SpreadsheetFormat> ReadableFormats { get; }
    IReadOnlyList<SpreadsheetFormat> WritableFormats { get; }

    SpreadsheetFormat Native { get; }

    ISpreadsheetReader? ReaderFor(string path);
    ISpreadsheetWriter? WriterFor(string path);
    ISpreadsheetWriter? WriterFor(SpreadsheetFormat format);

    ISpreadsheetReader? ReaderForContent(Stream stream);

    OperationResult<Workbook> Read(string path, Stream stream);
}

public sealed class SpreadsheetFormatRegistry(IEnumerable<ISpreadsheetReader> readers, IEnumerable<ISpreadsheetWriter> writers, IDocumentIntake intake)
    : DocumentFormatRegistry<Workbook, SpreadsheetFormat, ISpreadsheetReader, ISpreadsheetWriter>(readers, writers, intake), ISpreadsheetFormatRegistry
{
    protected override string Family => "spreadsheet";

    protected override string UnknownFormatCode => "FMT001";

    public ISpreadsheetWriter? WriterFor(SpreadsheetFormat format) =>
        Writers.FirstOrDefault(w => w.Format.Id == format.Id);
}
