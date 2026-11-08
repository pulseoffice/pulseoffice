using PulseOffice.Formats;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Foundation.Modules;
using PulseOffice.Platform;
using PulseOffice.Shell;

namespace PulseOffice.Documents.UI.Shell;

public interface IFolioDocumentService : IWorkspaceOpener, IDocumentFileService
{
    Task<TextDocument?> ReadAsync(string path, CancellationToken ct = default);
}

public sealed class FolioModuleFormats(ITextDocumentFormatRegistry formats) : IModuleFormats<TextDocument>
{
    public IReadOnlyList<FileFilter> ReadableFilters => Filters(formats.ReadableFormats);

    public IReadOnlyList<FileFilter> WritableFilters => Filters(formats.WritableFormats);

    public string NativeExtension => formats.Native.Extensions[0];

    public string InterchangeExtension => ".docx";

    public bool CanRead(string path) => formats.ReaderFor(path) is not null;

    public IModuleWriter<TextDocument>? WriterFor(string path) => Adapt(formats.WriterFor(path));

    public IModuleWriter<TextDocument>? NativeWriter => Adapt(formats.WriterFor(NativeExtension));

    public OperationResult<TextDocument> Read(string path, Stream stream) => formats.Read(path, stream);

    private static DelegatingModuleWriter<TextDocument>? Adapt(ITextDocumentWriter? writer) =>
        writer is null ? null : new(writer.CheckFidelity, writer.Write);

    private static IReadOnlyList<FileFilter> Filters(IReadOnlyList<TextDocumentFormat> list) =>
        [.. list.OrderByDescending(format => format.IsNative).Select(format => new FileFilter(format.DisplayName, format.Extensions))];
}

public sealed class FolioDocumentService(
    FolioModuleFormats formats,
    DocumentServiceContext context,
    IFolioDocumentFactory documents) : ModuleDocumentService<TextDocument>(formats, context), IFolioDocumentService
{
    public override ModuleKind Module => ModuleKind.Documents;

    protected override OpenDocument CreateDocument(TextDocument model, string name)
    {
        model.FileName = name;
        return documents.CreateFrom(model, name);
    }

    protected override void Stamp(TextDocument model, string fileName)
    {
        model.FileName = fileName;
        model.Properties.MarkSaved("PulseOffice", Identity.Name);
    }
}
