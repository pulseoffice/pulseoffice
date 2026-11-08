using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using PulseOffice.Sheets.Addressing;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Panels;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed record ScenarioChoice(IReadOnlyList<Dialogs.ScenarioRow> Rows);

public interface IScenarioProbe
{
    CellRange? Read(string text);

    IReadOnlyList<IReadOnlyList<SketchCell>> Sketch(Dialogs.ScenarioRow row);
}

public sealed class ScenarioView : LedgerDialogView
{
    private readonly IScenarioProbe _probe;
    private readonly Dialogs.IScenarioPlanner _planner;
    private readonly List<Dialogs.ScenarioRow> _rows;
    private readonly CellRange _selection;
    private readonly ListBox _list;
    private readonly DialogSection _detail;
    private bool _loading;

    public ScenarioView(LedgerDialogKit kit, IScenarioProbe probe, Dialogs.IScenarioPlanner planner, IReadOnlyList<Dialogs.ScenarioRow> rows, CellRange selection, IReferenceSource ranges)
        : base(kit, "scenarios.explanation")
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _rows = [.. rows ?? []];
        _selection = selection;
        _list = new ListBox
        {
            Height = 170,
            ItemTemplate = new FuncDataTemplate<Dialogs.ScenarioRow?>((row, _) => row is null ? new Panel() : Entry(row), supportsRecycling: false),
        };
        _list.Classes.Add("compact");
        Actions = new PanelActionBar(kit.Icons, kit.Metrics,
        [
            new PanelAction("add", "sort.level.add", kit["scenarios.add"]),
            new PanelAction("duplicate", "sort.level.copy", kit["scenarios.duplicate"]),
            new PanelAction("delete", "sort.level.delete", kit["scenarios.delete"]),
            new PanelAction("up", "sort.level.up", kit["scenarios.up"]) { StartsGroup = true },
            new PanelAction("down", "sort.level.down", kit["scenarios.down"]),
        ]);
        Actions.Invoked += (_, key) => Act(key);
        NameBox = new TextBox();
        CommentBox = new TextBox { AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Height = 56 };
        RangeBox = new RangeFieldControl(new RangeField("range", kit["scenarios.range"], selection.ToString(), ranges), kit.Texts, kit.Metrics);
        Sketch = new CellGridSketch(kit, 56);

