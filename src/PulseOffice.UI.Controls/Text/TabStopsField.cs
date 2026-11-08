using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Text;

public sealed class TabStopList(IEnumerable<TabStop> initial)
{
    private const double Tolerance = 0.01;

    private readonly List<TabStop> _stops = [.. initial.Where(stop => double.IsFinite(stop.Position) && stop.Position >= 0).OrderBy(stop => stop.Position)];

    public event EventHandler? Changed;

    public IReadOnlyList<TabStop> Stops => _stops;

    public bool Add(double position)
    {
        if (!double.IsFinite(position) || position < 0 || _stops.Any(stop => Math.Abs(stop.Position - position) < Tolerance)) return false;
        _stops.Add(new TabStop(position));
        Sort();
        return true;
    }

    public void Replace(int index, TabStop stop)
    {
        if (index < 0 || index >= _stops.Count || !double.IsFinite(stop.Position) || stop.Position < 0) return;
        if (_stops.Where((_, other) => other != index).Any(existing => Math.Abs(existing.Position - stop.Position) < Tolerance)) return;
        _stops[index] = stop;
        Sort();
    }

    public void Remove(int index)
    {
        if (index < 0 || index >= _stops.Count) return;
        _stops.RemoveAt(index);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _stops.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Sort()
    {
        _stops.Sort((left, right) => left.Position.CompareTo(right.Position));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record TabStopsField(string Key, string Label, IReadOnlyList<TabStop> Initial) : CustomFormField(Key, Label)
{
    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var list = new TabStopList(Initial);
        var editor = new TabStopsControl(list, texts, metrics);
        return new FormFieldEditor(editor, () => editor.Stops.ToList(), Labelled: false) { Observe = changed => list.Changed += (_, _) => changed() };
    }
}

public sealed class TabStopsControl : UserControl
{
    private const double PointsPerCentimetre = 72 / 2.54;

    private readonly TabStopList _list;
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics;
    private readonly StackPanel _rows = new();
    private readonly TabAlignment[] _alignments = [TabAlignment.Left, TabAlignment.Center, TabAlignment.Right, TabAlignment.Decimal];
    private readonly TabLeader[] _leaders = [TabLeader.None, TabLeader.Dots, TabLeader.Dashes, TabLeader.Underline];

    public TabStopsControl(TabStopList list, ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        _list = list ?? throw new ArgumentNullException(nameof(list));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _metrics = metrics;

        var layout = new StackPanel { Spacing = metrics.SpacingSmall };
        layout.Children.Add(Header());
        layout.Children.Add(new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(metrics.RadiusSoft),
            ClipToBounds = true,
            Child = new ScrollViewer
            {
                MaxHeight = 220,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = _rows,
            },
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border)
         .Themed(Border.BackgroundProperty, Palette.Keys.Surface));

        var position = new NumberBox(new NumberEntry(new NumericBounds(0, 100, 0.25), false, CultureInfo.CurrentCulture), Next(), metrics, unit: "cm", boxWidth: 72);
        Avalonia.Automation.AutomationProperties.SetName(position, texts["tabs.position"]);
        var add = new Button { Content = texts["tabs.add"], VerticalAlignment = VerticalAlignment.Center };
        add.Classes.Add("dialogQuiet");
        add.Click += (_, _) =>
        {
            _list.Add(position.Read() * PointsPerCentimetre);
            position.Value = Next();
        };
        var clear = new Button { Content = texts["tabs.clear"], VerticalAlignment = VerticalAlignment.Center };
        clear.Classes.Add("dialogQuiet");
        clear.Click += (_, _) => _list.Clear();
        layout.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = metrics.SpacingSmall,
            Children = { position, add, clear },
        });

