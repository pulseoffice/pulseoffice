using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed record AdvancedFilterChoice(string Data, string Criteria, bool Unique, string Output);

public sealed record AdvancedFilterProbe(IReadOnlyList<IReadOnlyList<string>> CriteriaRows, int? Passing, int Total);

public interface IAdvancedFilterProbe
{
    AdvancedFilterProbe Probe(AdvancedFilterChoice choice);
}

public sealed class AdvancedFilterView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private const int ShownCriteriaRows = 5;

    private readonly LedgerDialogKit _kit;
    private readonly IAdvancedFilterProbe _probe;
    private readonly Grid _criteriaTable = new();
    private readonly TextBlock _count;
    private readonly DialogRow _outputRow;

    public AdvancedFilterView(LedgerDialogKit kit, IAdvancedFilterProbe probe, string data, IReferenceSource ranges)
        : this(kit, probe, new AdvancedFilterChoice(data, string.Empty, false, string.Empty), null, ranges)
    {
    }

    public AdvancedFilterView(LedgerDialogKit kit, IAdvancedFilterProbe probe, AdvancedFilterChoice initial, string? notice, IReferenceSource ranges)
    {
        ArgumentNullException.ThrowIfNull(initial);
        _kit = kit ?? throw new ArgumentNullException(nameof(kit));
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));

        Data = new RangeFieldControl(new RangeField("data", kit["advancedFilter.data"], initial.Data, ranges), kit.Texts, kit.Metrics);
        Criteria = new RangeFieldControl(new RangeField("criteria", kit["advancedFilter.criteria"], initial.Criteria, ranges), kit.Texts, kit.Metrics);
        Output = new RangeFieldControl(new RangeField("output", kit["advancedFilter.output"], initial.Output, ranges) { Optional = true }, kit.Texts, kit.Metrics);
        Action = kit.Choices("advancedFilter.action",
        [
            ("inPlace", "ledger.data.advancedFilter", "advancedFilter.inPlace"),
            ("copy", "sort.level.copy", "advancedFilter.copyTo"),
        ], initial.Output.Length > 0 ? 1 : 0, showLabels: true);
        Unique = kit.Check("advancedFilter.unique", initial.Unique);
        Notice = notice is null ? null : kit.Note(notice);
        _count = kit.Note();
        _count.FontWeight = FontWeight.SemiBold;

        var layout = new TwoPaneDialogLayout(kit.Metrics)
        {
            Header = Notice is null
                ? kit.Help("advancedFilter.explanation")
                : new StackPanel { Spacing = kit.Metrics.SpacingSmall, Children = { kit.Help("advancedFilter.explanation"), Notice } },
            Preview = new StackPanel
            {
                Width = 240,
                Spacing = kit.Metrics.SpacingSmall,
                Children =
                {
                    kit.Caption("advancedFilter.preview"),
                    new Border { HorizontalAlignment = HorizontalAlignment.Left, Child = _criteriaTable }.Themed(Border.BackgroundProperty, Palette.Keys.Surface),
                    kit.Note(kit["advancedFilter.rowsHint"]),
                    _count,
                },
            },
        };
        var ranges2 = layout.AddSection(kit["advancedFilter.section.ranges"]);
        ranges2.Add(kit["advancedFilter.data"], Data);
        ranges2.Add(kit["advancedFilter.criteria"], Criteria);
        var result = layout.AddSection(kit["advancedFilter.section.result"]);
        result.Add(null, Action, DialogRowKind.Wide);
        _outputRow = result.Add(kit["advancedFilter.output"], Output);
        result.Add(null, Unique, DialogRowKind.Indented);
        Layout = layout;
        Content = layout;

        Data.TextChanged += (_, _) => Refresh();
        Criteria.TextChanged += (_, _) => Refresh();
        Output.TextChanged += (_, _) => Refresh();
        Unique.IsCheckedChanged += (_, _) => Refresh();
        Action.SelectionChanged += (_, _) => Refresh();

        Footer = new DialogFooter()
            .WithCancel(kit["dialog.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(kit["advancedFilter.confirm"], () => Completed?.Invoke(this, Choice), out var confirm, primary: true);
        Confirm = confirm;
        Refresh();
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public double? PreferredWidth => Layout.SuggestedWidth;

    public TwoPaneDialogLayout Layout { get; }

    public TextBlock? Notice { get; }

    public RangeFieldControl Data { get; }

    public RangeFieldControl Criteria { get; }

    public RangeFieldControl Output { get; }

    public IconChoiceGroup Action { get; }

    public CheckBox Unique { get; }

    public Button Confirm { get; }

    public string CountText => _count.Text ?? string.Empty;

    public int CriteriaCells => _criteriaTable.Children.Count;

    public AdvancedFilterChoice Choice => new(Data.Text, Criteria.Text, Unique.IsChecked == true, Action.SelectedIndex == 1 ? Output.Text : string.Empty);

    private void Refresh()
    {
        _outputRow.IsVisible = Action.SelectedIndex == 1;
        var choice = Choice;
        var probe = _probe.Probe(choice);
        ShowCriteria(probe.CriteriaRows);
        _count.Text = probe.Passing is { } passing
            ? _kit.Texts.Format("advancedFilter.passing", passing, probe.Total)
            : _kit["advancedFilter.incomplete"];
        Confirm.IsEnabled = probe.Passing is not null && (Action.SelectedIndex == 0 || Output.Text.Length > 0 && Output.IsValid);
    }

    private void ShowCriteria(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        _criteriaTable.Children.Clear();
        _criteriaTable.RowDefinitions.Clear();
        _criteriaTable.ColumnDefinitions.Clear();
        if (rows.Count == 0) return;
        var columns = rows.Max(row => row.Count);
        for (var column = 0; column < columns; column++) _criteriaTable.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        foreach (var (row, index) in rows.Take(ShownCriteriaRows).Select((row, index) => (row, index)))
        {
            _criteriaTable.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var column = 0; column < row.Count; column++)
            {
                var cell = new Border
                {
                    MinWidth = 56,
                    Padding = new Thickness(4, 2),
                    BorderThickness = new Thickness(0.5),
                    Child = new TextBlock
                    {
                        Text = row[column],
                        FontSize = _kit.Metrics.FontSizeSmall,
                        FontWeight = index == 0 ? FontWeight.SemiBold : FontWeight.Normal,
                    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary),
                }
                    .Themed(Border.BorderBrushProperty, Palette.Keys.Border)
                    .Themed(Border.BackgroundProperty, index == 0 ? Palette.Keys.SurfaceSunken : Palette.Keys.Surface);
                Grid.SetRow(cell, index);
                Grid.SetColumn(cell, column);
                _criteriaTable.Children.Add(cell);
            }
        }
    }
}

