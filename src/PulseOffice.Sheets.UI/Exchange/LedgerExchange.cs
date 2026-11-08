using PulseOffice.Foundation.Colors;
using PulseOffice.Charts;
using PulseOffice.Document.Commands;
using PulseOffice.Drawing.Assets;
using PulseOffice.Exchange;
using PulseOffice.Formats.Interchange.Text.Html;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using BorderLineStyle = PulseOffice.Text.Rich.BorderLineStyle;

namespace PulseOffice.Sheets.UI.Exchange;

public sealed record LedgerCellsNative(Workbook Workbook, ClipboardSnapshot Snapshot, CellAddress Origin)
{
    public LedgerCut? Cut { get; init; }
}

public sealed class LedgerCut(Worksheet sheet, CellRange range)
{
    public Worksheet Sheet => sheet;

    public CellRange Range => range;

    public bool Consumed { get; set; }
}

public sealed record LedgerObjectsNative(IReadOnlyList<PlacedObject> Objects);

public interface ILedgerExchange : IExchangeParticipant
{
    bool Receive(ExchangePackage package, ExchangePasteMode mode, PasteRequest request);
}

public sealed record LedgerExchangeServices(
    IActiveSheetContext Context,
    ISheetEditGuard Guard,
    Shell.IDocumentWorkspace Workspace,
    IStatusBarService Status,
    ITextCatalog Texts,
    IHtmlTableReader Tables,
    Formats.Interchange.HtmlSpreadsheetWriter Html,
    ISheetRangeCapture Ranges,
    IChartDataExtractor ChartData,
    ISheetImageInsertion Images,
    PulseOffice.Sheets.Styling.IStyleTransplant Transplant,
    ISheetClipboard Sheets,
    IEmbeddedObjectPicture Pictures,
    PulseOffice.Sheets.Formatting.ISpreadsheetLocale Locale,
    ISheetRangeMove Moves,
    IWorkbookCheckpoints Checkpoints,
    IExchangeGridSnapshots Grids,
    ICellGridConversions Conversions);

public sealed class LedgerExchange(LedgerExchangeServices services) : ILedgerExchange
{
    public ModuleKind Module => ModuleKind.Sheets;

    public bool CanCapture => services.Context.Workbook is not null;

    public bool CanReceive => services.Context.Workbook is not null && services.Workspace.Active?.IsReadOnly != true;

    public ExchangePackage? Capture(string id, bool cut)
    {
        if (services.Context.Workbook is not { } workbook) return null;
        var sheet = workbook.Sheets[services.Context.SheetIndex];
        var objects = services.Context.SelectedObjects.Where(sheet.Objects.Contains).ToList();
        return objects.Count > 0 ? CaptureObjects(id, workbook, sheet, objects, cut) : CaptureCells(id, workbook, sheet, services.Context.Selection, cut);
    }

    public IReadOnlyList<ExchangePasteMode> Modes(ExchangePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var modes = new List<ExchangePasteMode>();
        if (package.NativeFrom<LedgerCellsNative>(Module) is not null || package.NativeFrom<LedgerObjectsNative>(Module) is not null) modes.Add(ExchangePasteMode.Native);
        if (package.Content is ObjectsContent && !modes.Contains(ExchangePasteMode.Native)) modes.Add(ExchangePasteMode.Objects);
        if (package.Content is CellRangeContent || package.TextOf(ExchangeFlavor.Html) is { } html && services.Tables.FirstTable(html) is not null || package.TextOf(ExchangeFlavor.Text) is { Length: > 0 })
            if (package.NativeFrom<LedgerCellsNative>(Module) is null) modes.Add(ExchangePasteMode.Cells);
        if (package.Find(ExchangeFlavor.Png) is not null && package.Content is null) modes.Add(ExchangePasteMode.Image);
        return modes;
    }

    public bool Receive(ExchangePackage package, ExchangePasteMode mode) => Receive(package, mode, PasteRequest.All);

    public bool Receive(ExchangePackage package, ExchangePasteMode mode, PasteRequest request)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(request);
        if (services.Context.Workbook is not { } workbook) return false;
        var sheet = workbook.Sheets[services.Context.SheetIndex];
        if (ReadOnly()) return false;

