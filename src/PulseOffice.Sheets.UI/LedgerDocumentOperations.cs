using PulseOffice.Foundation.Modules;
using PulseOffice.Printing;
using PulseOffice.Shell;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerDocumentOperations(
    IActiveSheetContext context,
    ISheetPrintDocumentBuilder builder,
    PulseOffice.Formats.ISpreadsheetFormatRegistry formats,
    PulseOffice.Formats.Interchange.HtmlSpreadsheetWriter html,
    ISheetImageInsertion images,
    PulseOffice.Platform.IAtomicFileWriter atomic,
    IWorkbookComparer workbooks,
    PulseOffice.Document.Versions.IVersionTextDiff words,
    PulseOffice.Localization.ITextCatalog texts) : IDocumentOperations
{
    public PulseOffice.Document.Versions.VersionComparison? CompareVersion(OpenDocument document, string path)
    {
        if (context.Workbook is not { } current || !ReferenceEquals(current, document?.Model)) return null;
        using var stream = File.OpenRead(path);
        if (formats.Read(path, stream).Value is not { } saved) return null;

        var differences = workbooks.Compare(saved, current);
        var changes = new List<PulseOffice.Document.Versions.VersionChange>();
        foreach (var sheet in differences.SheetChanges)
        {
            var added = sheet.Kind == SheetDifferenceKind.Added;
            changes.Add(new PulseOffice.Document.Versions.VersionChange(
                added ? PulseOffice.Document.Versions.VersionChangeKind.Added : PulseOffice.Document.Versions.VersionChangeKind.Removed,
                texts.Format("versions.compare.sheet", sheet.SheetName),
                [new PulseOffice.Document.Versions.VersionRun(sheet.SheetName, added ? PulseOffice.Document.Versions.VersionRunKind.Added : PulseOffice.Document.Versions.VersionRunKind.Removed)]));
        }

        foreach (var cell in differences.CellChanges)
        {
            var before = Text(cell.Before, cell.FormulaBefore);
            var after = Text(cell.After, cell.FormulaAfter);
            var location = $"{current.Sheets[cell.SheetIndex].Name}!{cell.Address.ToA1()}";
            changes.Add(before.Length == 0
                ? new(PulseOffice.Document.Versions.VersionChangeKind.Added, location, [new(after, PulseOffice.Document.Versions.VersionRunKind.Added)])
                : after.Length == 0
                    ? new(PulseOffice.Document.Versions.VersionChangeKind.Removed, location, [new(before, PulseOffice.Document.Versions.VersionRunKind.Removed)])
                    : new(PulseOffice.Document.Versions.VersionChangeKind.Changed, location, words.Runs(before, after)));
        }

        return new PulseOffice.Document.Versions.VersionComparison(changes);
    }

    private string Text(PulseOffice.Sheets.Values.CellValue value, string? formula) =>
        !string.IsNullOrEmpty(formula) ? "=" + formula.TrimStart('=')
        : value.Kind switch
        {
            PulseOffice.Sheets.Values.ValueKind.Number => value.AsNumber().ToString("G15", System.Globalization.CultureInfo.CurrentCulture),
            PulseOffice.Sheets.Values.ValueKind.Boolean => value.AsBoolean() ? "TRUE" : "FALSE",
            PulseOffice.Sheets.Values.ValueKind.Error => value.Error.ToString(),
            _ => value.AsText(),
        };

    public bool InsertImage(OpenDocument document, byte[] data, string name) =>
        ReferenceEquals(context.Workbook, document.Model) && images.InsertRaster(data, Path.GetFileNameWithoutExtension(name), name);

    public ModuleKind Module => ModuleKind.Sheets;

    public string SuggestedName(OpenDocument document) => Path.GetFileNameWithoutExtension(document.Title);

    public IPrintableDocument? BuildPrintable(OpenDocument document) =>
        context.HasTarget ? builder.Build(SuggestedName(document)) : null;

    public IPrintableDocument? PreviewFile(string path)
    {
        using var stream = File.OpenRead(path);
        return formats.Read(path, stream).Value is { } workbook ? builder.Build(workbook, Path.GetFileNameWithoutExtension(path)) : null;
    }

    public IPrintableDocument? BuildRange(OpenDocument document, string reference) =>
        context.HasTarget && Addressing.CellRange.TryParseA1(reference, out var range) ? builder.BuildRange(SuggestedName(document), range) : null;

    public PdfExportProfile PdfExport(OpenDocument document) => new(HasHeadings: false, LayoutLabel: string.Empty, Layouts: []);

    public PulseOffice.Document.Metadata.DocumentProperties? Properties(OpenDocument document) =>
        WorkbookOf(document)?.Properties;

    public string NativeExtension => "ods";

    public string OfficeExtension => "xlsx";

    public bool WriteTo(OpenDocument document, string path)
    {
        if (WorkbookOf(document) is not { } workbook || formats.WriterFor(path) is not { } writer) return false;

        return atomic.Replace(path, stream => writer.Write(workbook, stream).Succeeded, written => written);
    }

    public string? RenderHtml(OpenDocument document) =>
        WorkbookOf(document) is { } workbook ? html.Render(workbook) : null;

    public PulseOffice.Document.Versions.DocumentVersionCollection? Versions(OpenDocument document) =>
        WorkbookOf(document)?.Versions;

    private Workbook? WorkbookOf(OpenDocument document) => document?.Model as Workbook;

    public string TemplateExtension => "ots";

    public string? OfficeTemplateExtension => "xltx";

}
