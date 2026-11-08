using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Formatting;

public readonly record struct TextExtent(double Width, double Height);

public interface ICellTextMeasure
{
    TextExtent Measure(string text, CellFont font, double? wrapWidth, int rotationDegrees);
}

public interface IRowAutoHeight
{
    bool Refresh(Workbook workbook, Worksheet sheet);

    bool RefreshAll(Workbook workbook);

    bool RefreshRows(Workbook workbook, Worksheet sheet, IReadOnlyCollection<int> rows);

    Twip? Measure(Workbook workbook, Worksheet sheet, int row);
}

public sealed class RowAutoHeight(ICellTextMeasure measure, ICellPresenter presenter) : IRowAutoHeight
{

    public bool RefreshAll(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var changed = false;
        foreach (var sheet in workbook.Sheets) changed |= Refresh(workbook, sheet);
        return changed;
    }

    public bool Refresh(Workbook workbook, Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(sheet);

        var layout = sheet.Layout;
        var measured = new Dictionary<int, double>();
        var lines = new Dictionary<CellFont, double>();

        foreach (var (address, cell) in sheet.NonEmptyCells())
        {
            if (Fixed(layout, address.Row)) continue;
            if (ContentHeight(workbook, sheet, address, cell, lines) is not { } height) continue;
            measured[address.Row] = Math.Max(measured.GetValueOrDefault(address.Row), height);
        }

        foreach (var run in layout.RowStyles.Runs)
        {
            var styleHeight = RowStyleHeight(workbook, run.Value);
            if (Target(layout, styleHeight) is null) continue;
            for (var row = run.First; row <= run.Last; row++)
            {
                if (Fixed(layout, row)) continue;
                measured[row] = Math.Max(measured.GetValueOrDefault(row), styleHeight);
            }
        }

        var rows = layout.AutomaticRowHeights.Keys.Concat(measured.Keys).Distinct().ToList();
        return Apply(layout, rows, measured);
    }

    public bool RefreshRows(Workbook workbook, Worksheet sheet, IReadOnlyCollection<int> rows)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0) return false;

        var layout = sheet.Layout;
        var wanted = rows.ToHashSet();
        var measured = new Dictionary<int, double>();
        var lines = new Dictionary<CellFont, double>();

        foreach (var (address, cell) in wanted.SelectMany(sheet.CellsInRow))
        {
            if (Fixed(layout, address.Row)) continue;
            if (ContentHeight(workbook, sheet, address, cell, lines) is not { } height) continue;
            measured[address.Row] = Math.Max(measured.GetValueOrDefault(address.Row), height);
        }

        foreach (var row in wanted)
        {
            if (Fixed(layout, row) || !layout.RowStyles.TryGetValue(row, out var styleId)) continue;
            measured[row] = Math.Max(measured.GetValueOrDefault(row), RowStyleHeight(workbook, styleId));
        }

        return Apply(layout, wanted, measured);
    }

    private double RowStyleHeight(Workbook workbook, int styleId) =>
        measure.Measure("Xg", workbook.Styles.Get(styleId).Font, null, 0).Height + CellBoxMetrics.VerticalPadding;

    private bool Apply(SheetLayout layout, IEnumerable<int> rows, IReadOnlyDictionary<int, double> measured)
    {
        var changed = false;
        foreach (var row in rows)
        {
            if (Fixed(layout, row)) continue;
            var target = measured.TryGetValue(row, out var pixels) ? Target(layout, pixels) : null;
            var current = layout.AutomaticRowHeights.TryGetValue(row, out var existing) ? existing : (Twip?)null;
            if (current == target) continue;
            layout.SetAutomaticRowHeight(row, target);
            changed = true;
        }

        return changed;
    }

    public Twip? Measure(Workbook workbook, Worksheet sheet, int row)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(sheet);

        var lines = new Dictionary<CellFont, double>();
        var tallest = sheet.CellsInRow(row)
            .Select(pair => ContentHeight(workbook, sheet, pair.Key, pair.Value, lines))
            .OfType<double>()
            .DefaultIfEmpty(0)
            .Max();

        return Target(sheet.Layout, tallest);
    }

    private bool Fixed(SheetLayout layout, int row) => !layout.IsRowAutomatic(row);

    private Twip? Target(SheetLayout layout, double pixels)
    {
        var twips = new Twip((int)Math.Ceiling(pixels / CellBoxMetrics.PixelsPerTwip));
        return twips.Value > layout.DefaultRowHeight.Value ? twips : null;
    }

    private double? ContentHeight(Workbook workbook, Worksheet sheet, CellAddress address, Cell cell, Dictionary<CellFont, double> lines)
    {
        var region = sheet.Merges.At(address);
        if (region is { } merged && (merged.Start != address || merged.End.Row != merged.Start.Row)) return null;

        var styleId = sheet.StyleIdAt(address);
        var style = workbook.Styles.Get(styleId);
        var straight = style.RotationDegrees % 360 == 0;
        if (straight && !style.WrapText && !cell.HasFormula && cell.Value.Kind is not ValueKind.Text) return LineHeight(style.Font, lines);
        if (straight && !style.WrapText && cell.Value.Kind is ValueKind.Text && SingleLine(cell.Value.AsText())) return LineHeight(style.Font, lines);

        var styled = cell with { StyleId = styleId };
        var width = Width(sheet.Layout, address, region);

        var presented = cell.Value.IsEmpty && !cell.HasFormula
            ? null
            : presenter.Present(styled, workbook.Styles, workbook.NumberFormats, width / (style.Font.Size * 0.6));
        var text = presented?.Text;

        if (string.IsNullOrEmpty(text)) return LineHeight(style.Font, lines);

        if (!straight)
            return measure.Measure(text, style.Font, null, style.RotationDegrees).Height + CellBoxMetrics.VerticalPadding;

        var available = Math.Max(width - (CellBoxMetrics.CellPadding + style.Indent * CellBoxMetrics.IndentStep) * 2, 1);
        var wrap = style.WrapText && presented is { AlignRight: false } ? available : (double?)null;
        if (wrap is null && SingleLine(text)) return LineHeight(style.Font, lines);
        return measure.Measure(text, style.Font, wrap, 0).Height + CellBoxMetrics.VerticalPadding;
    }

    private double LineHeight(CellFont font, Dictionary<CellFont, double> lines)
    {
        if (lines.TryGetValue(font, out var height)) return height;
        height = measure.Measure("Xg", font, null, 0).Height + CellBoxMetrics.VerticalPadding;
        lines[font] = height;
        return height;
    }

    private bool SingleLine(string text)
    {
        foreach (var character in text)
            if (character is '\n' or '\r' or '\u2028' or '\u2029' || character >= LatinLimit) return false;
        return true;
    }

    private const char LatinLimit = '\u0370';

    private double Width(SheetLayout layout, CellAddress address, CellRange? region)
    {
        var last = region?.End.Column ?? address.Column;
        var twips = 0L;
        for (var column = address.Column; column <= last; column++) twips += layout.ColumnWidth(column).Value;
        return twips * CellBoxMetrics.PixelsPerTwip;
    }
}

