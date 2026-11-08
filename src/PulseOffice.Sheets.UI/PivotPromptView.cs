using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.UI.DataDialogs;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Panels;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI;

public sealed record PivotChoice(IReadOnlyList<PivotField> Fields, string DestinationSheet);

public sealed class PivotLayoutModel
{
    private readonly IReadOnlyList<string> _columns;
    private readonly Dictionary<PivotArea, List<int>> _zones = new()
    {
        [PivotArea.Filter] = [],
        [PivotArea.Column] = [],
        [PivotArea.Row] = [],
        [PivotArea.Value] = [],
    };
    private readonly Dictionary<int, PivotAggregation> _aggregations = [];

    public PivotLayoutModel(IReadOnlyList<string> columns) => _columns = columns ?? throw new ArgumentNullException(nameof(columns));

    public event EventHandler? Changed;

    public IReadOnlyList<string> Columns => _columns;

    public IReadOnlyList<PivotArea> Areas { get; } = [PivotArea.Filter, PivotArea.Column, PivotArea.Row, PivotArea.Value];

    public IReadOnlyList<int> In(PivotArea area) => _zones.TryGetValue(area, out var zone) ? zone : [];

    public PivotArea AreaOf(int column)
    {
        foreach (var (area, zone) in _zones)
            if (zone.Contains(column)) return area;
        return PivotArea.Unused;
    }

    public PivotAggregation AggregationOf(int column) => _aggregations.GetValueOrDefault(column, PivotAggregation.Sum);

    public void Place(int column, PivotArea area, int index = int.MaxValue)
    {
        if (column < 0 || column >= _columns.Count) return;
        foreach (var zone in _zones.Values) zone.Remove(column);
        if (area != PivotArea.Unused)
        {
            var target = _zones[area];
            target.Insert(Math.Clamp(index, 0, target.Count), column);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle(int column, bool used, Func<int, bool> numeric)
    {
        if (!used) Place(column, PivotArea.Unused);
        else if (AreaOf(column) == PivotArea.Unused) Place(column, numeric(column) ? PivotArea.Value : PivotArea.Row);
    }

    public void Aggregate(int column, PivotAggregation aggregation)
    {
        _aggregations[column] = aggregation;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<PivotField> Fields() =>
        [.. Areas.SelectMany(area => In(area).Select((column, order) => new PivotField
        {
            SourceName = _columns[column],
            SourceColumnIndex = column,
            Area = area,
            AreaOrder = order,
            Aggregation = AggregationOf(column),
        }))];
}

public interface IPivotPreviewer
{
    IReadOnlyList<IReadOnlyList<string>> Preview(IReadOnlyList<PivotField> fields, int rows, int columns);

    bool IsNumeric(int column);
}

public sealed class PivotPromptView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private readonly PivotAggregation[] _aggregations =
    [
        PivotAggregation.Sum, PivotAggregation.Count, PivotAggregation.CountNumbers,
        PivotAggregation.Average, PivotAggregation.Max, PivotAggregation.Min,
        PivotAggregation.Median, PivotAggregation.DistinctCount,
    ];

    private readonly LedgerDialogKit _kit;
    private readonly IPivotPreviewer _previewer;
    private readonly StackPanel _fieldList = new() { Spacing = 0 };
    private readonly Dictionary<PivotArea, (Border Frame, StackPanel Items)> _zones = [];
    private readonly Grid _preview = new();
    private readonly Border _previewFrame;
    private readonly TextBlock _previewNote;
    private readonly TextBox _destination;
    private (int Column, Point Start)? _press;

    public PivotPromptView(IReadOnlyList<string> columns, string suggestedSheet, LedgerDialogKit kit, IPivotPreviewer previewer)
    {
        ArgumentNullException.ThrowIfNull(columns);
        _kit = kit ?? throw new ArgumentNullException(nameof(kit));
        _previewer = previewer ?? throw new ArgumentNullException(nameof(previewer));
        Model = new PivotLayoutModel(columns);
        var metrics = kit.Metrics;

        _destination = new TextBox { Text = suggestedSheet, MinWidth = 160 };
        AutomationProperties.SetName(_destination, kit["pivot.destination"]);
        _previewNote = kit.Note();
        _previewFrame = new Border { Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Child = _preview, BorderBrush = new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB)), BorderThickness = new Thickness(1), IsVisible = false };

        var zoneGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = metrics.SpacingSmall, RowSpacing = metrics.SpacingSmall };
        foreach (var (area, index) in Model.Areas.Select((area, index) => (area, index)))
        {
            var zone = Zone(area);
            Grid.SetColumn(zone, index % 2);
            Grid.SetRow(zone, index / 2);
            zoneGrid.Children.Add(zone);
        }

