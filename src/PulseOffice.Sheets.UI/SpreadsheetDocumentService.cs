using PulseOffice.Formats;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Foundation.Modules;
using PulseOffice.Platform;
using PulseOffice.Shell;

namespace PulseOffice.Sheets.UI;

public interface ISpreadsheetDocumentService
{
    Task OpenAsync(CancellationToken ct = default);

    Task OpenAsync(string path, CancellationToken ct = default);

    Task SaveAsync(OpenDocument document, CancellationToken ct = default);

    Task SaveAsAsync(OpenDocument document, CancellationToken ct = default);
}

public sealed class SpreadsheetModuleFormats(ISpreadsheetFormatRegistry formats) : IModuleFormats<Workbook>
{
    public IReadOnlyList<FileFilter> ReadableFilters => Filters(formats.ReadableFormats);

    public IReadOnlyList<FileFilter> WritableFilters => Filters(formats.WritableFormats);

    public string NativeExtension => formats.Native.Extensions[0];

    public string InterchangeExtension => ".xlsx";

    public bool CanRead(string path) => formats.ReaderFor(path) is not null;

    public IModuleWriter<Workbook>? WriterFor(string path) => Adapt(formats.WriterFor(path));

    public IModuleWriter<Workbook>? NativeWriter => Adapt(formats.WriterFor(formats.Native));

    public OperationResult<Workbook> Read(string path, Stream stream) => Named(path, formats.Read(path, stream));

    public PulseOffice.Formats.Interchange.CsvSpreadsheetReader? DelimitedReaderFor(string path) =>
        formats.ReaderFor(path) as PulseOffice.Formats.Interchange.CsvSpreadsheetReader;

    public OperationResult<Workbook> ReadDelimited(string path, Stream stream, PulseOffice.Formats.Interchange.CsvOptions options) =>
        DelimitedReaderFor(path) is { } reader ? Named(path, reader.WithOptions(options).Read(stream)) : Read(path, stream);

    private OperationResult<Workbook> Named(string path, OperationResult<Workbook> result)
    {
        if (result.Value is { Sheets.Count: 1 } workbook && DelimitedReaderFor(path) is not null
            && _names.FromFile(path) is { Length: > 0 } name)
            workbook.Sheets[0].Name = name;
        return result;
    }

    private readonly TextFileSheetName _names = new();

    private static DelegatingModuleWriter<Workbook>? Adapt(ISpreadsheetWriter? writer) =>
        writer is null ? null : new(writer.CheckFidelity, writer.Write);

    private static IReadOnlyList<FileFilter> Filters(IReadOnlyList<SpreadsheetFormat> list) =>
        [.. list.OrderByDescending(format => format.IsNative).Select(format => new FileFilter(format.DisplayName, format.Extensions))];
}

public sealed class SpreadsheetDocumentService(
    SpreadsheetModuleFormats formats,
    DocumentServiceContext context,
    ILedgerDocumentFactory documents,
    Dialogs.ICsvImportPrompt delimited,
    Dialogs.ICsvUnattendedOptions unattendedOptions,
    IUnattendedOpening unattended) : ModuleDocumentService<Workbook>(formats, context), ISpreadsheetDocumentService
{
    public override ModuleKind Module => ModuleKind.Sheets;

    protected override async Task<Func<Stream, OperationResult<Workbook>>?> ReaderAsync(string path, Stream content, CancellationToken ct)
    {
        if (formats.DelimitedReaderFor(path) is not { } reader) return await base.ReaderAsync(path, content, ct).ConfigureAwait(true);

        var detected = reader.Detect(content);
        if (unattended.IsActive)
        {
            var given = unattendedOptions.Apply(detected, unattended.Options);
            return stream => formats.ReadDelimited(path, stream, given);
        }

        var head = new byte[64 * 1024];
        var origin = content.Position;
        var length = content.Read(head, 0, head.Length);
        content.Position = origin;

        if (await delimited.AskAsync(Path.GetFileName(path), head[..length], detected, ct).ConfigureAwait(true) is not { } chosen) return null;
        return stream => formats.ReadDelimited(path, stream, chosen);
    }

    protected override OpenDocument CreateDocument(Workbook model, string name) => documents.CreateFrom(model, name);

    protected override void Stamp(Workbook model, string fileName) =>
        model.Properties.MarkSaved("PulseOffice", Identity.Name);
}

public sealed class TextFileSheetName
{
    private const int Longest = 31;

    private readonly char[] _forbidden = ['[', ']', '*', '?', ':', '/', '\\'];

    public string FromFile(string path)
    {
        var stem = string.Concat(Path.GetFileNameWithoutExtension(path).Select(character => _forbidden.Contains(character) ? '_' : character)).Trim();
        return stem.Length > Longest ? stem[..Longest] : stem;
    }
}
