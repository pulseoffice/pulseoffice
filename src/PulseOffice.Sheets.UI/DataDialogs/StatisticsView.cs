using Avalonia.Controls;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;

namespace PulseOffice.Sheets.UI.DataDialogs;

public enum StatisticsProcedure { Descriptive, Correlation, Covariance, Anova, Regression }

public sealed record StatisticsChoice(StatisticsProcedure Procedure, string Input, string Destination);

public interface IStatisticsProbe
{
    StatisticsTable Table(StatisticsChoice choice);

    bool DestinationValid(string destination);
}

public sealed class StatisticsView : LedgerDialogView
{
    private readonly IStatisticsProbe _probe;
    private readonly TextBlock _summary;

    public StatisticsView(LedgerDialogKit kit, IStatisticsProbe probe, string input, string destination, IReferenceSource ranges, IReferenceSource cells)
        : base(kit, "analysis.statistics.explanation")
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        Procedure = kit.Choices("tools.procedure",
        [
            ("descriptive", "statistics.descriptive", "tools.procedure.descriptive"),
            ("correlation", "statistics.correlation", "tools.procedure.correlation"),
            ("covariance", "statistics.covariance", "tools.procedure.covariance"),
            ("anova", "statistics.anova", "tools.procedure.anova"),
            ("regression", "statistics.regression", "tools.procedure.regression"),
        ], 0, showLabels: true, columns: 3);
        Input = new RangeFieldControl(new RangeField("input", kit["tools.input"], input, ranges), kit.Texts, kit.Metrics);
        Destination = new RangeFieldControl(new RangeField("destination", kit["tools.destination"], destination, cells), kit.Texts, kit.Metrics);
        Sketch = new CellGridSketch(kit, 64);
        _summary = kit.Note();
        Layout.Preview = PreviewPane("statistics.preview", 280, Sketch, _summary);
        var what = Layout.AddSection(kit["statistics.section.procedure"]);
        what.Add(null, Procedure, DialogRowKind.Wide);
        var where = Layout.AddSection(kit["statistics.section.data"]);
        where.Add(kit["tools.input"], Input);
        where.Add(kit["tools.destination"], Destination);
        Finish("statistics.confirm", where);
        Procedure.SelectionChanged += (_, _) => Refresh();
        Input.TextChanged += (_, _) => Refresh();
        Destination.TextChanged += (_, _) => Refresh();
        Refresh();
    }

    public IconChoiceGroup Procedure { get; }

    public RangeFieldControl Input { get; }

    public RangeFieldControl Destination { get; }

    public CellGridSketch Sketch { get; }

    public StatisticsChoice Choice => new((StatisticsProcedure)Math.Max(Procedure.SelectedIndex, 0), Input.Text, Destination.Text);

    protected override object? Result() => Choice;

    private void Refresh()
    {
        if (!Input.IsValid || !_probe.DestinationValid(Destination.Text))
        {
            Sketch.Show([]);
            _summary.Text = string.Empty;
            Report(Kit["status.analysis.badReference"]);
            return;
        }

        var table = _probe.Table(Choice);
        var rows = table.Rows.Take(SketchCells.Rows + 2)
            .Select(row => (IReadOnlyList<SketchCell>)[new SketchCell(Kit[row.LabelKey], Header: true),
                .. row.Values.Take(SketchCells.Columns - 1).Select(value => new SketchCell(
                    value.TryCoerceToNumber(out var number) ? number.ToString("G5", System.Globalization.CultureInfo.CurrentCulture) : value.ToString(),
                    Number: true))])
            .ToList();
        Sketch.Show(rows);
        _summary.Text = table.Rows.Count == 0 ? string.Empty : Kit.Texts.Format("statistics.summary", table.Rows.Count, table.Rows.Max(row => row.Values.Count) + 1);
        Report(table.Rows.Count == 0 ? Kit["status.tools.nothing"] : null);
    }
}

public sealed class SheetStatisticsProbe(
    IStatisticsProcedures statistics, Dialogs.ISheetReferenceReader reader, Workbook workbook, int sheetIndex) : IStatisticsProbe
{
    public bool DestinationValid(string destination) =>
        reader.ReadLocal(workbook, destination ?? string.Empty, sheetIndex, Dialogs.ReferenceShape.Cell) is not null;

    public StatisticsTable Table(StatisticsChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        if (reader.ReadLocal(workbook, choice.Input, sheetIndex, Dialogs.ReferenceShape.Range) is not { } input) return StatisticsTable.Empty;
        var columns = Columns(workbook.Sheets[sheetIndex], input);
        if (columns.Count == 0) return StatisticsTable.Empty;
        return choice.Procedure switch
        {
            StatisticsProcedure.Correlation => statistics.Correlation(columns),
            StatisticsProcedure.Covariance => statistics.Covariance(columns),
            StatisticsProcedure.Anova => statistics.AnovaSingleFactor(columns),
            StatisticsProcedure.Regression => statistics.LinearRegression(columns[0], columns.Count > 1 ? columns[1] : columns[0]),
            _ => statistics.Descriptive(columns),
        };
    }

    private IReadOnlyList<IReadOnlyList<double>> Columns(Worksheet sheet, CellRange range)
    {
        var used = sheet.UsedRange();
        if (used is not { } bounds) return [];
        var bottom = Math.Min(range.End.Row, bounds.End.Row);
        var right = Math.Min(range.End.Column, bounds.End.Column);
        var columns = new List<IReadOnlyList<double>>();
        for (var column = range.Start.Column; column <= right; column++)
        {
            var values = new List<double>();
            for (var row = range.Start.Row; row <= bottom; row++)
                if (sheet.GetValue(new CellAddress(row, column)).TryCoerceToNumber(out var number)) values.Add(number);
            if (values.Count > 0) columns.Add(values);
        }

        return columns;
    }
}
