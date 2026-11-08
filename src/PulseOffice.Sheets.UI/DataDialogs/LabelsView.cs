using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Sheets.Addressing;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed record LabelsChoice(IReadOnlyList<LabelRange> Ranges);

public interface ILabelsProbe
{
    CellRange? Read(string text);

    IReadOnlyList<IReadOnlyList<SketchCell>> Sketch(LabelRange range);
}

public sealed class LabelsView : LedgerDialogView
{
    private readonly ILabelsProbe _probe;
    private readonly List<LabelRange> _ranges;
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly TextBlock _none;

    public LabelsView(LedgerDialogKit kit, ILabelsProbe probe, IReadOnlyList<LabelRange> existing, string selection, IReferenceSource ranges)
        : base(kit, "labels.explanation")
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _ranges = [.. existing ?? []];
        LabelArea = new RangeFieldControl(new RangeField("labels", kit["labels.range"], selection, ranges), kit.Texts, kit.Metrics);
        DataArea = new RangeFieldControl(new RangeField("data", kit["labels.data"], string.Empty, ranges) { Optional = true }, kit.Texts, kit.Metrics);
        Orientation = kit.Choices("labels.orientation",
        [
            ("rows", "series.direction.down", "labels.rows"),
            ("columns", "series.direction.right", "labels.columns"),
        ], 0, showLabels: true);
        AddButton = new Button { Content = kit["labels.add"], HorizontalAlignment = HorizontalAlignment.Left };
        AddButton.Click += (_, _) => Add();
        Sketch = new CellGridSketch(kit);
        _none = kit.Note(kit["labels.none"]);

        Layout.Preview = PreviewPane("labels.preview", 250, Sketch);
        var defined = Layout.AddSection(kit["labels.section.defined"]);
        defined.Add(null, new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(kit.Metrics.RadiusSoft),
            Padding = new Thickness(kit.Metrics.SpacingSmall),
            MinHeight = 60,
            Child = new StackPanel { Children = { _none, new ScrollViewer { MaxHeight = 140, Content = _list } } },
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border).Themed(Border.BackgroundProperty, Palette.Keys.Surface), DialogRowKind.Wide);
        var add = Layout.AddSection(kit["labels.section.new"]);
        add.Add(kit["labels.range"], LabelArea);
        add.Add(kit["labels.data"], DataArea);
        add.Add(kit["labels.orientation"], Orientation);
        add.Add(null, AddButton, DialogRowKind.Indented);
        Finish("labels.confirm", add);

        LabelArea.TextChanged += (_, _) => Refresh();
        DataArea.TextChanged += (_, _) => Refresh();
        Orientation.SelectionChanged += (_, _) => Refresh();
        Rebuild();
        Refresh();
    }

    public RangeFieldControl LabelArea { get; }

    public RangeFieldControl DataArea { get; }

    public IconChoiceGroup Orientation { get; }

    public Button AddButton { get; }

    public CellGridSketch Sketch { get; }

    public IReadOnlyList<LabelRange> Ranges => _ranges;

    public void Remove(int index)
    {
        if (index < 0 || index >= _ranges.Count) return;
        _ranges.RemoveAt(index);
        Rebuild();
        Refresh();
    }

    protected override object? Result()
    {
        if (Draft() is { } pending && !_ranges.Contains(pending)) Add();
        return new LabelsChoice([.. _ranges]);
    }

    private LabelRange? Draft()
    {
        if (_probe.Read(LabelArea.Text) is not { } labels) return null;
        var data = DataArea.Text.Trim().Length == 0 ? labels : _probe.Read(DataArea.Text);
        return data is { } area
            ? new LabelRange(labels, area, Orientation.SelectedKey == "columns" ? LabelOrientation.Columns : LabelOrientation.Rows)
            : null;
    }

    private void Add()
    {
        if (Draft() is not { } draft) return;
        _ranges.RemoveAll(range => range.Labels == draft.Labels);
        _ranges.Add(draft);
        LabelArea.Text = string.Empty;
        DataArea.Text = string.Empty;
        Rebuild();
        Refresh();
    }

    private void Rebuild()
    {
        _list.Children.Clear();
        _none.IsVisible = _ranges.Count == 0;
        for (var index = 0; index < _ranges.Count; index++)
        {
            var position = index;
            var range = _ranges[index];
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = Kit.Metrics.SpacingSmall };
            row.Children.Add(Kit.Icon(range.Orientation == LabelOrientation.Columns ? "series.direction.right" : "series.direction.down", 0.8));
            var text = new TextBlock
            {
                Text = Kit.Texts.Format("labels.entry", range.Labels.ToString(), range.Data.ToString()),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            var delete = new Button { Content = Kit.Icon("sort.level.delete", 0.8), Padding = new Thickness(4) };
            delete.Classes.Add("dialogQuiet");
            ToolTip.SetTip(delete, Kit["labels.remove"]);
            Avalonia.Automation.AutomationProperties.SetName(delete, Kit["labels.remove"]);
            delete.Click += (_, _) => Remove(position);
            Grid.SetColumn(delete, 2);
            row.Children.Add(delete);
            _list.Children.Add(row);
        }
    }

    private void Refresh()
    {
        var draft = Draft();
        AddButton.IsEnabled = draft is not null;
        Sketch.Show(draft is null ? _ranges.Count > 0 ? _probe.Sketch(_ranges[^1]) : [] : _probe.Sketch(draft));
        var typed = LabelArea.Text.Trim().Length > 0 || DataArea.Text.Trim().Length > 0;
        Report(typed && draft is null ? Kit["status.analysis.badReference"] : null);
    }
}

public sealed class SheetLabelsProbe(Dialogs.ISheetReferenceReader reader, ISketchCells cells, Workbook workbook, int sheetIndex) : ILabelsProbe
{
    public CellRange? Read(string text) =>
        reader.Read(workbook, text ?? string.Empty, sheetIndex, Dialogs.ReferenceShape.Range) is [var reference] && reference.SheetIndex == sheetIndex
            ? reference.Range
            : null;

    public IReadOnlyList<IReadOnlyList<SketchCell>> Sketch(LabelRange range)
    {
        ArgumentNullException.ThrowIfNull(range);
        var box = new CellRange(
            new CellAddress(Math.Min(range.Labels.Start.Row, range.Data.Start.Row), Math.Min(range.Labels.Start.Column, range.Data.Start.Column)),
            new CellAddress(Math.Max(range.Labels.End.Row, range.Data.End.Row), Math.Max(range.Labels.End.Column, range.Data.End.Column)));
        return cells.Read(workbook.Sheets[sheetIndex], box, false, range.Labels.Contains);
    }
}