        var fields = new StackPanel
        {
            Spacing = metrics.SpacingSmall,
            Width = 190,
            Children =
            {
                kit.Caption("pivot.fields"),
                new PanelListFrame(metrics, new ScrollViewer { MaxHeight = 260, Content = _fieldList, Padding = new Thickness(6, 4) }),
                kit.Note(kit["pivot.dragHint"]),
            },
        };

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = metrics.SpacingLarge };
        body.Children.Add(fields);
        var right = new StackPanel
        {
            Spacing = metrics.SpacingMedium,
            Children =
            {
                zoneGrid,
                kit.Caption("pivot.preview"),
                new Panel { Height = PreviewHeight, ClipToBounds = true, Children = { _previewFrame, _previewNote } },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingSmall, Children = { new TextBlock { Text = kit["pivot.destination"], VerticalAlignment = VerticalAlignment.Center }, _destination } },
            },
        };
        Grid.SetColumn(right, 1);
        body.Children.Add(right);

        Content = new StackPanel { Spacing = metrics.SpacingMedium, Children = { kit.Help("pivot.explanation"), body } };

        AddHandler(PointerMovedEvent, OnPointerMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Model.Changed += (_, _) => Rebuild();

        Footer = new DialogFooter()
            .WithCancel(kit["pivot.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(kit["pivot.confirm"], () => Completed?.Invoke(this, new PivotChoice(
                Model.Fields(),
                string.IsNullOrWhiteSpace(_destination.Text) ? suggestedSheet : _destination.Text!.Trim())), out var confirm,
                primary: true);
        Confirm = confirm;
        Rebuild();
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public double? PreferredWidth => 760;

    private const double PreviewHeight = 170;

    public PivotLayoutModel Model { get; }

    public Button Confirm { get; }

    public IReadOnlyList<CheckBox> FieldBoxes => [.. _fieldList.Children.OfType<CheckBox>()];

    public Border ZoneFrame(PivotArea area) => _zones[area].Frame;

    public int PreviewCells => _preview.Children.Count;

    public string PreviewNote => _previewNote.Text ?? string.Empty;

    public void Drop(int column, Point point)
    {
        foreach (var (area, zone) in _zones)
        {
            if (zone.Frame.TranslatePoint(new Point(0, 0), this) is not { } origin) continue;
            var bounds = new Rect(origin, zone.Frame.Bounds.Size);
            if (!bounds.Contains(point)) continue;
            var index = zone.Items.Children.Count(item => item.TranslatePoint(new Point(0, item.Bounds.Height / 2), this) is { } centre && centre.Y < point.Y);
            Model.Place(column, area, index);
            return;
        }
    }

    private Control Zone(PivotArea area)
    {
        var items = new StackPanel { Spacing = 2, MinHeight = 52 };
        var frame = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_kit.Metrics.RadiusTight),
            Padding = new Thickness(6),
            MinHeight = 84,
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 4,
                        Children =
                        {
                            _kit.Icons.Find("pivot.area." + AreaKey(area)) is { } icon
                                ? new IconView(icon, _kit.Metrics.IconSize - 2).Themed(IconView.ForegroundProperty, Palette.Keys.TextSecondary)
                                : new TextBlock(),
                            _kit.Caption("pivot.zone." + AreaKey(area)),
                        },
                    },
                    items,
                },
            },
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border).Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);
        AutomationProperties.SetName(frame, _kit["pivot.zone." + AreaKey(area)]);
        _zones[area] = (frame, items);
        return frame;
    }

    private void Rebuild()
    {
        _fieldList.Children.Clear();
        for (var column = 0; column < Model.Columns.Count; column++)
        {
            var index = column;
            var box = new CheckBox { Content = Model.Columns[column], IsChecked = Model.AreaOf(column) != PivotArea.Unused };
            box.IsCheckedChanged += (_, _) => Model.Toggle(index, box.IsChecked == true, _previewer.IsNumeric);
            Draggable(box, index);
            _fieldList.Children.Add(box);
        }

        foreach (var (area, zone) in _zones)
        {
            zone.Items.Children.Clear();
            foreach (var column in Model.In(area)) zone.Items.Children.Add(Chip(column, area));
        }

        var fields = Model.Fields();
        _preview.Children.Clear();
        _preview.RowDefinitions.Clear();
        _preview.ColumnDefinitions.Clear();
        var hasValues = fields.Any(field => field.Area == PivotArea.Value);
        Confirm.IsEnabled = hasValues;
        _previewNote.Text = hasValues ? string.Empty : _kit["pivot.needValue"];
        _previewNote.IsVisible = !hasValues;
        _previewFrame.IsVisible = false;
        if (!hasValues) return;

        var grid = _previewer.Preview(fields, 7, 5);
        if (grid.Count == 0) return;
        _previewFrame.IsVisible = true;
        foreach (var _ in grid) _preview.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var c = 0; c < grid.Max(row => row.Count); c++) _preview.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (var r = 0; r < grid.Count; r++)
            for (var c = 0; c < grid[r].Count; c++)
            {
                var cell = new Border
                {
                    MinWidth = 54,
                    Padding = new Thickness(4, 1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB)),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Background = r == 0 ? new SolidColorBrush(Color.FromRgb(0xDC, 0xFC, 0xE7)) : Brushes.White,
                    Child = new TextBlock { Text = grid[r][c], FontSize = _kit.Metrics.FontSizeSmall, Foreground = Brushes.Black, FontWeight = r == 0 || c == 0 ? FontWeight.SemiBold : FontWeight.Normal, MaxWidth = 90, TextTrimming = TextTrimming.CharacterEllipsis },
                };
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                _preview.Children.Add(cell);
            }
    }

    private Control Chip(int column, PivotArea area)
    {
        var label = area == PivotArea.Value
            ? _kit.Texts.Format("pivot.valueChip", _kit["pivot.aggregation." + Camel(Model.AggregationOf(column).ToString())], Model.Columns[column])
            : Model.Columns[column];
        var menu = new ContextMenu();
        var items = new List<MenuItem>();
        foreach (var target in Model.Areas.Where(target => target != area))
        {
            var item = new MenuItem { Header = _kit.Texts.Format("pivot.moveTo", _kit["pivot.zone." + AreaKey(target)]) };
            item.Click += (_, _) => Model.Place(column, target);
            items.Add(item);
        }

        var remove = new MenuItem { Header = _kit["pivot.removeField"] };
        remove.Click += (_, _) => Model.Place(column, PivotArea.Unused);
        items.Add(remove);
        if (area == PivotArea.Value)
            foreach (var aggregation in _aggregations)
            {
                var item = new MenuItem { Header = _kit["pivot.aggregation." + Camel(aggregation.ToString())] };
                item.Click += (_, _) => Model.Aggregate(column, aggregation);
                items.Add(item);
            }

        menu.ItemsSource = items;
        var chip = new Button
        {
            Content = new TextBlock { Text = label, TextTrimming = TextTrimming.CharacterEllipsis },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            ContextMenu = menu,
            Padding = new Thickness(6, 2),
        };
        chip.Classes.Add("dialogQuiet");
        AutomationProperties.SetName(chip, label);
        AutomationProperties.SetHelpText(chip, _kit["pivot.chipHelp"]);
        chip.Click += (_, _) => menu.Open(chip);
        chip.KeyDown += (_, args) =>
        {
            if (args.KeyModifiers != KeyModifiers.Alt) return;
            var areas = Model.Areas.ToList();
            var position = Model.In(area).ToList().IndexOf(column);
            switch (args.Key)
            {
                case Avalonia.Input.Key.Up: Model.Place(column, area, position - 1); break;
                case Avalonia.Input.Key.Down: Model.Place(column, area, position + 1); break;
                case Avalonia.Input.Key.Left: Model.Place(column, areas[(areas.IndexOf(area) + areas.Count - 1) % areas.Count]); break;
                case Avalonia.Input.Key.Right: Model.Place(column, areas[(areas.IndexOf(area) + 1) % areas.Count]); break;
                default: return;
            }

            args.Handled = true;
        };
        Draggable(chip, column);
        return chip;
    }

    private void Draggable(Control control, int column) =>
        control.AddHandler(PointerPressedEvent, (_, args) =>
        {
            if (args.GetCurrentPoint(this).Properties.IsLeftButtonPressed) _press = (column, args.GetPosition(this));
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_press is not { } press) return;
        var point = e.GetPosition(this);
        foreach (var zone in _zones.Values)
            Highlight(zone.Frame, zone.Frame.TranslatePoint(new Point(0, 0), this) is { } origin && new Rect(origin, zone.Frame.Bounds.Size).Contains(point) && Distance(press.Start, point) > 6);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_press is not { } press) return;
        _press = null;
        foreach (var zone in _zones.Values) Highlight(zone.Frame, false);
        var point = e.GetPosition(this);
        if (Distance(press.Start, point) <= 6) return;
        Drop(press.Column, point);
        e.Handled = true;
    }

    private void Highlight(Border frame, bool target)
    {
        if (frame.Classes.Contains("dropTarget") == target) return;
        frame.Classes.Set("dropTarget", target);
        frame.Themed(Border.BorderBrushProperty, target ? "PulseAccent" : Palette.Keys.Border);
        frame.BorderThickness = new Thickness(target ? 2 : 1);
    }

    private double Distance(Point a, Point b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private string AreaKey(PivotArea area) => Camel(area.ToString());

    private string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}

