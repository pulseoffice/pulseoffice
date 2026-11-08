using Avalonia.Controls;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Lists;
using PulseOffice.UI.Controls.References;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed record ConsolidateChoice(
    IReadOnlyList<string> Sources, string Destination, SubtotalFunction Function, bool RowLabels, bool ColumnLabels);

public sealed record ConsolidateLabels(SubtotalFunction Function, bool RowLabels, bool ColumnLabels);

public sealed record SketchPreview(IReadOnlyList<IReadOnlyList<SketchCell>> Rows, string? Problem, string Summary = "");

public interface IConsolidatePreviewer
{
    bool Accepts(string source);

    SketchPreview Preview(ConsolidateChoice choice);
}

public sealed class ConsolidateView : LedgerDialogView
{
    private readonly IConsolidatePreviewer _previewer;
    private readonly EditableList _sources;
    private readonly List<SubtotalFunction> _functions;
    private readonly TextBlock _summary;

    public ConsolidateView(
        LedgerDialogKit kit, IConsolidatePreviewer previewer, IReadOnlyList<string> sources, string destination,
        IReferencePicker areaPicker, IReferenceSource cells, ConsolidateLabels? initial = null)
        : base(kit, "consolidate.explanation")
    {
        _previewer = previewer ?? throw new ArgumentNullException(nameof(previewer));
        _functions =
        [
            SubtotalFunction.Sum, SubtotalFunction.Count, SubtotalFunction.Average, SubtotalFunction.Max,
            SubtotalFunction.Min, SubtotalFunction.Product, SubtotalFunction.CountNumbers,
            SubtotalFunction.StandardDeviation, SubtotalFunction.StandardDeviationPopulation,
            SubtotalFunction.Variance, SubtotalFunction.VariancePopulation,
        ];

        _sources = new EditableList(sources, unique: true, accepts: previewer.Accepts);
        Sources = new ListEditorControl(_sources, ListEditMode.Editable, kit.Texts, kit.Metrics, kit["consolidate.placeholder"],
            pick: new ListReferencePick(kit["consolidate.sources"], areaPicker, previewer.Accepts));
        Destination = new RangeFieldControl(new RangeField("destination", kit["consolidate.destination"], destination, cells), kit.Texts, kit.Metrics);
        Function = kit.Choices("consolidate.function", _functions.Select(function => (Key(function), "consolidate.fn." + Key(function), "subtotal." + Key(function))),
            Math.Max(0, _functions.IndexOf(initial?.Function ?? SubtotalFunction.Sum)), showLabels: true, columns: 2);
        RowLabels = kit.Check("consolidate.labelsRow", initial?.RowLabels ?? true);
        ColumnLabels = kit.Check("consolidate.labelsColumn", initial?.ColumnLabels ?? true);
        Sketch = new CellGridSketch(kit);
        _summary = kit.Note();

        Layout.Preview = PreviewPane("consolidate.preview", 260, Sketch, _summary);
        var areas = Layout.AddSection(kit["consolidate.section.sources"]);
        areas.Add(null, Sources, DialogRowKind.Wide);
        var how = Layout.AddSection(kit["consolidate.section.how"]);
        how.Add(kit["consolidate.function"], Function, alignTop: true);
        how.Add(null, RowLabels, DialogRowKind.Indented);
        how.Add(null, ColumnLabels, DialogRowKind.Indented);
        var where = Layout.AddSection(kit["consolidate.section.result"]);
        where.Add(kit["consolidate.destination"], Destination);
        Finish("consolidate.confirm", where);

        _sources.Changed += (_, _) => Refresh();
        Destination.TextChanged += (_, _) => Refresh();
        Function.SelectionChanged += (_, _) => Refresh();
        RowLabels.IsCheckedChanged += (_, _) => Refresh();
        ColumnLabels.IsCheckedChanged += (_, _) => Refresh();
        Refresh();
    }

    public ListEditorControl Sources { get; }

    public RangeFieldControl Destination { get; }

    public IconChoiceGroup Function { get; }

    public CheckBox RowLabels { get; }

    public CheckBox ColumnLabels { get; }

    public CellGridSketch Sketch { get; }

    public void AddSource(string source) => _sources.Add(source);

    public ConsolidateChoice Choice => new(
        [.. _sources.Items], Destination.Text, _functions[Math.Max(Function.SelectedIndex, 0)],
        RowLabels.IsChecked == true, ColumnLabels.IsChecked == true);

    protected override object? Result() => Choice;

    private void Refresh()
    {
        var problem = _sources.Count == 0 ? Kit["consolidate.noSources"]
            : !Destination.IsValid ? Kit["status.analysis.badReference"]
            : null;
        var preview = problem is null ? _previewer.Preview(Choice) : new SketchPreview([], problem);
        Sketch.Show(preview.Rows);
        _summary.Text = preview.Summary;
        Report(preview.Problem);
    }

    private string Key(SubtotalFunction function) => function switch
    {
        SubtotalFunction.Sum => "sum",
        SubtotalFunction.Count => "count",
        SubtotalFunction.Average => "average",
        SubtotalFunction.Max => "max",
        SubtotalFunction.Min => "min",
        SubtotalFunction.Product => "product",
        SubtotalFunction.CountNumbers => "countNumbers",
        SubtotalFunction.StandardDeviation => "standardDeviation",
        SubtotalFunction.StandardDeviationPopulation => "standardDeviationPopulation",
        SubtotalFunction.Variance => "variance",
        _ => "variancePopulation",
    };
}

public sealed class WorkbookConsolidatePreviewer(
    ISheetConsolidation consolidation, Dialogs.ISheetReferenceReader reader, ISketchCells cells, Workbook workbook, int sheetIndex)
    : IConsolidatePreviewer
{
    public bool Accepts(string source) => reader.Read(workbook, source, sheetIndex, Dialogs.ReferenceShape.Range) is [_];

    public SketchPreview Preview(ConsolidateChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        var areas = choice.Sources
            .Select(text => reader.Read(workbook, text, sheetIndex, Dialogs.ReferenceShape.Range)?[0])
            .OfType<SheetRangeReference>()
            .Select(reference => new ConsolidationSource(reference.SheetIndex, reference.Range))
            .ToList();
        if (areas.Count == 0) return new SketchPreview([], cells.Texts["consolidate.noSources"]);
        if (reader.Read(workbook, choice.Destination, sheetIndex, Dialogs.ReferenceShape.Cell) is not [{ SheetIndex: var target } destination] || target != sheetIndex)
            return new SketchPreview([], cells.Texts["status.analysis.badReference"]);

        var plan = consolidation.Plan(workbook, new ConsolidationRequest(areas, destination.Range.Start)
        {
            Function = choice.Function,
            LabelsInFirstRow = choice.RowLabels,
            LabelsInFirstColumn = choice.ColumnLabels,
        });

        return cells.From(plan.Values, destination.Range.Start, choice.RowLabels, choice.ColumnLabels, "consolidate.summary");
    }
}
