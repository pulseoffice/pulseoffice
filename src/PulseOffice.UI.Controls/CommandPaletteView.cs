using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Commands.Shortcuts;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shortcuts;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public abstract record CommandPaletteEntry;

public sealed record CommandPaletteHeader(string Title) : CommandPaletteEntry;

public sealed record CommandPaletteItem(CommandPresentation Presentation) : CommandPaletteEntry;

public sealed class CommandPaletteView : UserControl, IDialogContent
{
    private const int PageStep = 8;
    private const double IconSize = 16;

    private readonly Func<string, IReadOnlyList<CommandPaletteSection>> _sections;
    private readonly IIconCatalog? _icons;
    private readonly IGestureNotation _notation;
    private readonly DesignTokens.Metrics _metrics;
    private readonly TextBox _query;
    private readonly ListBox _results;
    private readonly TextBlock _empty;
    private readonly List<CommandPaletteEntry> _entries = [];

    public CommandPaletteView(
        Func<string, IReadOnlyList<CommandPaletteSection>> sections,
        IIconCatalog? icons,
        IGestureNotation notation,
        string placeholder,
        string emptyText,
        InterfaceDensity density = InterfaceDensity.Compact)
    {
        _sections = sections ?? throw new ArgumentNullException(nameof(sections));
        _notation = notation ?? throw new ArgumentNullException(nameof(notation));
        _icons = icons;
        _metrics = DesignTokens.For(density);

        _query = new TextBox
        {
            PlaceholderText = placeholder,
            FontSize = _metrics.FontSize + 2,
            MinHeight = _metrics.ControlHeight + 8,
            VerticalContentAlignment = VerticalAlignment.Center,
            InnerLeftContent = SearchGlyph(),
        };
        AutomationProperties.SetName(_query, placeholder);

        _results = new ListBox
        {
            Height = 380,
            BorderThickness = default,
            Background = Brushes.Transparent,
            ItemTemplate = new FuncDataTemplate<CommandPaletteEntry>((entry, _) => entry is null ? new Border() : BuildEntry(entry)),
        };
        _results.Classes.Add("palette");
        AutomationProperties.SetName(_results, placeholder);

        _empty = new TextBlock
        {
            Text = emptyText,
            FontSize = _metrics.FontSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, _metrics.SpacingMedium * 3, 0, 0),
            IsVisible = false,
            IsHitTestVisible = false,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var list = new Panel { Height = 380 };
        list.Children.Add(_results);
        list.Children.Add(_empty);

        var layout = new StackPanel { Spacing = _metrics.SpacingMedium, MinWidth = 560 };
        layout.Children.Add(_query);
        layout.Children.Add(list);

        Content = layout;

        _query.TextChanged += (_, _) => Refresh();
        _query.KeyDown += OnKeyDown;
        _results.KeyDown += OnKeyDown;
        _results.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left) Commit();
        };
        _results.SelectionChanged += (_, _) => SkipHeaderSelection();
        _results.ContainerPrepared += (_, e) =>
        {
            var header = e.Container.DataContext is CommandPaletteHeader;
            e.Container.Classes.Set("paletteHeader", header);
            e.Container.IsHitTestVisible = !header;
            e.Container.Focusable = !header;
        };

        Refresh();
    }

    public event EventHandler<object?>? Completed;

    public bool ShowsNoMatches => _empty.IsVisible;

    public IReadOnlyList<CommandPaletteEntry> Entries => _entries;

    public CommandPresentation? Selected =>
        _results.SelectedIndex is var index and >= 0 && index < _entries.Count && _entries[index] is CommandPaletteItem item
            ? item.Presentation
            : null;

    public string Query
    {
        get => _query.Text ?? string.Empty;
        set => _query.Text = value;
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _query.Focus();
    }

    private void Refresh()
    {
        _entries.Clear();

        foreach (var section in _sections(Query))
        {
            if (section.Items.Count == 0) continue;
            if (section.Title is { Length: > 0 } title) _entries.Add(new CommandPaletteHeader(title));
            _entries.AddRange(section.Items.Select(item => new CommandPaletteItem(item)));
        }

        _results.ItemsSource = null;
        _results.ItemsSource = _entries.ToList();

        var first = _entries.FindIndex(entry => entry is CommandPaletteItem);
        _results.SelectedIndex = first;
        if (first >= 0) _results.ScrollIntoView(0);
        _empty.IsVisible = first < 0;
    }

    private Control BuildEntry(CommandPaletteEntry entry) => entry switch
    {
        CommandPaletteHeader header => BuildHeader(header),
        CommandPaletteItem item => BuildRow(item.Presentation),
        _ => new Border(),
    };

    private Control BuildHeader(CommandPaletteHeader header)
    {
        var text = new TextBlock
        {
            Text = header.Title.ToUpper(System.Globalization.CultureInfo.CurrentCulture),
            FontSize = _metrics.FontSize - 2,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 0.6,
            Margin = new Thickness(2, _metrics.SpacingMedium, 0, 2),
            IsHitTestVisible = false,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        text.Classes.Add("paletteHeader");
        return text;
    }

    private Control BuildRow(CommandPresentation command)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            MinHeight = _metrics.ControlHeight + 6,
        };

        var glyph = Glyph(command.Command);
        Grid.SetColumn(glyph, 0);
        grid.Children.Add(glyph);

        var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(_metrics.SpacingMedium, 0) };
        text.Children.Add(new TextBlock
        {
            Text = command.Title.TrimEnd('…', '.', ' '),
            FontSize = _metrics.FontSize,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });

        if (command.Path is { Length: > 0 } path)
            text.Children.Add(new TextBlock
            {
                Text = path,
                FontSize = _metrics.FontSize - 2,
                TextTrimming = TextTrimming.CharacterEllipsis,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        if (Query.Length > 0 && command.Category is { Length: > 0 } category)
        {
            var chip = new Border
            {
                Padding = new Thickness(6, 1),
                CornerRadius = new CornerRadius(8),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, _metrics.SpacingMedium, 0),
                Child = new TextBlock { Text = category, FontSize = _metrics.FontSize - 2 }
                    .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
            }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);
            Grid.SetColumn(chip, 2);
            grid.Children.Add(chip);
        }

        if (command.Gesture is { Length: > 0 } gesture)
        {
            var caps = new KeyCapsView(gesture, _notation, _metrics);
            Grid.SetColumn(caps, 3);
            grid.Children.Add(caps);
        }

        var name = command.Gesture is { Length: > 0 } shortcut
            ? $"{command.Title} ({_notation.Display(shortcut)})"
            : command.Title;
        AutomationProperties.SetName(grid, name);
        if (command.Description is { Length: > 0 } description)
        {
            AutomationProperties.SetHelpText(grid, description);
            ToolTip.SetTip(grid, description);
        }

        return grid;
    }

    private Control Glyph(CommandDefinition command)
    {
        var geometry = _icons is null
            ? null
            : (command.IconKey is { Length: > 0 } key ? _icons.Find(key) : null) ?? _icons.Find(command.Id.Value);

        var frame = new Border
        {
            Width = IconSize + 10,
            Height = IconSize + 10,
            CornerRadius = new CornerRadius(_metrics.RadiusSoft),
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);

        frame.Child = geometry is null
            ? new Ellipse { Width = 5, Height = 5, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                .Themed(Shape.FillProperty, Palette.Keys.TextDisabled)
            : new IconView(geometry, IconSize) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

        return frame;
    }

    private Control SearchGlyph() =>
        _icons?.Find("ui.search") is { } geometry
            ? new Border
            {
                Padding = new Thickness(_metrics.SpacingMedium, 0, 0, 0),
                Child = new IconView(geometry, 16) { VerticalAlignment = VerticalAlignment.Center }
                    .Themed(IconView.ForegroundProperty, Palette.Keys.TextSecondary),
            }
            : new Border();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                Move(1);
                break;

            case Key.Up:
                Move(-1);
                break;

            case Key.PageDown:
                Move(PageStep);
                break;

            case Key.PageUp:
                Move(-PageStep);
                break;

            case Key.Enter:
                Commit();
                break;

            default:
                return;
        }

        e.Handled = true;
    }

    public void Move(int delta)
    {
        var items = Enumerable.Range(0, _entries.Count).Where(index => _entries[index] is CommandPaletteItem).ToList();
        if (items.Count == 0) return;

        var position = items.IndexOf(_results.SelectedIndex);
        var next = position < 0 ? 0 : Math.Clamp(position + delta, 0, items.Count - 1);
        Select(items[next]);
    }

    private void Select(int index)
    {
        _results.SelectedIndex = index;
        var reveal = index > 0 && _entries[index - 1] is CommandPaletteHeader ? index - 1 : index;
        _results.ScrollIntoView(reveal);
        _results.ScrollIntoView(index);
    }

    private int _lastItem = -1;

    private void SkipHeaderSelection()
    {
        var index = _results.SelectedIndex;
        if (index < 0 || index >= _entries.Count) return;

        if (_entries[index] is CommandPaletteItem)
        {
            _lastItem = index;
            return;
        }

        _results.SelectedIndex = _lastItem >= 0 && _lastItem < _entries.Count ? _lastItem : _entries.FindIndex(entry => entry is CommandPaletteItem);
    }

    public void Commit()
    {
        if (Selected is { } command) Completed?.Invoke(this, command.Command);
    }
}