        Layout.Preview = PreviewPane("scenarios.preview", 240, Sketch);
        var all = Layout.AddSection(kit["scenarios.list"]);
        all.Add(null, new StackPanel
        {
            Spacing = kit.Metrics.SpacingSmall,
            Children =
            {
                new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(kit.Metrics.RadiusSoft), Child = _list }
                    .Themed(Border.BorderBrushProperty, Palette.Keys.Border).Themed(Border.BackgroundProperty, Palette.Keys.Surface),
                Actions,
            },
        }, DialogRowKind.Wide);
        _detail = Layout.AddSection(kit["scenarios.section.detail"]);
        _detail.Add(kit["scenarios.name"], NameBox);
        _detail.Add(kit["scenarios.comment"], CommentBox, alignTop: true);
        RangeRow = _detail.Add(kit["scenarios.range"], RangeBox);
        Finish("scenarios.save", _detail, "scenarios.show", Show);

        _list.SelectionChanged += (_, _) => Load();
        _list.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Delete) Act("delete");
            else if (args.Key == Key.Insert) Act("add");
            else if (args.KeyModifiers.HasFlag(KeyModifiers.Alt) && args.Key == Key.Up) Act("up");
            else if (args.KeyModifiers.HasFlag(KeyModifiers.Alt) && args.Key == Key.Down) Act("down");
            else return;
            args.Handled = true;
        };
        NameBox.TextChanged += (_, _) => Edit(row => row with { Name = NameBox.Text ?? string.Empty });
        CommentBox.TextChanged += (_, _) => Edit(row => row with { Comment = CommentBox.Text ?? string.Empty });
        RangeBox.TextChanged += (_, _) => Edit(row => _probe.Read(RangeBox.Text) is { } range && row.Existing is null ? row with { Range = range } : row);
        Rebuild(_rows.Count > 0 ? 0 : -1);
    }

    public PanelActionBar Actions { get; }

    public TextBox NameBox { get; }

    public TextBox CommentBox { get; }

    public RangeFieldControl RangeBox { get; }

    public DialogRow RangeRow { get; }

    public CellGridSketch Sketch { get; }

    public IReadOnlyList<Dialogs.ScenarioRow> Rows => _rows;

    public int Selected => _list.SelectedIndex;

    public void Select(int index) => _list.SelectedIndex = index;

    public void Act(string key)
    {
        var index = _list.SelectedIndex;
        switch (key)
        {
            case "add":
                _rows.Add(new Dialogs.ScenarioRow(null, _planner.NextName(_rows.Select(row => row.Name), Kit["scenarios.stem"]), string.Empty, _selection));
                Rebuild(_rows.Count - 1);
                NameBox.Focus();
                NameBox.SelectAll();
                break;
            case "duplicate" when index >= 0:
                var source = _rows[index];
                _rows.Insert(index + 1, new Dialogs.ScenarioRow(null, _planner.NextName(_rows.Select(row => row.Name), source.Name), source.Comment, source.Range));
                Rebuild(index + 1);
                break;
            case "delete" when index >= 0:
                _rows.RemoveAt(index);
                Rebuild(Math.Min(index, _rows.Count - 1));
                break;
            case "up" when index > 0:
                (_rows[index - 1], _rows[index]) = (_rows[index], _rows[index - 1]);
                Rebuild(index - 1);
                break;
            case "down" when index >= 0 && index < _rows.Count - 1:
                (_rows[index + 1], _rows[index]) = (_rows[index], _rows[index + 1]);
                Rebuild(index + 1);
                break;
        }
    }

    protected override object? Result() => new ScenarioChoice([.. _rows.Select(row => row with { Apply = false })]);

    private void Show()
    {
        var index = _list.SelectedIndex;
        if (index < 0 || _planner.InvalidRows(_rows).Count > 0) return;
        Complete(new ScenarioChoice([.. _rows.Select((row, position) => row with { Apply = position == index })]));
    }

    private Control Entry(Dialogs.ScenarioRow row) => new StackPanel
    {
        Margin = new Thickness(Kit.Metrics.SpacingSmall, 2),
        Children =
        {
            new TextBlock { Text = row.Name, FontWeight = Avalonia.Media.FontWeight.SemiBold },
            Kit.Note(row.Comment.Length > 0 ? row.Range + " · " + row.Comment : row.Range.ToString()),
        },
    };

    private void Rebuild(int selected)
    {
        _loading = true;
        _list.ItemsSource = _rows.ToList();
        _list.SelectedIndex = selected;
        _loading = false;
        Load();
    }

    private void Load()
    {
        var index = _list.SelectedIndex;
        _loading = true;
        _detail.IsEnabled = index >= 0;
        var row = index >= 0 ? _rows[index] : null;
        NameBox.Text = row?.Name ?? string.Empty;
        CommentBox.Text = row?.Comment ?? string.Empty;
        RangeBox.Text = row?.Range.ToString() ?? string.Empty;
        RangeRow.IsEnabled = row is { Existing: null };
        _loading = false;
        Actions.SetEnabled("duplicate", index >= 0);
        Actions.SetEnabled("delete", index >= 0);
        Actions.SetEnabled("up", index > 0);
        Actions.SetEnabled("down", index >= 0 && index < _rows.Count - 1);
        Refresh();
    }

    private void Edit(Func<Dialogs.ScenarioRow, Dialogs.ScenarioRow> change)
    {
        if (_loading || _list.SelectedIndex < 0) return;
        _rows[_list.SelectedIndex] = change(_rows[_list.SelectedIndex]);
        Refresh();
    }

    private void Refresh()
    {
        var index = _list.SelectedIndex;
        Sketch.Show(index >= 0 ? _probe.Sketch(_rows[index]) : []);
        var invalid = _planner.InvalidRows(_rows);
        Report(invalid.Count > 0 ? Kit.Texts.Format("status.scenarios.badName", invalid[0] + 1) : null);
        if (SecondaryButton is not null) SecondaryButton.IsEnabled = index >= 0 && invalid.Count == 0;
    }
}

public sealed class SheetScenarioProbe(Dialogs.ISheetReferenceReader reader, ISketchCells cells, Workbook workbook, int sheetIndex) : IScenarioProbe
{
    public CellRange? Read(string text) => reader.ReadLocal(workbook, text ?? string.Empty, sheetIndex, Dialogs.ReferenceShape.Range);

    public IReadOnlyList<IReadOnlyList<SketchCell>> Sketch(Dialogs.ScenarioRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Existing is not { } scenario) return cells.Read(workbook.Sheets[sheetIndex], row.Range, false);
        var shown = new List<IReadOnlyList<SketchCell>>();
        for (var r = row.Range.Start.Row; r <= Math.Min(row.Range.End.Row, row.Range.Start.Row + SketchCells.Rows - 1); r++)
        {
            var line = new List<SketchCell>();
            for (var c = row.Range.Start.Column; c <= Math.Min(row.Range.End.Column, row.Range.Start.Column + SketchCells.Columns - 1); c++)
            {
                var address = new CellAddress(r, c);
                line.Add(scenario.Values.TryGetValue(address, out var value)
                    ? cells.Cell(value, marked: !value.Equals(workbook.Sheets[sheetIndex][address].Value))
                    : new SketchCell(string.Empty));
            }

            shown.Add(line);
        }

        return shown;
    }
}
