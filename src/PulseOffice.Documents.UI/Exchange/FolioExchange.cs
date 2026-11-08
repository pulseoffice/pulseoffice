using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Clipboard;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Drawing.Assets;
using PulseOffice.Exchange;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Exchange;

public sealed record FolioNative(DocumentFragment Fragment);

public sealed class FolioExchange(
    IActiveFolioContext context,
    IFolioRichTransfer richText,
    IObjectFragments fragments,
    IFolioImages images,
    ICellGridConversions grids,
    IEmbeddedObjectPicture pictures,
    PulseOffice.UI.Embedding.ISheetObjectFactory sheets,
    IPortableObjects portable) : IExchangeParticipant
{
    public ModuleKind Module => ModuleKind.Documents;

    public bool CanCapture => context.Target?.Editor is not null;

    public bool CanReceive => context.Target?.Editor is { } editor && !editor.ReadOnly();

    public ExchangePackage? Capture(string id, bool cut)
    {
        if (context.Target?.Editor is not { } editor) return null;
        var text = editor.SelectedText();
        var selected = editor.SelectedObjectId is { } objectId ? editor.Document.Objects.Find(objectId) : null;
        var fragment = cut ? editor.Cut() : editor.Copy();
        if (fragment is null) return null;

        var embedded = selected is null ? null : portable.Portable(selected.Content);
        var picture = embedded is null ? null : pictures.Png(embedded, selected!.Size, editor.Services.Cultures.For(editor.Document.Language));
        var representations = embedded is RasterImageObject { MediaType: "image/png" } && picture is not null
            ? [new ExchangeRepresentation(ExchangeFlavor.Png, picture)]
            : picture is null ? richText.Export(fragment, text) : [.. richText.Export(fragment, text), new ExchangeRepresentation(ExchangeFlavor.Png, picture)];
        return new ExchangePackage
        {
            Id = id,
            Origin = Module,
            Content = embedded is null ? null : new ObjectsContent([new ExchangedObject(embedded, selected!.Size)]),
            Native = new FolioNative(fragment),
            Representations = representations,
        };
    }

    public IReadOnlyList<ExchangePasteMode> Modes(ExchangePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var modes = new List<ExchangePasteMode>();
        if (Native(package) is not null) modes.Add(ExchangePasteMode.Native);
        else if (package.Content is ObjectsContent) modes.Add(ExchangePasteMode.Objects);
        if (package.Content is CellRangeContent)
        {
            modes.Add(ExchangePasteMode.Table);
            if (package.Find(ExchangeFlavor.OpenDocument, ExchangeMediaTypes.Spreadsheet) is not null) modes.Add(ExchangePasteMode.SheetObject);
        }
        if (package.Origin != Module && package.Content is not CellRangeContent && Formatted(package).Representations.Count > 0)
            modes.Add(ExchangePasteMode.FormattedText);
        if (package.TextOf(ExchangeFlavor.Text) is { Length: > 0 }) modes.Add(ExchangePasteMode.UnformattedText);
        if (package.Find(ExchangeFlavor.Png) is not null) modes.Add(ExchangePasteMode.Image);
        return modes;
    }

    public bool Receive(ExchangePackage package, ExchangePasteMode mode)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (context.Target?.Editor is not { } editor || editor.ReadOnly()) return false;
        return mode switch
        {
            ExchangePasteMode.Native => Native(package) is { } native && editor.Paste(native),
            ExchangePasteMode.Objects when package.Content is ObjectsContent content =>
                editor.Paste(fragments.Wrap([.. content.Objects.Select(item => (item.Object, item.Size))])),
            ExchangePasteMode.Table when package.Content is CellRangeContent grid =>
                richText.ImportHtml(grids.Html(grid)) is { } table && editor.Paste(table),
            ExchangePasteMode.SheetObject when package.Content is CellRangeContent grid && package.Find(ExchangeFlavor.OpenDocument, ExchangeMediaTypes.Spreadsheet) is { } ods =>
                editor.Paste(fragments.Wrap([(sheets.Create(editor.Document.NextId("sheet"), grid, ods), default)])),
            ExchangePasteMode.FormattedText => richText.Import(Formatted(package)) is { } formatted && editor.Paste(formatted),
            ExchangePasteMode.UnformattedText when package.TextOf(ExchangeFlavor.Text) is { Length: > 0 } text => editor.PasteText(text),
            ExchangePasteMode.Image when package.BytesOf(ExchangeFlavor.Png) is { } png => images.Insert(editor, png, "image/png", "image.png"),
            _ => false,
        };
    }

    private ExchangePackage Formatted(ExchangePackage package) => package with
    {
        Representations = [.. package.Representations.Where(item => item.Flavor is ExchangeFlavor.Html or ExchangeFlavor.Rtf
            || item.Flavor == ExchangeFlavor.OpenDocument && item.MediaType == ExchangeMediaTypes.Text
            || item.Flavor == ExchangeFlavor.OpenXml && item.MediaType == ExchangeMediaTypes.WordDocument)],
    };

    private DocumentFragment? Native(ExchangePackage package) =>
        package.NativeFrom<FolioNative>(Module)?.Fragment
        ?? (package.Origin == Module && package.Find(ExchangeFlavor.OpenDocument, ExchangeMediaTypes.Text) is not null ? richText.Import(package) : null);
}
