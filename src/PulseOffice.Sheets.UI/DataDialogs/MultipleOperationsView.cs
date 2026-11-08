using Avalonia.Controls;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed record MultipleOperationsChoice(string Formulas, string RowInput, string ColumnInput);

public interface IMultipleOperationsPreviewer
{
    CellRange Target { get; }

    MultipleOperationsRequest? Request(MultipleOperationsChoice choice);

    SketchPreview Preview(MultipleOperationsChoice choice);
}

public sealed class MultipleOperationsView : LedgerDialogView
{
    private readonly IMultipleOperationsPreviewer _previewer;
    private readonly TextBlock _summary;

    public MultipleOperationsView(LedgerDialogKit kit, IMultipleOperationsPreviewer previewer, IReferenceSource formulas, IReferenceSource cells)
        : base(kit, "operations.explanation")
    {
        _previewer = previewer ?? throw new ArgumentNullException(nameof(previewer));
        Formulas = new RangeFieldControl(new RangeField("formulas", kit["operations.formulas"], string.Empty, formulas) { Placeholder = "B1:B3" }, kit.Texts, kit.Metrics);
        RowInput = new RangeFieldControl(new RangeField("rowInput", kit["operations.rowInput"], string.Empty, cells) { Optional = true }, kit.Texts, kit.Metrics);
        ColumnInput = new RangeFieldControl(new RangeField("columnInput", kit["operations.columnInput"], string.Empty, cells) { Optional = true }, kit.Texts, kit.Metrics);
        Mode = kit.Choices("operations.mode",
        [
            ("column", "operations.mode.column", "operations.mode.column"),
            ("row", "operations.mode.row", "operations.mode.row"),
            ("both", "operations.mode.both", "operations.mode.both"),
        ], 0, showLabels: true);
        Sketch = new CellGridSketch(kit);
        _summary = kit.Note();

        Layout.Preview = PreviewPane("operations.preview", 260, Sketch, _summary);
        var target = Layout.AddSection(kit["operations.section.target"]);
        target.Add(null, kit.Note(kit.Texts.Format("operations.target", previewer.Target.ToString())), DialogRowKind.Wide);
        target.Add(kit["operations.formulas"], Formulas);
        var inputs = Layout.AddSection(kit["operations.section.inputs"]);
        inputs.Add(null, Mode, DialogRowKind.Wide);
        RowRow = inputs.Add(kit["operations.rowInput"], RowInput);
        ColumnRow = inputs.Add(kit["operations.columnInput"], ColumnInput);
        Finish("operations.confirm", inputs);

        Formulas.TextChanged += (_, _) => Refresh();
        RowInput.TextChanged += (_, _) => Refresh();
        ColumnInput.TextChanged += (_, _) => Refresh();
        Mode.SelectionChanged += (_, _) => Refresh();
        Refresh();
    }

    public RangeFieldControl Formulas { get; }

    public RangeFieldControl RowInput { get; }

    public RangeFieldControl ColumnInput { get; }

    public IconChoiceGroup Mode { get; }

    public DialogRow RowRow { get; }

    public DialogRow ColumnRow { get; }

    public CellGridSketch Sketch { get; }

    public MultipleOperationsChoice Choice => new(
        Formulas.Text,
        Mode.SelectedKey is "row" or "both" ? RowInput.Text : string.Empty,
        Mode.SelectedKey is "column" or "both" ? ColumnInput.Text : string.Empty);

    protected override object? Result() => Choice;

    private void Refresh()
    {
        RowRow.IsVisible = Mode.SelectedKey is "row" or "both";
        ColumnRow.IsVisible = Mode.SelectedKey is "column" or "both";
        var request = _previewer.Request(Choice);
        if (request is not { IsValid: true })
        {
            Sketch.Show([]);
            _summary.Text = string.Empty;
            Report(Kit["operations.invalid"]);
            return;
        }

        var preview = _previewer.Preview(Choice);
        Sketch.Show(preview.Rows);
        _summary.Text = preview.Summary;
        Report(preview.Problem);
    }
}

public sealed class EngineMultipleOperationsPreviewer(
    IMultipleOperations operations, Dialogs.ISheetReferenceReader reader, ISketchCells cells,
    Engine.Recalculation.CalculationEngine engine, int sheetIndex, CellRange target) : IMultipleOperationsPreviewer
{
    private const long MaximumFormulaCells = 1_000;

    public CellRange Target { get; } = target;

    public MultipleOperationsRequest? Request(MultipleOperationsChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        var workbook = engine.Workbook;
        var formulas = (reader.Read(workbook, choice.Formulas, sheetIndex, Dialogs.ReferenceShape.RangeList) ?? [])
            .Where(reference => reference.SheetIndex == sheetIndex && reference.Range.CellCount <= MaximumFormulaCells)
            .SelectMany(reference => reference.Range.Cells())
            .Select(address => new SheetCell(sheetIndex, address))
            .ToList();
        return new MultipleOperationsRequest
        {
            Target = Target,
            Formulas = formulas,
            RowInput = Cell(choice.RowInput),
            ColumnInput = Cell(choice.ColumnInput),
        };
    }

    public SketchPreview Preview(MultipleOperationsChoice choice)
    {
        if (Request(choice) is not { IsValid: true } request) return new SketchPreview([], cells.Texts["operations.invalid"]);
        var bounded = new CellRange(Target.Start, new CellAddress(
            Math.Min(Target.End.Row, Target.Start.Row + SketchCells.Rows - 1),
            Math.Min(Target.End.Column, Target.Start.Column + SketchCells.Columns - 1)));
        var computed = operations.Compute(engine, sheetIndex, request with { Target = bounded }).Values;
        var sheet = engine.Workbook.Sheets[sheetIndex];
        var shown = new Dictionary<CellAddress, CellValue>();
        foreach (var address in bounded.Cells()) shown[address] = computed.TryGetValue(address, out var value) ? value : sheet[address].Value;
        var written = Math.Max(0, Target.RowCount - (request.RowInput is null ? 0 : 1)) * Math.Max(0, Target.ColumnCount - (request.ColumnInput is null ? 0 : 1));
        return cells.From(shown, Target.Start, true, true, "operations.summary") with { Summary = cells.Texts.Format("operations.summary", written) };
    }

    private SheetCell? Cell(string text) =>
        reader.Read(engine.Workbook, text, sheetIndex, Dialogs.ReferenceShape.Cell) is [var reference] && reference.SheetIndex == sheetIndex
            ? new SheetCell(sheetIndex, reference.Range.Start)
            : null;
}
