using PulseOffice.Formats;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Foundation.Modules;
using PulseOffice.Platform;
using PulseOffice.Shell;
using PulseOffice.UI.Controls.Notifications;

namespace PulseOffice.Presentations.UI;

public interface IDeckDocumentService
{
    Task SaveAsync(OpenDocument document, CancellationToken ct = default);

    Task SaveAsAsync(OpenDocument document, CancellationToken ct = default);
}

public sealed class DeckModuleFormats(IPresentationFormatRegistry formats) : IModuleFormats<Presentation>
{
    public IReadOnlyList<FileFilter> ReadableFilters => Filters(formats.ReadableFormats);

    public IReadOnlyList<FileFilter> WritableFilters => Filters(formats.WritableFormats);

    public string NativeExtension => formats.Native.Extensions[0];

    public string InterchangeExtension => ".pptx";

    public bool CanRead(string path) => formats.ReaderFor(path) is not null;

    public IModuleWriter<Presentation>? WriterFor(string path) => Adapt(formats.WriterFor(path));

    public IModuleWriter<Presentation>? NativeWriter => Adapt(formats.WriterFor(NativeExtension));

    public OperationResult<Presentation> Read(string path, Stream stream) => formats.Read(path, stream);

    private static DelegatingModuleWriter<Presentation>? Adapt(IPresentationWriter? writer) =>
        writer is null ? null : new(writer.CheckFidelity, writer.Write);

    private static IReadOnlyList<FileFilter> Filters(IReadOnlyList<PresentationFormat> list) =>
        [.. list.OrderByDescending(format => format.IsNative).Select(format => new FileFilter(format.DisplayName, format.Extensions))];
}

public sealed class DeckDocumentService(
    DeckModuleFormats formats,
    DocumentServiceContext context,
    IDeckDocumentFactory documents,
    IDeckLinkResolver links) : ModuleDocumentService<Presentation>(formats, context), IDeckDocumentService
{
    public override ModuleKind Module => ModuleKind.Presentations;

    protected override OpenDocument CreateDocument(Presentation model, string name)
    {
        model.FileName = name;
        return documents.CreateFrom(model, name);
    }

    protected override void Opened(Presentation model, string path) => links.Resolve(model, path);

    protected override void Loaded(Presentation model)
    {
        if (model.Slides.SelectMany(slide => slide.Elements).OfType<ObjectElement>()
            .Any(element => element.Content is PulseOffice.Drawing.Assets.RasterImageObject { LinkPath.Length: > 0 }))
            Status.Report(Texts["links.notLoaded"], NotificationSeverity.Info);
    }

    protected override void Stamp(Presentation model, string fileName)
    {
        model.FileName = fileName;
        model.Properties.MarkSaved("PulseOffice", Identity.Name);
    }
}