public sealed class SourcePivotPreviewer(IPivotEngine engine, IPivotSource source, PivotLabels labels) : IPivotPreviewer
{
    public bool IsNumeric(int column)
    {
        var numbers = 0;
        var rows = Math.Min(source.RowCount, 20);
        for (var row = 0; row < rows; row++)
            if (source.ValueAt(row, column).Kind == Values.ValueKind.Number) numbers++;
        return rows > 0 && numbers * 2 > rows;
    }

    public IReadOnlyList<IReadOnlyList<string>> Preview(IReadOnlyList<PivotField> fields, int rows, int columns)
    {
        var table = engine.Build(new PivotDefinition
        {
            Id = "preview",
            Name = "preview",
            SourceSheet = string.Empty,
            SourceRange = default,
            DestinationSheet = string.Empty,
            Destination = Addressing.CellAddress.First,
            Fields = fields,
        }, source);
        if (table.IsEmpty) return [];

        var result = new List<IReadOnlyList<string>>
        {
            (List<string>)[string.Empty, .. table.Columns.Take(columns - 1).Select(column => fields.Any(field => field.Area == PivotArea.Column) ? Header(column) : column.Field.Label)],
        };
        for (var row = 0; row < Math.Min(table.Rows.Count, rows - 1); row++)
            result.Add((List<string>)[new string(' ', table.Rows[row].Depth * 2) + Label(table.Rows[row]),
                .. Enumerable.Range(0, Math.Min(table.Columns.Count, columns - 1)).Select(column => table.Value(row, column).ToString())]);
        return result;
    }

    private string Header(PivotColumn column) =>
        column.Node.Kind == PivotNodeKind.Item
            ? string.Join(" · ", column.Node.Path.Append(column.Field.Label).Where(part => part.Length > 0))
            : Label(column.Node);

    private string Label(PivotNode node) => node.Kind switch
    {
        PivotNodeKind.GrandTotal => labels.GrandTotal,
        PivotNodeKind.Subtotal => string.Format(System.Globalization.CultureInfo.CurrentCulture, labels.SubtotalFormat, node.Label),
        _ => node.Label,
    };
}