        _list.Changed += (_, _) => Rebuild();
        Rebuild();
        Content = layout;
    }

    public IReadOnlyList<TabStop> Stops => _list.Stops;

    private double Next() => _list.Stops.Count == 0 ? 2.5 : Math.Round(_list.Stops[^1].Position / PointsPerCentimetre + 2.5, 2);

    private Control Header()
    {
        var grid = Columns();
        Add(grid, 0, Caption(_texts["tabs.position"]));
        Add(grid, 1, Caption(_texts["tabs.alignment"]));
        Add(grid, 2, Caption(_texts["tabs.leader"]));
        Add(grid, 3, Caption(_texts["tabs.decimal"]));
        return grid;
    }

    private Grid Columns() => new()
    {
        ColumnDefinitions = new ColumnDefinitions("96,*,*,56,Auto"),
        ColumnSpacing = _metrics.SpacingSmall,
        Margin = new Thickness(_metrics.SpacingSmall, 2),
    };

    private TextBlock Caption(string text) => new TextBlock { Text = text, FontSize = _metrics.FontSizeSmall }
        .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private void Add(Grid grid, int column, Control control)
    {
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }

    private void Rebuild()
    {
        _rows.Children.Clear();
        if (_list.Stops.Count == 0)
        {
            _rows.Children.Add(new TextBlock
            {
                Text = _texts["tabs.none"],
                FontSize = _metrics.FontSizeSmall + 0.5,
                Margin = new Thickness(_metrics.SpacingMedium),
                TextWrapping = TextWrapping.Wrap,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
            return;
        }
        for (var index = 0; index < _list.Stops.Count; index++) _rows.Children.Add(Row(index));
    }

    private Control Row(int index)
    {
        var stop = _list.Stops[index];
        var grid = Columns();
        grid.MinHeight = 32;
        var position = new NumberBox(new NumberEntry(new NumericBounds(0, 100, 0.25), false, CultureInfo.CurrentCulture), Math.Round(stop.Position / PointsPerCentimetre, 3), _metrics, unit: "cm", boxWidth: 64);
        position.ValueCommitted += (_, value) => _list.Replace(index, stop with { Position = value * PointsPerCentimetre });
        var alignment = new ComboBox
        {
            ItemsSource = _alignments.Select(item => _texts[$"tabs.alignment.{item.ToString().ToLowerInvariant()}"]).ToList(),
            SelectedIndex = Array.IndexOf(_alignments, stop.Alignment),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var leader = new ComboBox
        {
            ItemsSource = _leaders.Select(item => _texts[$"tabs.leader.{item.ToString().ToLowerInvariant()}"]).ToList(),
            SelectedIndex = Array.IndexOf(_leaders, stop.Leader),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var character = new TextBox { Text = stop.DecimalCharacter.ToString(), MaxLength = 1, IsEnabled = stop.Alignment == TabAlignment.Decimal, VerticalAlignment = VerticalAlignment.Center };
        alignment.SelectionChanged += (_, _) => _list.Replace(index, Current() with { Alignment = _alignments[Math.Max(0, alignment.SelectedIndex)] });
        leader.SelectionChanged += (_, _) => _list.Replace(index, Current() with { Leader = _leaders[Math.Max(0, leader.SelectedIndex)] });
        character.LostFocus += (_, _) =>
        {
            if (character.Text is { Length: 1 } typed) _list.Replace(index, Current() with { DecimalCharacter = typed[0] });
        };
        var remove = new Button
        {
            Content = "✕",
            Width = 28,
            Height = 26,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
        };
        remove.Classes.Add("dialogQuiet");
        ToolTip.SetTip(remove, _texts["list.remove"]);
        Avalonia.Automation.AutomationProperties.SetName(remove, _texts["list.remove"]);
        Avalonia.Automation.AutomationProperties.SetName(position, _texts["tabs.position"]);
        Avalonia.Automation.AutomationProperties.SetName(alignment, _texts["tabs.alignment"]);
        Avalonia.Automation.AutomationProperties.SetName(leader, _texts["tabs.leader"]);
        Avalonia.Automation.AutomationProperties.SetName(character, _texts["tabs.decimal"]);
        remove.Click += (_, _) => _list.Remove(index);
        Add(grid, 0, position);
        Add(grid, 1, alignment);
        Add(grid, 2, leader);
        Add(grid, 3, character);
        Add(grid, 4, remove);
        return new Border { Child = grid, BorderThickness = new Thickness(0, 0, 0, index < _list.Stops.Count - 1 ? 1 : 0) }
            .Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        TabStop Current() => index < _list.Stops.Count ? _list.Stops[index] : stop;
    }
}
