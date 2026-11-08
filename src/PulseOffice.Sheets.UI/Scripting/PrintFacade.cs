using PulseOffice.Scripting.Editor.Facades;
using PulseOffice.Formats.Pdf;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Platform;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.UI.Scripting;

public sealed class PrintFacade(
    IScriptDispatcher dispatcher,
    IActiveSheetContext context,
    ISheetPrintDocumentBuilder documents,
    IPdfExporter exporter,
    IPaperCatalog papers,
    IPrintServiceProvider printers,
    IScriptSheetEdits edits,
    PulseOffice.Platform.IAtomicFileWriter atomic,
    PulseOffice.Scripting.IScriptExecutionPermissions permissions) : ScriptFacade(dispatcher)
{
    public string[] Papers => Read<string[]>(() =>
        [.. papers.All.Select(paper => paper.Id)]);

    public string Paper
    {
        get => Read(() => Setup()?.Paper.Id ?? string.Empty);
        set
        {
            var paper = papers.All.FirstOrDefault(paper =>
                    string.Equals(paper.Id, value, StringComparison.CurrentCultureIgnoreCase))
                ?? throw new ArgumentException(
                    $"Unknown paper '{value}': use one of {string.Join(", ", papers.All.Select(paper => paper.Id))}.", nameof(value));
            Change(setup => setup with { Paper = paper });
        }
    }

    public string Orientation
    {
        get => Read(() => Setup()?.Orientation.ToString() ?? string.Empty);
        set
        {
            var orientation = (value ?? string.Empty).ToLowerInvariant() switch
            {
                "landscape" or "orizzontale" => PageOrientation.Landscape,
                "portrait" or "verticale" => PageOrientation.Portrait,
                _ => throw new ArgumentException($"Unknown orientation '{value}': use Portrait or Landscape.", nameof(value)),
            };
            Change(setup => setup with { Orientation = orientation });
        }
    }

    public void Margins(double millimetres)
    {
        if (!double.IsFinite(millimetres) || millimetres < 0)
            throw new ArgumentOutOfRangeException(nameof(millimetres), millimetres, "The margin must be a positive number.");

        Change(setup => setup with { Margins = Uniform(Fitting(setup, millimetres)) });
    }

    public void Header(string left, string centre, string right) => Change(setup => setup with
    {
        Header = new HeaderFooter(left, centre, right),
    });

    public void Footer(string left, string centre, string right) => Change(setup => setup with
    {
        Footer = new HeaderFooter(left, centre, right),
    });

    public void FirstHeader(string left, string centre, string right) => Change(setup => setup with
    {
        FirstHeader = new HeaderFooter(left, centre, right),
        DifferentFirstPage = true,
    });

    public void FirstFooter(string left, string centre, string right) => Change(setup => setup with
    {
        FirstFooter = new HeaderFooter(left, centre, right),
        DifferentFirstPage = true,
    });

    public void EvenHeader(string left, string centre, string right) => Change(setup => setup with
    {
        EvenHeader = new HeaderFooter(left, centre, right),
        DifferentOddEven = true,
    });

    public void EvenFooter(string left, string centre, string right) => Change(setup => setup with
    {
        EvenFooter = new HeaderFooter(left, centre, right),
        DifferentOddEven = true,
    });

    public bool DifferentFirstPage
    {
        get => Read(() => Setup()?.DifferentFirstPage ?? false);
        set => Change(setup => setup with { DifferentFirstPage = value });
    }

    public bool DifferentOddEven
    {
        get => Read(() => Setup()?.DifferentOddEven ?? false);
        set => Change(setup => setup with { DifferentOddEven = value });
    }

    public int FirstPageNumber
    {
        get => Read(() => Setup()?.FirstPageNumber ?? 0);
        set
        {
            if (value is < 0 or > MaximumFirstPageNumber)
                throw new ArgumentOutOfRangeException(nameof(value), value, $"The first page number must be between 1 and {MaximumFirstPageNumber}, or 0 for automatic numbering.");
            Change(setup => setup with { FirstPageNumber = value == 0 ? null : value });
        }
    }

    public bool GridLines
    {
        get => Read(() => Setup()?.PrintGridLines ?? false);
        set => Change(setup => setup with { PrintGridLines = value });
    }

    public bool Headings
    {
        get => Read(() => Setup()?.PrintHeadings ?? false);
        set => Change(setup => setup with { PrintHeadings = value });
    }

    public void PrintArea(string reference)
    {
        CellRange? range = null;
        if (reference is { Length: > 0 })
            range = CellRange.TryParseA1(reference, out var parsed)
                ? parsed
                : throw new ArgumentException($"'{reference}' is not a cell range.", nameof(reference));

        Change(() => edits.Record("PrintArea", () =>
        {
            if (Sheet() is not { } sheet) return false;

            sheet.PrintRanges.Clear();
            if (range is { } area) sheet.PrintRanges.Add(area);
            return true;
        }, done => done));
    }

    public void RepeatRows(int count) => Change(() =>
    {
        if (count is < 0 or > MaximumRepeatedRows)
            throw new ArgumentOutOfRangeException(nameof(count), count, $"The repeated rows must be between 0 and {MaximumRepeatedRows}.");
        if (Sheet() is not { } sheet) return;

        var before = sheet.RepeatedPrintRows;
        var after = count;
        if (before == after) return;

        sheet.RepeatedPrintRows = after;
        context.History?.Push(new PulseOffice.Document.Commands.DelegateAction(
            "RepeatRows",
            redo: () => sheet.RepeatedPrintRows = after,
            undo: () => sheet.RepeatedPrintRows = before));
    });

    public int PageCount => Read(() =>
        documents.Build(Name()) is { } document ? document.PageCount : 0);

    public string[] Printers
    {
        get
        {
            var service = Read(() => printers.Current);

            if (!service.Capabilities.ChoosePrinter) return [];

            return [.. Task.Run(() => service.PrintersAsync()).GetAwaiter().GetResult().Select(printer => printer.DisplayName)];
        }
    }

    public bool ExportPdf(string path)
    {
        permissions.Demand(PulseOffice.Scripting.ScriptCapability.FileSystemWrite);
        return Export(path);
    }

    private bool Export(string path) => Read(() =>
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("The PDF path is empty.", nameof(path));
        if (!string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"'{path}' does not end in .pdf.", nameof(path));
        if (File.Exists(path)) throw new InvalidOperationException($"'{path}' already exists: macros do not overwrite files.");
        if (documents.Build(Name()) is not { } document) return false;

        try
        {
            return atomic.Replace(path, stream => exporter.Write(document, stream, new PdfExportOptions(Title: Name())).Succeeded, written => written);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"The PDF could not be written to '{path}': {failure.Message}", failure);
        }
    });

    private const int MaximumRepeatedRows = 100;

    private const int MaximumFirstPageNumber = 32767;

    private const double MinimumPrintableMillimetres = 10;

    private Margins Uniform(Twip margin) => new(margin, margin, margin, margin);

    private Twip Fitting(PageSetup setup, double millimetres)
    {
        var shortest = Math.Min(setup.PageWidth.Value, setup.PageHeight.Value) / Twip.PerCentimeter * 10;
        return 2 * millimetres + MinimumPrintableMillimetres <= shortest
            ? Twip.FromMillimeters(millimetres)
            : throw new ArgumentOutOfRangeException(nameof(millimetres), millimetres,
                $"The margins leave less than {MinimumPrintableMillimetres} millimetres of printable page.");
    }

    private PageSetup? Setup() =>
        context.Workbook is { } workbook && Sheet() is { } sheet
            ? workbook.PageStyles.For(sheet).Setup
            : null;

    private void Change(Func<PageSetup, PageSetup> change) => Change(() =>
    {
        if (context.Workbook is not { } workbook || Sheet() is not { } sheet) return;

        var catalog = workbook.PageStyles;
        var setup = change(catalog.For(sheet).Setup);
        var name = catalog.OwnNameFor(sheet, workbook.Sheets);
        var previousName = sheet.PageStyleName;
        var previousSetup = catalog.Find(name)?.Setup;

        void Apply()
        {
            catalog.Define(name, setup);
            sheet.PageStyleName = name;
        }

        void Revert()
        {
            if (previousSetup is null) catalog.Remove(name);
            else catalog.Define(name, previousSetup);
            sheet.PageStyleName = previousName;
        }

        Apply();
        context.History?.Push(new PulseOffice.Document.Commands.DelegateAction("PageSetup", redo: Apply, undo: Revert));
    });

    private Worksheet? Sheet() =>
        context.Workbook is { } workbook && context.SheetIndex >= 0 && context.SheetIndex < workbook.Sheets.Count
            ? workbook.Sheets[context.SheetIndex]
            : null;

    private string Name() => Sheet()?.Name ?? "Ledger";
}