public sealed class SheetAdvancedFilterProbe(
    Workbook workbook, int sheetIndex, Engine.Operations.ISheetFilter filters, Engine.Operations.ISheetRangeReferences references,
    Engine.Operations.IShiftedFormulaEvaluator? formulas = null) : IAdvancedFilterProbe
{
    public AdvancedFilterProbe Probe(AdvancedFilterChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        var data = references.Parse(workbook, choice.Data, sheetIndex);
        var criteria = references.Parse(workbook, choice.Criteria, sheetIndex);
        IReadOnlyList<IReadOnlyList<string>> rows = criteria is null ? [] : Rows(workbook.Sheets[criteria.SheetIndex], criteria.Range);
        if (data is null || criteria is null || data.SheetIndex != sheetIndex || data.Range.RowCount < 2) return new AdvancedFilterProbe(rows, null, 0);

        var sheet = workbook.Sheets[sheetIndex];
        var hidden = filters.RowsToHide(sheet, new Engine.Operations.AdvancedFilterRequest(data.Range, criteria.Range)
        {
            UniqueOnly = choice.Unique,
            CriteriaSheet = workbook.Sheets[criteria.SheetIndex],
            Formulas = formulas,
        });
        var total = data.Range.RowCount - 1;
        return new AdvancedFilterProbe(rows, total - hidden.Count, total);
    }

    private IReadOnlyList<IReadOnlyList<string>> Rows(Worksheet sheet, Addressing.CellRange range) =>
        [.. Enumerable.Range(range.Start.Row, Math.Min(range.RowCount, 6))
            .Select(row => (IReadOnlyList<string>)[.. Enumerable.Range(range.Start.Column, Math.Min(range.ColumnCount, 4))
                .Select(column => sheet[new Addressing.CellAddress(row, column)].Value.ToString())])];
}

public interface IAdvancedFilterProbes
{
    IAdvancedFilterProbe For(Workbook workbook, int sheetIndex, Engine.Operations.IShiftedFormulaEvaluator? formulas = null);
}

public sealed class AdvancedFilterProbes(Engine.Operations.ISheetFilter filters, Engine.Operations.ISheetRangeReferences references) : IAdvancedFilterProbes
{
    public IAdvancedFilterProbe For(Workbook workbook, int sheetIndex, Engine.Operations.IShiftedFormulaEvaluator? formulas = null) =>
        new SheetAdvancedFilterProbe(workbook, sheetIndex, filters, references, formulas);
}