public sealed class RowAutoHeightTracker(IRowAutoHeight heights)
{
    private readonly Dictionary<Worksheet, HashSet<int>> _dirty = [];
    private readonly Dictionary<Worksheet, long> _signatures = [];
    private Workbook? _workbook;

    public void Attach(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        Detach();
        _workbook = workbook;
        foreach (var sheet in workbook.Sheets) Watch(sheet);
        heights.RefreshAll(workbook);
        foreach (var sheet in workbook.Sheets) _signatures[sheet] = Signature(sheet);
    }

    public void Detach()
    {
        foreach (var sheet in _signatures.Keys) Unwatch(sheet);
        _dirty.Clear();
        _signatures.Clear();
        _workbook = null;
    }

    public bool Flush()
    {
        if (_workbook is null) return false;

        var changed = false;
        foreach (var sheet in _workbook.Sheets)
        {
            var signature = Signature(sheet);
            if (!_signatures.TryGetValue(sheet, out var known))
            {
                Watch(sheet);
                changed |= heights.Refresh(_workbook, sheet);
            }
            else if (known != signature)
                changed |= heights.Refresh(_workbook, sheet);
            else if (_dirty.TryGetValue(sheet, out var rows) && rows.Count > 0)
                changed |= heights.RefreshRows(_workbook, sheet, rows);

            _signatures[sheet] = signature;
        }

        foreach (var gone in _signatures.Keys.Where(sheet => !_workbook.Sheets.Contains(sheet)).ToList())
        {
            Unwatch(gone);
            _signatures.Remove(gone);
        }

        _dirty.Clear();
        return changed;
    }

    private void Watch(Worksheet sheet)
    {
        Unwatch(sheet);
        sheet.CellTouched += Touched;
        sheet.Cleared += Reset;
    }

    private void Unwatch(Worksheet sheet)
    {
        sheet.CellTouched -= Touched;
        sheet.Cleared -= Reset;
    }

    private void Touched(Worksheet sheet, CellAddress address)
    {
        if (!_dirty.TryGetValue(sheet, out var rows)) _dirty[sheet] = rows = [];
        rows.Add(address.Row);
    }

    private void Reset(Worksheet sheet) => _signatures[sheet] = long.MinValue;

    private long Signature(Worksheet sheet) => HashCode.Combine(sheet.Layout.ShapeSignature(), sheet.Merges.Count);
}