        switch (mode)
        {
            case ExchangePasteMode.Native when package.NativeFrom<LedgerObjectsNative>(Module) is { } placed:
                return PasteObjects(workbook, sheet, [.. placed.Objects.Select(item => new ExchangedObject(item.Content, item.Size))]);
            case ExchangePasteMode.Objects when package.Content is ObjectsContent content:
                return PasteObjects(workbook, sheet, content.Objects);
            case ExchangePasteMode.Image when package.BytesOf(ExchangeFlavor.Png) is { } png:
                return !Refused(workbook, sheet, services.Context.Selection) && services.Images.InsertRaster(png, Guid.NewGuid().ToString("N"), "image.png");
            case ExchangePasteMode.Native or ExchangePasteMode.Cells:
                return PasteCells(workbook, sheet, package, request);
            default:
                return false;
        }
    }

    private ExchangePackage? CaptureCells(string id, Workbook workbook, Worksheet sheet, CellRange range, bool cut)
    {
        if (cut && Refused(workbook, sheet, range)) return null;
        var snapshot = services.Sheets.Copy(sheet, range);
        var grid = services.Ranges.Grid(workbook, sheet, range);
        var package = new ExchangePackage
        {
            Id = id,
            Origin = Module,
            Content = grid,
            Native = new LedgerCellsNative(workbook, snapshot, range.Start) { Cut = cut ? new LedgerCut(sheet, range) : null },
            Representations =
            [
                ExchangeRepresentation.OfText(ExchangeFlavor.Text, services.Conversions.Tabular(grid)),
                ExchangeRepresentation.OfText(ExchangeFlavor.Html, services.Html.RenderFragment(sheet, services.Ranges.Occupied(sheet, range), workbook.Styles)),
                .. services.Ranges.Packages(workbook, sheet, range),
            ],
        };
        return package;
    }

    private ExchangePackage CaptureObjects(string id, Workbook workbook, Worksheet sheet, List<PlacedObject> objects, bool cut)
    {
        var exchanged = objects.Select(placed => new ExchangedObject(Portable(workbook, placed.Content), placed.Size)).ToList();
        var package = new ExchangePackage
        {
            Id = id,
            Origin = Module,
            Content = new ObjectsContent(exchanged),
            Native = new LedgerObjectsNative(objects),
            Representations = Pictures(exchanged),
        };
        if (!cut || ReadOnly()) return package;
        if (sheet.Protection.IsEnabled)
            services.Status.Report(services.Texts["status.protection.objects"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
        else RemoveObjects(sheet, objects);
        return package;
    }

    private IReadOnlyList<ExchangeRepresentation> Pictures(IReadOnlyList<ExchangedObject> objects) =>
        objects is [var single] && services.Pictures.Png(single.Object, single.Size, services.Locale.Culture) is { } png
            ? [new ExchangeRepresentation(ExchangeFlavor.Png, png)]
            : [];

    private Document.Embedding.EmbeddedObject Portable(Workbook workbook, Document.Embedding.EmbeddedObject content) =>
        content is ChartDefinition { InlineData: null } chart ? chart with { InlineData = services.ChartData.Extract(workbook, chart) } : content;

    private bool PasteCells(Workbook workbook, Worksheet sheet, ExchangePackage package, PasteRequest request)
    {
        var target = services.Context.Selection.Start;
        if (package.NativeFrom<LedgerCellsNative>(Module) is { Cut: { Consumed: false } cut } native
            && ReferenceEquals(native.Workbook, workbook) && workbook.IndexOf(cut.Sheet) >= 0)
            return MoveCells(workbook, sheet, cut, target);
        if (Snapshot(workbook, package, target) is not { } found || found.Snapshot.IsEmpty) return false;
        if (Refused(workbook, sheet, PasteArea(found.Snapshot, target, request))) return false;
        services.Sheets.Paste(found.Snapshot, sheet, target, found.Origin, request with { HistoryLabel = services.Texts["command.ledger.clipboard.paste.title"] }, services.Context.History);
        services.Context.RequestRefresh();
        return true;
    }

    private bool MoveCells(Workbook workbook, Worksheet sheet, LedgerCut cut, CellAddress target)
    {
        var move = new RangeMove(cut.Sheet.Name, cut.Range, sheet.Name, target);
        if (!move.Fits || !services.Moves.KeepsBlocksWhole(workbook, move))
        {
            services.Status.Report(services.Texts["status.cells.wouldSplit"], NotificationSeverity.Error);
            return false;
        }
        if (Refused(workbook, cut.Sheet, cut.Range) || Refused(workbook, sheet, move.TargetArea)) return false;
        if (services.Context.Engine is not { } engine) return false;

        var before = services.Checkpoints.Capture(workbook);
        services.Moves.Move(engine, move);
        var after = services.Checkpoints.Capture(workbook);
        services.Context.History?.Push(new DelegateAction(
            services.Texts["command.ledger.clipboard.cut.title"],
            redo: () => services.Checkpoints.Restore(engine, workbook, after),
            undo: () => services.Checkpoints.Restore(engine, workbook, before)));
        cut.Consumed = true;
        services.Context.RequestRefresh();
        return true;
    }

    private (ClipboardSnapshot Snapshot, CellAddress Origin)? Snapshot(Workbook workbook, ExchangePackage package, CellAddress target)
    {
        if (package.NativeFrom<LedgerCellsNative>(Module) is { } native)
            return (ReferenceEquals(native.Workbook, workbook) ? native.Snapshot : Restyled(native.Snapshot, native.Workbook, workbook), native.Origin);
        if (package.Content is CellRangeContent grid) return (services.Grids.FromGrid(grid, workbook), new CellAddress(grid.FirstRow, grid.FirstColumn));
        if (package.TextOf(ExchangeFlavor.Html) is { } html && services.Tables.FirstTable(html) is { } table) return (FromTable(table, workbook.Styles), target);
        if (package.TextOf(ExchangeFlavor.Text) is { Length: > 0 } text) return (services.Sheets.FromDelimitedText(text), target);
        return null;
    }

    private ClipboardSnapshot Restyled(ClipboardSnapshot snapshot, Workbook source, Workbook target) =>
        snapshot with
        {
            SourceSheet = null,
            Cells = [.. snapshot.Cells.Select(entry => (entry.Row, entry.Column, entry.Cell with
            {
                StyleId = target.Styles.Intern(services.Transplant.Carry(source.Styles.Get(entry.Cell.StyleId), source.NumberFormats, target.NumberFormats)),
            }))],
        };

    private ClipboardSnapshot FromTable(HtmlTable table, StyleCatalog styles)
    {
        var shift = table.Caption is null ? 0 : 1;
        var texts = new List<IReadOnlyList<string?>>();
        if (table.Caption is { } caption) texts.Add([caption]);
        texts.AddRange(table.Rows.Select(row => (IReadOnlyList<string?>)[.. row.Select(cell => cell?.Text)]));
        var values = services.Sheets.FromGrid(texts);
        var cells = values.Cells.Select(entry =>
        {
            if (entry.Row < shift || table.Rows[entry.Row - shift] is not { } row || entry.Column >= row.Count
                || row[entry.Column] is not { } source || Style(source) is not { } style) return entry;
            return (entry.Row, entry.Column, entry.Cell with { StyleId = styles.Intern(style), ExplicitStyle = true });
        }).ToList();
        var merges = table.Rows
            .SelectMany((row, index) => row.Select((cell, column) => (Cell: cell, Row: index + shift, Column: column)))
            .Where(entry => entry.Cell is { } cell && (cell.ColumnSpan > 1 || cell.RowSpan > 1))
            .Select(entry => new CellRange(
                new CellAddress(entry.Row, entry.Column),
                new CellAddress(entry.Row + entry.Cell!.RowSpan - 1, entry.Column + entry.Cell.ColumnSpan - 1)))
            .ToList();
        return values with { Cells = cells, Merges = merges };
    }

    private CellStyle? Style(HtmlTableCell cell)
    {
        if (!cell.Bold && !cell.Italic && !cell.Underline && cell.ColorHex is null && cell.BackgroundHex is null && cell.Borders is null
            && cell.Alignment == HtmlTextAlignment.Unspecified && cell.VerticalAlignment == HtmlVerticalAlignment.Unspecified) return null;
        var font = CellFont.Default with { Bold = cell.Bold, Italic = cell.Italic, Underline = cell.Underline, ColorHex = cell.ColorHex ?? CellFont.Default.ColorHex };
        return CellStyle.Default with { Font = font, BackgroundHex = cell.BackgroundHex, Borders = Borders(cell.Borders), Horizontal = Horizontal(cell.Alignment), Vertical = Vertical(cell.VerticalAlignment) };
    }

    private CellVerticalAlignment Vertical(HtmlVerticalAlignment alignment) => alignment switch
    {
        HtmlVerticalAlignment.Top => CellVerticalAlignment.Top,
        HtmlVerticalAlignment.Middle => CellVerticalAlignment.Middle,
        _ => CellVerticalAlignment.Bottom,
    };

    private CellHorizontalAlignment Horizontal(HtmlTextAlignment alignment) => alignment switch
    {
        HtmlTextAlignment.Left => CellHorizontalAlignment.Left,
        HtmlTextAlignment.Center => CellHorizontalAlignment.Center,
        HtmlTextAlignment.Right => CellHorizontalAlignment.Right,
        HtmlTextAlignment.Justify => CellHorizontalAlignment.Justify,
        _ => CellHorizontalAlignment.General,
    };

    private CellBorders Borders(HtmlCellEdges? edges) =>
        edges is null ? CellBorders.None : new CellBorders(Edge(edges.Left), Edge(edges.Top), Edge(edges.Right), Edge(edges.Bottom));

    private BorderEdge Edge(HtmlBorderLine? line)
    {
        if (line is null) return BorderEdge.None;
        var style = line.Style switch
        {
            BorderLineStyle.Dashed => BorderStyle.Dashed,
            BorderLineStyle.Dotted => BorderStyle.Dotted,
            BorderLineStyle.Double => BorderStyle.Double,
            _ => line.WidthPoints switch { >= 2.25 => BorderStyle.Thick, >= 1.5 => BorderStyle.Medium, _ => BorderStyle.Thin },
        };
        return new BorderEdge(style, new ArgbColor(line.Color).ToHashedHex(), Twip.FromPoints(line.WidthPoints));
    }

    private bool PasteObjects(Workbook workbook, Worksheet sheet, IReadOnlyList<ExchangedObject> objects)
    {
        if (objects.Count == 0 || Refused(workbook, sheet, services.Context.Selection)) return false;
        if (sheet.Protection.IsEnabled)
        {
            services.Status.Report(services.Texts["status.protection.objects"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return false;
        }
        var anchor = services.Context.Selection.Start;
        var top = sheet.Objects.Count == 0 ? 0 : sheet.Objects.Max(item => item.ZOrder);
        var placed = objects.Select((item, index) =>
        {
            var id = Guid.NewGuid().ToString("N");
            return new PlacedObject
            {
                Id = id,
                Content = item.Object with { Id = id },
                Anchor = anchor,
                OffsetX = Twip.FromPoints(12 * index),
                OffsetY = Twip.FromPoints(12 * index),
                Size = item.Size.IsEmpty ? item.Object.NaturalSize : item.Size,
                ZOrder = top + index + 1,
            };
        }).ToList();

        foreach (var item in placed) sheet.Objects.Add(item);
        services.Context.History?.Push(new DelegateAction(
            services.Texts["command.ledger.clipboard.paste.title"],
            redo: () => { foreach (var item in placed) sheet.Objects.Add(item); },
            undo: () => { foreach (var item in placed) sheet.Objects.Remove(item); }));
        services.Context.SelectObjects(placed);
        services.Context.RequestRefresh();
        return true;
    }

    private void RemoveObjects(Worksheet sheet, IReadOnlyList<PlacedObject> removed)
    {
        foreach (var placed in removed) sheet.Objects.Remove(placed);
        services.Context.History?.Push(new DelegateAction(
            services.Texts["command.ledger.clipboard.cut.title"],
            redo: () => { foreach (var placed in removed) sheet.Objects.Remove(placed); },
            undo: () => { foreach (var placed in removed) sheet.Objects.Add(placed); }));
        services.Context.SelectObject(null);
        services.Context.RequestRefresh();
    }

    private bool ReadOnly()
    {
        if (services.Workspace.Active?.IsReadOnly != true) return false;
        services.Status.Report(services.Texts["status.readOnly"], NotificationSeverity.Error, NotificationRemedies.EditMode);
        return true;
    }

    private bool Refused(Workbook workbook, Worksheet sheet, CellRange range)
    {
        if (ReadOnly()) return true;
        if (services.Guard.CanWrite(workbook, sheet, range)) return false;
        services.Status.Report(services.Texts["status.protection.locked"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
        return true;
    }

    private CellRange PasteArea(ClipboardSnapshot snapshot, CellAddress target, PasteRequest request)
    {
        var (rows, columns) = request.Transpose ? (snapshot.ColumnCount, snapshot.RowCount) : (snapshot.RowCount, snapshot.ColumnCount);
        return new CellRange(target, new CellAddress(
            Math.Min(target.Row + Math.Max(rows, 1) - 1, CellAddress.MaxRows - 1),
            Math.Min(target.Column + Math.Max(columns, 1) - 1, CellAddress.MaxColumns - 1)));
    }
}
