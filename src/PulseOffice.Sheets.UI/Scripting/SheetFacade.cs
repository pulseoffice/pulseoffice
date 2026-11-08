using PulseOffice.Scripting.Editor.Facades;
using System.Globalization;
using PulseOffice.Charts;
using PulseOffice.Document.Commands;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Scripting;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Localization;
using PulseOffice.Sheets.Engine.Localization;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.UI.Scripting;

public sealed class SheetFacade(
    IScriptDispatcher dispatcher,
    IActiveSheetContext context,
    IScriptOutput output,
    ISheetStructure structure,
    ISheetMerge merge,
    ISheetSort sorter,
    ISheetFilter filter,
    ISheetSnapshots snapshots,
    IFormulaLocalization localization,
    ITextCatalog texts,
    PulseOffice.Foundation.Identity.IUserIdentity identity,
    PulseOffice.Sheets.Formatting.ICellPresenter presenter,
    IScriptReferences references,
    IScriptColors colors,
    PulseOffice.Sheets.Engine.Parsing.IFormulaExpressionParser parser,
    IScriptProtection protection,
    ISheetEditGuard guard) : ScriptFacade(dispatcher)
{
    private const double UnclippedWidth = 255;

    public string Name => Read(() =>
        context.Workbook is { } workbook ? workbook.Sheets[context.SheetIndex].Name : string.Empty);

    public int Index => Read(() => context.SheetIndex);

    public string Selection => Read(() => context.Selection.ToString());

    public string UsedRange => Read(() =>
        Sheet()?.UsedRange() is { } used ? used.ToString() : string.Empty);

    public int ObjectCount => Read(() => Sheet()?.Objects.Count ?? 0);

    public string? Read(string reference) => Read(() =>
        Cell(reference) is { } cell && context.Workbook is { } workbook
            ? cell.Value.IsEmpty ? string.Empty : presenter.Present(cell, workbook.Styles, workbook.NumberFormats, UnclippedWidth).Text
            : null);

    public double Number(string reference) => Read(() =>
        Cell(reference)?.Value is { Kind: ValueKind.Number } value ? value.AsNumber() : double.NaN);

    public string? Formula(string reference) => Read(() => Cell(reference)?.Formula);

    public string? FormulaLocal(string reference) => Read(() =>
        Cell(reference)?.Formula is { } formula ? localization.ToDisplay(formula, texts.Language.ToCode()) : null);

    public void SetFormulaLocal(string reference, string formula)
    {
        ArgumentNullException.ThrowIfNull(formula);
        SetFormula(reference, localization.ToCanonical(formula.StartsWith('=') ? formula : "=" + formula, texts.Language.ToCode()));
    }

    public bool IsEmpty(string reference) => Read(() =>
        Cell(reference) is not { } cell || (cell.Value.IsEmpty && !cell.HasFormula));

    public void Write(string reference, object? value) =>
        Change(() =>
        {
            protection.Cells(CellRange.Single(references.Address(reference)));
            Tracked(reference, () => WriteCore(reference, value));
        });

    public void SetFormula(string reference, string formula) => Change(() => Tracked(reference, () =>
    {
        if (string.IsNullOrWhiteSpace(formula) || formula.Trim() == "=") throw new ArgumentException("The formula is empty.", nameof(formula));
        var address = references.Address(reference);
        var written = Checked(formula.StartsWith('=') ? formula : "=" + formula);
        protection.Cells(CellRange.Single(address));
        if (context.Engine is not { } engine) return;

        engine.SetFormula(new SheetCell(context.SheetIndex, address), written);

        context.RequestRefresh();
    }));

    public string?[][] Range(string reference) => Read(() =>
    {
        var range = references.Range(reference);
        if (Sheet() is not { } sheet) return [];

        range = Bounded(range);
        var rows = new List<string?[]>();

        for (var row = range.Start.Row; row <= range.End.Row; row++)
        {
            var line = new string?[range.End.Column - range.Start.Column + 1];

            for (var column = range.Start.Column; column <= range.End.Column; column++)
                line[column - range.Start.Column] = sheet[new CellAddress(row, column)].Value.ToString();

            rows.Add(line);
        }

        return rows.ToArray();
    });

    public double[][] Numbers(string reference) => Read(() =>
    {
        var range = references.Range(reference);
        if (Sheet() is not { } sheet) return [];

        range = Bounded(range);
        var rows = new List<double[]>();

        for (var row = range.Start.Row; row <= range.End.Row; row++)
        {
            var line = new double[range.End.Column - range.Start.Column + 1];

            for (var column = range.Start.Column; column <= range.End.Column; column++)
            {
                var value = sheet[new CellAddress(row, column)].Value;

                line[column - range.Start.Column] =
                    value.Kind == ValueKind.Number ? value.AsNumber() : double.NaN;
            }

            rows.Add(line);
        }

        return rows.ToArray();
    });

    public void WriteRange(string reference, object?[][] values) => Edit("WriteRange", () =>
    {
        var range = references.Range(reference);
        if (values is not { Length: > 0 }) throw new ArgumentException("There are no values to write.", nameof(values));

        var width = values.Max(line => line?.Length ?? 0);
        if (range.Start.Row + values.Length > CellAddress.MaxRows || range.Start.Column + width > CellAddress.MaxColumns)
            throw new ArgumentException($"The values do not fit in the sheet from {range.Start.ToA1()}.");
        if ((long)values.Length * width > MaxCells)
            throw new ArgumentException($"More than {MaxCells} values.");
        foreach (var text in values.Where(line => line is not null).SelectMany(line => line).OfType<string>())
            if (text.StartsWith('=') && text.Length > 1) Checked(text);
            else Fitting(text);
        protection.Cells(new CellRange(range.Start, new CellAddress(range.Start.Row + values.Length - 1, range.Start.Column + Math.Max(width, 1) - 1)));

        for (var row = 0; row < values.Length; row++)
        {
            if (values[row] is not { } line) continue;

            for (var column = 0; column < line.Length; column++)
                WriteCore(
                    new CellAddress(range.Start.Row + row, range.Start.Column + column).ToA1(),
                    line[column]);
        }

        context.RequestRefresh();
    });

    public void Clear(string reference) => Edit("Clear", () =>
    {
        var range = references.Range(reference);
        protection.Cells(range);
        if (context.Engine is not { } engine) return;

        foreach (var address in Bounded(range).Cells())
            engine.SetValue(new SheetCell(context.SheetIndex, address), CellValue.Empty);

        context.RequestRefresh();
    });

    public void Select(string reference) => Show(() =>
    {
        context.SelectRange(references.Range(reference));
    });

    public void Recalculate() => Change(() =>
    {
        context.Engine?.RecalculateAll();
        context.RequestRefresh();
    });

    public void Log(object? message) => output.Write(message?.ToString() ?? string.Empty);

    public void Bold(string reference, bool on = true) =>
        Style(reference, style => style with { Font = style.Font with { Bold = on } });

    public void Italic(string reference, bool on = true) =>
        Style(reference, style => style with { Font = style.Font with { Italic = on } });

    public void Underline(string reference, bool on = true) =>
        Style(reference, style => style with { Font = style.Font with { Underline = on } });

    public void FontSize(string reference, double points) =>
        Style(reference, style => style with { Font = style.Font with { Size = Points(points) } });

    public void FontName(string reference, string family) =>
        Style(reference, style => style with { Font = style.Font with { Family = Family(family) } });

    public void Color(string reference, string hex) =>
        Style(reference, style => style with { Font = style.Font with { ColorHex = colors.Hex(hex) } });

    public void Background(string reference, string? hex) =>
        Style(reference, style => style with { BackgroundHex = colors.OptionalHex(hex) });

    public void Align(string reference, string alignment)
    {
        var horizontal = (alignment ?? string.Empty).ToLowerInvariant() switch
        {
            "general" or "generale" => CellHorizontalAlignment.General,
            "left" or "sinistra" => CellHorizontalAlignment.Left,
            "center" or "centre" or "centro" => CellHorizontalAlignment.Center,
            "right" or "destra" => CellHorizontalAlignment.Right,
            "justify" or "giustificato" => CellHorizontalAlignment.Justify,
            _ => throw new ArgumentException(
                $"Unknown alignment '{alignment}': use general, left, center, right or justify.", nameof(alignment)),
        };
        Style(reference, style => style with { Horizontal = horizontal });
    }

    public void NumberFormat(string reference, int formatId)
    {
        if (!Read(() => context.Workbook is not { } workbook
                || Formatting.NumberFormatCatalog.BuiltInFormats.ContainsKey(formatId)
                || workbook.NumberFormats.CustomFormats.Values.Contains(formatId)))
            throw new ArgumentOutOfRangeException(nameof(formatId), formatId, "There is no number format with this identifier.");
        Style(reference, style => style with { NumberFormatId = formatId });
    }

    public void Wrap(string reference, bool on = true) =>
        Style(reference, style => style with { WrapText = on });

    public void StyleName(string reference, string name) => Edit("StyleName", () =>
    {
        var range = references.Range(reference);
        protection.Format();
        if (context.Workbook is not { } workbook || Sheet() is not { } sheet) return;
        if (workbook.NamedStyles.All.FirstOrDefault(style => style.Name == name) is not { } named)
            throw new ArgumentException(
                $"There is no cell style '{name}': the styles are {string.Join(", ", workbook.NamedStyles.All.Select(style => style.Name))}.", nameof(name));

        var style = workbook.NamedStyles.StyleOf(named);
        var identifier = workbook.Styles.Intern(style);

        foreach (var address in Bounded(range).Cells())
        {
            sheet.SetStyle(address, identifier);
            sheet.SetStyleName(address, name);
        }

        context.RequestRefresh();
    });

    public void InsertRows(int row, int count = 1) => Structural(rules => rules.AllowInsertRows, "inserting rows",
        engine => structure.InsertRows(engine, context.SheetIndex, RowIndex(row), Span(row, count, CellAddress.MaxRows)));

    public void DeleteRows(int row, int count = 1) => Structural(rules => rules.AllowDeleteRows, "deleting rows",
        engine => structure.DeleteRows(engine, context.SheetIndex, RowIndex(row), Span(row, count, CellAddress.MaxRows)));

    public void InsertColumns(int column, int count = 1) => Structural(rules => rules.AllowInsertColumns, "inserting columns",
        engine => structure.InsertColumns(engine, context.SheetIndex, ColumnIndex(column), Span(column, count, CellAddress.MaxColumns)));

    public void DeleteColumns(int column, int count = 1) => Structural(rules => rules.AllowDeleteColumns, "deleting columns",
        engine => structure.DeleteColumns(engine, context.SheetIndex, ColumnIndex(column), Span(column, count, CellAddress.MaxColumns)));

    public void RowHeight(int row, double millimetres) => Edit("RowHeight", () =>
    {
        protection.Format();
        if (Sheet() is not { } sheet) return;

        sheet.Layout.SetRowHeight(RowIndex(row), Twip.FromMillimeters(Measure(millimetres)));
        context.RequestRefresh();
    });

    public void ColumnWidth(int column, double millimetres) => Edit("ColumnWidth", () =>
    {
        protection.Format();
        if (Sheet() is not { } sheet) return;

        sheet.Layout.SetColumnWidth(ColumnIndex(column), Twip.FromMillimeters(Measure(millimetres)));
        context.RequestRefresh();
    });

    public void HideRows(int row, int count = 1) => Edit("HideRows", () =>
    {
        protection.Format();
        if (Sheet() is not { } sheet) return;

        if (Span(row, count, CellAddress.MaxRows) is var hidden and > 0) sheet.Layout.HideRows(RowIndex(row), RowIndex(row) + hidden - 1);

        context.RequestRefresh();
    });

    public void ShowRows(int row, int count = 1) => Edit("ShowRows", () =>
    {
        protection.Format();
        if (Sheet() is not { } sheet) return;

        if (Span(row, count, CellAddress.MaxRows) is var shown and > 0) sheet.Layout.ShowRows(RowIndex(row), RowIndex(row) + shown - 1);

        context.RequestRefresh();
    });

    public void Freeze(int rows, int columns) =>
        Show(() =>
        {
            if (rows < 0 || rows >= CellAddress.MaxRows) throw new ArgumentOutOfRangeException(nameof(rows), rows, "The frozen rows must be 0 or more.");
            if (columns < 0 || columns >= CellAddress.MaxColumns) throw new ArgumentOutOfRangeException(nameof(columns), columns, "The frozen columns must be 0 or more.");
            context.FreezePanes(rows, columns);
        });

    public bool Merge(string reference) => Recorded("Merge", () =>
    {
        if (context.Workbook is not { } workbook || Sheet() is not { } sheet) return false;
        var range = references.Range(reference);
        protection.Format();
        protection.Cells(range);
        if (!merge.CanMerge(sheet, range)) return false;

        merge.Merge(workbook, sheet, range, MergeKind.MergeAndCentre);
        context.RequestRefresh();

        return true;
    });

    public bool Unmerge(string reference) => Recorded("Unmerge", () =>
    {
        if (Sheet() is not { } sheet) return false;
        var range = references.Range(reference);
        protection.Format();
        if (!merge.CanUnmerge(sheet, range)) return false;

        merge.Unmerge(sheet, range);
        context.RequestRefresh();

        return true;
    });

    public void Sort(string reference, int column, bool ascending = true, bool hasHeader = false) =>
        Change(() =>
        {
            var range = references.Range(reference);
            if (Sheet() is not { } sheet) return;
            if (column < 1 || column > range.End.Column - range.Start.Column + 1)
                throw new ArgumentOutOfRangeException(nameof(column), column, "The sort column is outside the range.");
            protection.Permitted(rules => rules.AllowSort, "sorting");
            protection.Cells(range);

            sorter.Sort(
                sheet,
                range,
                [new Engine.Operations.SortKey(column - 1, ascending)],
                hasHeader,
                context.History,
                texts["history.ledger.sort"]);

            context.RequestRefresh();
        });

    public string[] DistinctValues(string reference, int column, bool hasHeader = true) =>
        Read<string[]>(() =>
            Sheet() is { } sheet && references.Range(reference) is var range
                ? [.. filter.DistinctValues(sheet, range, Within(range, column) - 1, hasHeader)]
                : []);

    public string[] Find(string text, bool inFormulas = false) => Read<string[]>(() =>
    {
        if (Sheet() is not { } sheet || string.IsNullOrEmpty(text)) return [];

        var found = new List<string>();

        foreach (var (address, cell) in sheet.NonEmptyCells())
        {
            var haystack = inFormulas && cell.HasFormula ? cell.Formula! : cell.Value.ToString();

            if (haystack?.Contains(text, StringComparison.CurrentCultureIgnoreCase) == true)
                found.Add(address.ToA1());
        }

        return [.. found];
    });

    public int Replace(string text, string replacement) => Write(() =>
    {
        if (context.Engine is not { } engine || Sheet() is not { } sheet) return 0;
        if (string.IsNullOrEmpty(text)) return 0;

        var replaced = new List<(CellAddress Address, string Text)>();

        foreach (var (address, cell) in sheet.NonEmptyCells())
        {
            if (cell.HasFormula || cell.Value.Kind != ValueKind.Text) continue;
            if (context.Workbook is { } protectedBook && !guard.CanWrite(protectedBook, sheet, CellRange.Single(address))) continue;

            var current = cell.Value.AsText();

            if (!current.Contains(text, StringComparison.CurrentCulture)) continue;

            replaced.Add((address, Fitting(current.Replace(text, replacement, StringComparison.CurrentCulture))));
        }

        if (replaced.Count == 0) return 0;

        var before = snapshots.Capture(sheet);

        foreach (var (address, result) in replaced)
            engine.SetValue(new SheetCell(context.SheetIndex, address), CellValue.Text(result));

        Remember(before, "Replace");

        return replaced.Count;
    });

    public void Comment(string reference, string text) => Edit("Comment", () =>
    {
        var address = references.Address(reference);
        protection.Cells(CellRange.Single(address));
        if (Sheet() is not { } sheet) return;

        if (string.IsNullOrEmpty(text)) sheet.Comments.Remove(address);
        else sheet.Comments[address] = new CellComment { Text = Fitting(text), Author = identity.Name };

        context.RequestRefresh();
    });

    public string AddChart(string reference, string kind, string anchor) => Write(() =>
    {
        var data = references.Range(reference);
        var at = references.Address(anchor);
        protection.Unprotected("adding charts");
        if (Sheet() is not { } sheet) return string.Empty;

        var chartType = Kind<ChartType>(kind, nameof(kind));

        var identifier = Guid.NewGuid().ToString("N");

        var placed = new PlacedObject
        {
            Id = identifier,
            Anchor = at,
            Size = new DocumentSize(Twip.FromCentimeters(10), Twip.FromCentimeters(6)),
            Content = new ChartDefinition
            {
                Id = identifier,
                Type = chartType,
                SourceSheet = sheet.Name,
                SourceRange = data.ToString(),
            },
        };

        sheet.Objects.Add(placed);

        context.History?.Push(new DelegateAction(
            "AddChart",
            redo: () => sheet.Objects.Add(placed),
            undo: () => sheet.Objects.Remove(placed)));

        context.RequestRefresh();

        return identifier;
    });

    public string AddShape(string kind, string anchor, double widthMm = 40, double heightMm = 25) =>
        Write(() =>
        {
            var at = references.Address(anchor);
            protection.Unprotected("adding shapes");
            if (Sheet() is not { } sheet) return string.Empty;

            var shapeKind = Kind<ShapeKind>(kind, nameof(kind));
            var width = Positive(widthMm, nameof(widthMm));
            var height = Positive(heightMm, nameof(heightMm));

            var identifier = Guid.NewGuid().ToString("N");

            var placed = new PlacedObject
            {
                Id = identifier,
                Anchor = at,
                Size = new DocumentSize(
                    Twip.FromMillimeters(width), Twip.FromMillimeters(height)),
                Content = new ShapeObject { Id = identifier, Shape = shapeKind },
            };

            sheet.Objects.Add(placed);

            context.History?.Push(new DelegateAction(
                "AddShape",
                redo: () => sheet.Objects.Add(placed),
                undo: () => sheet.Objects.Remove(placed)));

            context.RequestRefresh();

            return identifier;
        });

    public bool RemoveObject(string id) => Write(() =>
    {
        protection.Unprotected("removing objects");
        if (Sheet() is not { } sheet) return false;
        if (sheet.Objects.FirstOrDefault(placed => placed.Id == id) is not { } found) return false;

        var index = sheet.Objects.IndexOf(found);

        sheet.Objects.Remove(found);

        context.History?.Push(new DelegateAction(
            "RemoveObject",
            redo: () => sheet.Objects.Remove(found),
            undo: () => sheet.Objects.Insert(index, found)));

        context.RequestRefresh();

        return true;
    });

    private Worksheet? Sheet() =>
        context.Workbook is { } workbook ? workbook.Sheets[context.SheetIndex] : null;

    private Cell? Cell(string reference)
    {
        var address = references.Address(reference);
        return Sheet() is { } sheet ? sheet[address] : null;
    }

    private void WriteCore(string reference, object? value)
    {
        var address = references.Address(reference);
        if (context.Engine is not { } engine) return;

        var cell = new SheetCell(context.SheetIndex, address);

        switch (value)
        {
            case null:
                engine.SetValue(cell, CellValue.Empty);
                break;

            case string text when text.StartsWith('=') && text.Length > 1:
                engine.SetFormula(cell, Checked(text));
                break;

            case string text:
                engine.SetValue(cell, CellValue.Text(Fitting(text)));
                break;

            case bool flag:
                engine.SetValue(cell, CellValue.Boolean(flag));
                break;

            default:
                engine.SetValue(cell, CellValue.Number(
                    Convert.ToDouble(value, CultureInfo.InvariantCulture)));
                break;
        }

        context.RequestRefresh();
    }

    private void Style(string reference, Func<CellStyle, CellStyle> change) =>
        Change(() =>
        {
            var range = references.Range(reference);
            protection.Format();
            if (Sheet() is not { } sheet || context.Workbook is not { } workbook) return;

            var before = snapshots.Capture(sheet);

            foreach (var address in Bounded(range).Cells())
                sheet.SetStyle(address, workbook.Styles.Derive(sheet.StyleIdAt(address), change));

            Remember(before, "Format");

            context.RequestRefresh();
        });

    private void Structural(Func<SheetProtection, bool> permission, string action, Action<Engine.Recalculation.CalculationEngine> change) =>
        Change(() =>
        {
            protection.Permitted(permission, action);
            if (context.Engine is not { } engine || Sheet() is not { } sheet) return;

            var before = snapshots.Capture(sheet);

            change(engine);

            Remember(before, "Structure");

            context.RequestRefresh();
        });

    private void Tracked(string reference, Action change)
    {
        var address = references.Address(reference);
        if (context.Engine is not { } engine || Sheet() is not { } sheet)
        {
            change();
            return;
        }

        var target = new SheetCell(context.SheetIndex, address);
        var before = sheet[address];
        change();
        var after = sheet[address];
        if (before == after) return;

        context.History?.Push(new DelegateAction(
            "Write",
            redo: () => Put(engine, target, after),
            undo: () => Put(engine, target, before)));
    }

    private void Put(Engine.Recalculation.CalculationEngine engine, SheetCell target, Cell cell)
    {
        if (cell.HasFormula) engine.SetFormula(target, cell.Formula!);
        else engine.SetValue(target, cell.Value);
        context.RequestRefresh();
    }

    private void Edit(string label, Action change) => Change(() =>
    {
        if (Sheet() is not { } sheet)
        {
            change();
            return;
        }

        var before = snapshots.Capture(sheet);
        change();
        Remember(before, label);
    });

    private CellRange Bounded(CellRange range)
    {
        var count = (long)(range.End.Row - range.Start.Row + 1) * (range.End.Column - range.Start.Column + 1);
        if (count > MaxCells) throw new ArgumentException($"The range {range} has more than {MaxCells} cells.");
        return range;
    }

    private const long MaxCells = 1_000_000;

    private int RowIndex(int row) => row is >= 1 and <= CellAddress.MaxRows
        ? row - 1
        : throw new ArgumentOutOfRangeException(nameof(row), row, $"Rows go from 1 to {CellAddress.MaxRows}.");

    private int ColumnIndex(int column) => column is >= 1 and <= CellAddress.MaxColumns
        ? column - 1
        : throw new ArgumentOutOfRangeException(nameof(column), column, $"Columns go from 1 to {CellAddress.MaxColumns}.");

    private int Span(int start, int count, int limit) => count >= 1
        ? Math.Min(count, limit - start + 1)
        : throw new ArgumentOutOfRangeException(nameof(count), count, "The count must be 1 or more.");

    private int Within(CellRange range, int column) => column >= 1 && column <= range.End.Column - range.Start.Column + 1
        ? column
        : throw new ArgumentOutOfRangeException(nameof(column), column, $"The column must be between 1 and {range.End.Column - range.Start.Column + 1}.");

    private string Checked(string formula)
    {
        try
        {
            parser.Parse(formula);
            return formula;
        }
        catch (PulseOffice.Sheets.Engine.Parsing.FormulaSyntaxException failure)
        {
            throw new ArgumentException($"'{formula}' is not a valid formula: {failure.Message}.", nameof(formula), failure);
        }
    }

    private string Fitting(string text) => PulseOffice.Sheets.Engine.Evaluation.Functions.ResultLimits.TextFits(text.Length)
        ? text
        : throw new ArgumentException($"The text has {text.Length} characters: a cell holds at most {PulseOffice.Sheets.Engine.Evaluation.Functions.ResultLimits.MaximumTextLength}.", nameof(text));

    private TEnum Kind<TEnum>(string kind, string parameter) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(kind, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) && !int.TryParse(kind, out _)
            ? parsed
            : throw new ArgumentException($"Unknown kind '{kind}': use one of {string.Join(", ", Enum.GetNames<TEnum>())}.", parameter);

    private const double MaximumShapeMillimetres = 5000;

    private double Positive(double value, string parameter) => double.IsFinite(value) && value is > 0 and <= MaximumShapeMillimetres
        ? value
        : throw new ArgumentOutOfRangeException(parameter, value, $"The measure must be greater than zero and at most {MaximumShapeMillimetres} millimetres.");

    private double Points(double points) => double.IsFinite(points) && points is >= 1 and <= 409
        ? points
        : throw new ArgumentOutOfRangeException(nameof(points), points, "The font size must be between 1 and 409 points.");

    private string Family(string family) => !string.IsNullOrWhiteSpace(family)
        ? family.Trim()
        : throw new ArgumentException("The font family is empty.", nameof(family));

    private const double MaximumMeasureMillimetres = 500;

    private double Measure(double millimetres) => double.IsFinite(millimetres) && millimetres is >= 0 and <= MaximumMeasureMillimetres
        ? millimetres
        : throw new ArgumentOutOfRangeException(nameof(millimetres), millimetres, $"The measure must be between 0 and {MaximumMeasureMillimetres} millimetres.");

    private bool Recorded(string label, Func<bool> change)
    {
        var done = false;
        Edit(label, () => done = change());
        return done;
    }

    private void Remember(SheetSnapshot before, string label)
    {
        if (context.Engine is not { } engine || Sheet() is not { } sheet) return;

        var after = snapshots.Capture(sheet);
        var index = context.SheetIndex;

        context.History?.Push(new DelegateAction(
            label,
            redo: () => snapshots.Restore(engine, index, after),
            undo: () => snapshots.Restore(engine, index, before)));
    }
}
