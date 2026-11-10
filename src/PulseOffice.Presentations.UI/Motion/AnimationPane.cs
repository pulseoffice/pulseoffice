using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.Presentations.UI.Motion;

public sealed record AnimationPaneServices(
    IAnimationPresetRegistry Presets,
    IAnimationTimeline Timeline,
    IAnimationOrdering Ordering,
    IAnimationPresetPreview Preview,
    IAnimationLooks Looks,
    IMeasureUnits Units,
    IIconCatalog Icons,
    ITextCatalog Texts,
    IGalleryNavigator Navigator);

public sealed record AnimationPaneContext(
    Slide Slide,
    Func<SlideElement?> Selection,
    Func<string, string> TargetName,
    Action<IReadOnlyList<AnimationEffect>> Replace,
    Action Play);

public sealed class AnimationPane : UserControl
{
    private const double DragThreshold = 4;
    private const double BarHeight = 4;

    private readonly AnimationPaneServices _services;
    private readonly AnimationPaneContext _context;
    private readonly DesignTokens.Metrics _metrics = DesignTokens.For(InterfaceDensity.Compact);
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly ContentControl _options = new();
    private readonly Border _dropMarker = new() { Height = 2, IsHitTestVisible = false, IsVisible = false };
    private readonly List<Button> _rows = [];
    private string? _selectedId;
    private int _dragFrom = -1;
    private Point _dragOrigin;
    private bool _dragging;
    private int _dropIndex = -1;

    public AnimationPane(AnimationPaneContext context, AnimationPaneServices services)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _dropMarker.Themed(Border.BackgroundProperty, "PulseAccent");

        var add = new Button
        {
            Content = IconLabel("deck.anim.add", Texts["deck.motion.addEffect"]),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        AutomationProperties.SetName(add, Texts["deck.motion.addEffect"]);
        add.Flyout = GalleryFlyout(choice => Add(choice), null);
        AddButton = add;

        var play = new Button { Content = Icon("deck.anim.play"), Padding = new Thickness(_metrics.SpacingSmall, 2) };
        AutomationProperties.SetName(play, Texts["deck.motion.play"]);
        ToolTip.SetTip(play, Texts["deck.motion.play"]);
        play.Click += (_, _) => _context.Play();
        PlayButton = play;

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var caption = new TextBlock
        {
            Text = Texts["deck.animations"].ToUpperInvariant(),
            FontSize = Math.Max(9, _metrics.FontSizeSmall - 1),
            LetterSpacing = 0.6,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        Grid.SetColumn(play, 1);
        header.Children.Add(caption);
        header.Children.Add(play);

        var listHost = new Panel();
        listHost.Children.Add(_list);
        listHost.Children.Add(new Canvas { Children = { _dropMarker }, IsHitTestVisible = false });
        listHost.AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        listHost.AddHandler(PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        listHost.AddHandler(PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        listHost.AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        AutomationProperties.SetName(listHost, Texts["deck.animations"]);

        Content = new StackPanel
        {
            Spacing = _metrics.SpacingSmall,
            Children = { header, add, listHost, _options },
        };
        Refresh();
    }

    public Button AddButton { get; }

    public Button PlayButton { get; }

    public IReadOnlyList<Button> Rows => _rows;

    public string? SelectedId => _selectedId;

    public ContentControl Options => _options;

    private ITextCatalog Texts => _services.Texts;

    private IReadOnlyList<AnimationEffect> Effects => [.. _context.Slide.Animations];

    public void Refresh()
    {
        var view = _services.Timeline.Build(Effects);
        if (_selectedId is not null && view.Rows.All(row => row.Effect.Id != _selectedId)) _selectedId = null;
        _selectedId ??= view.Rows.FirstOrDefault()?.Effect.Id;

        _list.Children.Clear();
        _rows.Clear();
        if (view.Rows.Count == 0)
            _list.Children.Add(new TextBlock
            {
                Text = Texts["deck.animations.none"],
                FontSize = _metrics.FontSizeSmall,
                TextWrapping = TextWrapping.Wrap,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        foreach (var row in view.Rows)
        {
            var button = Row(row, view);
            _rows.Add(button);
            _list.Children.Add(button);
        }

        _options.Content = view.Rows.FirstOrDefault(row => row.Effect.Id == _selectedId)?.Effect is { } selected ? OptionsFor(selected) : null;
    }

    public void Select(string id)
    {
        _selectedId = id;
        Refresh();
        _rows.FirstOrDefault(row => row.Tag as string == id)?.Focus();
    }

    public void Add(AnimationPresetChoice choice)
    {
        var target = _context.Selection() ?? _context.Slide.InZOrder().OfType<TextElement>().FirstOrDefault() ?? _context.Slide.InZOrder().FirstOrDefault();
        if (target is null) return;
        var effect = new AnimationEffect
        {
            Id = _context.Slide.NextAnimationId(),
            TargetId = target.Id,
            Category = choice.Category,
            Preset = choice.Preset,
            Trigger = AnimationTrigger.OnClick,
        };
        _selectedId = effect.Id;
        _context.Replace([.. Effects, effect]);
    }

    public void Move(int from, int to)
    {
        var effects = Effects;
        if (from < 0 || from >= effects.Count || from == to) return;
        _selectedId = effects[from].Id;
        _context.Replace(_services.Ordering.Move(effects, from, to));
    }

    public void Edit(AnimationEffect edited)
    {
        _selectedId = edited.Id;
        _context.Replace(_services.Ordering.Replace(Effects, edited));
    }

    public void Remove(string id) => _context.Replace(_services.Ordering.Remove(Effects, id));

    private Button Row(AnimationTimelineRow row, AnimationTimelineView view)
    {
        var effect = row.Effect;
        var looks = _services.Looks;
        var number = new TextBlock
        {
            Text = row.ClickNumber?.ToString(CultureInfo.CurrentCulture) ?? string.Empty,
            FontSize = _metrics.FontSizeSmall,
            Width = 16,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        var category = Icon(looks.IconKey(effect.Category), looks.ColorKey(effect.Category));
        var trigger = Icon($"deck.motion.trigger.{char.ToLowerInvariant(effect.Trigger.ToString()[0])}{effect.Trigger.ToString()[1..]}", Palette.Keys.TextSecondary, 0.7);
        ToolTip.SetTip(trigger, looks.TriggerName(effect.Trigger));
        var name = new TextBlock
        {
            Text = $"{_context.TargetName(effect.TargetId)} · {looks.PresetName(effect.Preset)}",
            FontSize = _metrics.FontSizeSmall,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);

        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*"), ColumnSpacing = 4 };
        Grid.SetColumn(category, 1);
        Grid.SetColumn(trigger, 2);
        Grid.SetColumn(name, 3);
        top.Children.Add(number);
        top.Children.Add(category);
        top.Children.Add(trigger);
        top.Children.Add(name);

        var start = Math.Max(0.0001, view.StartShare(row));
        var length = Math.Max(0.02, view.LengthShare(row));
        var rest = Math.Max(0.0001, 1 - start - length);
        var bar = new Border { Height = BarHeight, CornerRadius = new CornerRadius(BarHeight / 2) }
            .Themed(Border.BackgroundProperty, looks.ColorKey(effect.Category));
        var track = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{Star(start)}*,{Star(length)}*,{Star(rest)}*"),
            Margin = new Thickness(44, 3, 0, 0),
        };
        Grid.SetColumn(bar, 1);
        track.Children.Add(bar);
        ToolTip.SetTip(track, Texts.Format("deck.motion.timeRange", Seconds(row.StartMs), Seconds(row.EndMs)));

        var content = new StackPanel { Children = { top, track } };
        if (effect.IsTriggered)
        {
            var triggeredBy = new TextBlock
            {
                Text = Texts.Format("deck.motion.triggeredBy", _context.TargetName(effect.TriggerShapeId!)),
                FontSize = Math.Max(9, _metrics.FontSizeSmall - 1),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(44, 2, 0, 0),
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
            content.Children.Insert(0, triggeredBy);
        }

        var selected = effect.Id == _selectedId;
        var button = new Button
        {
            Tag = effect.Id,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(2, 3, _metrics.SpacingSmall, 4),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(selected ? 1 : 0),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            Content = content,
        };
        if (selected) button.Themed(Button.BackgroundProperty, "PulseAccentSubtle").Themed(Button.BorderBrushProperty, "PulseAccent");
        AutomationProperties.SetName(button, $"{row.ClickNumber?.ToString(CultureInfo.CurrentCulture) ?? string.Empty} {looks.CategoryName(effect.Category)}, {name.Text}, {looks.TriggerName(effect.Trigger)}{(effect.IsTriggered ? ", " + Texts.Format("deck.motion.triggeredBy", _context.TargetName(effect.TriggerShapeId!)) : string.Empty)}".Trim());
        button.Click += (_, _) =>
        {
            if (_dragging) return;
            Select(effect.Id);
        };
        return button;
    }

    private string Star(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private string Seconds(int milliseconds) => (milliseconds / 1000.0).ToString("0.##", CultureInfo.CurrentCulture);

    private Control OptionsFor(AnimationEffect effect)
    {
        var section = new DialogSection(Texts["deck.motion.effectOptions"], _metrics);

        var change = new Button
        {
            Content = new TextBlock { Text = $"{_services.Looks.PresetName(effect.Preset)} ▾", TextTrimming = TextTrimming.CharacterEllipsis },
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(change, Texts["deck.animationDialog.preset"]);
        change.Flyout = GalleryFlyout(choice => Edit(_services.Ordering.Rechosen(effect, choice.Category, choice.Preset)), effect);
        section.Add(Texts["deck.animationDialog.preset"], change);

        var triggers = Enum.GetValues<AnimationTrigger>();
        var start = new IconChoiceGroup(
            [.. triggers.Select(trigger => new IconChoice(trigger.ToString(),
                $"deck.motion.trigger.{char.ToLowerInvariant(trigger.ToString()[0])}{trigger.ToString()[1..]}", _services.Looks.TriggerName(trigger)))],
            _services.Icons, _metrics, Texts["deck.animationDialog.trigger"], Array.IndexOf(triggers, effect.Trigger));
        start.SelectionChanged += (_, index) => Edit(effect with { Trigger = triggers[index] });
        section.Add(Texts["deck.animationDialog.trigger"], start);

        section.Add(Texts["deck.motion.triggerShape"], TriggerShapes(effect));

        var rewind = new CheckBox { Content = Texts["deck.motion.rewind"], IsChecked = effect.Rewind };
        rewind.IsCheckedChanged += (_, _) =>
        {
            if ((rewind.IsChecked == true) != effect.Rewind) Edit(effect with { Rewind = rewind.IsChecked == true });
        };
        section.Add(null, rewind, DialogRowKind.Wide);

        var duration = Measure("s", effect.DurationMs / 1000.0, 0.05, 600, 0.25, Texts["deck.motion.duration"]);
        duration.ValueCommitted += (_, value) => Edit(effect with { DurationMs = (int)Math.Round(value * 1000) });
        Scrubbed(section.Add(Texts["deck.motion.duration"], duration), duration);

        var delay = Measure("s", effect.DelayMs / 1000.0, 0, 600, 0.25, Texts["deck.motion.delay"]);
        delay.ValueCommitted += (_, value) => Edit(effect with { DelayMs = (int)Math.Round(value * 1000) });
        Scrubbed(section.Add(Texts["deck.motion.delay"], delay), delay);

        var repeat = Measure("×", effect.Repeat, 1, 100, 1, Texts["deck.animationDialog.repeat"], integer: true);
        repeat.ValueCommitted += (_, value) => Edit(effect with { Repeat = Math.Max(1, (int)Math.Round(value)) });
        Scrubbed(section.Add(Texts["deck.animationDialog.repeat"], repeat), repeat);

        var index = Effects.ToList().FindIndex(candidate => candidate.Id == effect.Id);
        var earlier = Action("deck.motion.moveEarlier", Texts["deck.animationDialog.earlier"], () => Move(index, index - 1));
        earlier.IsEnabled = index > 0;
        var later = Action("deck.motion.moveLater", Texts["deck.animationDialog.later"], () => Move(index, index + 1));
        later.IsEnabled = index >= 0 && index < Effects.Count - 1;
        var remove = Action("deck.motion.remove", Texts["deck.animationDialog.remove"], () => Remove(effect.Id));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { earlier, later, remove } };
        section.Add(null, actions, DialogRowKind.Wide);
        return section;
    }

    private ComboBox TriggerShapes(AnimationEffect effect)
    {
        var shapes = _context.Slide.InZOrder().Select(element => element.Id).ToList();
        if (effect.TriggerShapeId is { } current && !shapes.Contains(current, StringComparer.Ordinal)) shapes.Add(current);
        var items = new List<string?> { null };
        items.AddRange(shapes);
        var box = new ComboBox
        {
            ItemsSource = items.Select(id => id is null ? Texts["deck.motion.triggerSequence"] : Texts.Format("deck.motion.triggeredBy", _context.TargetName(id))).ToList(),
            SelectedIndex = Math.Max(0, items.IndexOf(effect.TriggerShapeId)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 140,
        };
        AutomationProperties.SetName(box, Texts["deck.motion.triggerShape"]);
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedIndex < 0 || box.SelectedIndex >= items.Count) return;
            var chosen = items[box.SelectedIndex];
            if (!string.Equals(chosen, effect.TriggerShapeId, StringComparison.Ordinal)) Edit(effect with { TriggerShapeId = chosen });
        };
        return box;
    }

    private void Scrubbed(DialogRow row, MeasureField field)
    {
        if (row.Label is { } label) field.AttachScrubber(label);
    }

    private MeasureField Measure(string unit, double value, double minimum, double maximum, double step, string name, bool integer = false) =>
        new(new MeasureEntry(new MeasureOptions(unit, minimum, maximum, step) { Integer = integer, LargeStep = integer ? 5 : 1 },
            _services.Units, CultureInfo.CurrentCulture), value, Texts, _metrics, _services.Icons, name, boxWidth: 56)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
        };

    private Button Action(string icon, string label, Action run)
    {
        var button = new Button { Content = Icon(icon), Padding = new Thickness(_metrics.SpacingSmall, 2) };
        AutomationProperties.SetName(button, label);
        ToolTip.SetTip(button, label);
        button.Click += (_, _) => run();
        return button;
    }

    private Flyout GalleryFlyout(Action<AnimationPresetChoice> chosen, AnimationEffect? current)
    {
        var flyout = new Flyout { Placement = PlacementMode.LeftEdgeAlignedTop };
        flyout.Opening += (_, _) =>
        {
            var gallery = EffectGallery(current);
            void Choose()
            {
                if (gallery.SelectedItem is not { } choice) return;
                flyout.Hide();
                chosen(choice);
            }
            gallery.Invoked += (_, _) => Choose();
            foreach (var tile in gallery.Tiles) tile.Click += (_, _) => Choose();
            flyout.Content = new Border { Padding = new Thickness(_metrics.SpacingSmall), Child = gallery };
        };
        return flyout;
    }

    public ThumbnailGallery<AnimationPresetChoice> EffectGallery(AnimationEffect? current)
    {
        var categories = new[] { AnimationCategory.Entrance, AnimationCategory.Emphasis, AnimationCategory.Exit, AnimationCategory.MotionPath };
        var items = categories.SelectMany(category => _services.Presets.Presets(category)
            .Select(preset => new ThumbnailGalleryItem<AnimationPresetChoice>(
                new AnimationPresetChoice(category, preset), _services.Looks.PresetName(preset), _services.Looks.CategoryName(category), preset))).ToList();
        var gallery = new ThumbnailGallery<AnimationPresetChoice>(
            items,
            new AnimationPresetPainter(_services.Preview, _services.Looks, _services.Icons),
            _services.Navigator,
            new ThumbnailGalleryOptions
            {
                TileWidth = 76,
                ThumbnailWidth = 68,
                ThumbnailHeight = 44,
                FrameRadius = _metrics.RadiusTight,
                Columns = 6,
                Scrolls = true,
                MaxViewportHeight = 420,
                Filterable = true,
                FilterPlaceholder = Texts["deck.motion.searchEffects"],
                EmptyText = Texts["deck.motion.noEffects"],
                AccessibleName = Texts["deck.motion.addEffect"],
            },
            _metrics);
        if (current is not null) gallery.Select(new AnimationPresetChoice(current.Category, current.Preset));
        return gallery;
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        var effects = Effects;
        var index = effects.ToList().FindIndex(effect => effect.Id == _selectedId);
        if (index < 0) return;
        var alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        switch (e.Key)
        {
            case Key.Up when alt: Move(index, index - 1); e.Handled = true; break;
            case Key.Down when alt: Move(index, index + 1); e.Handled = true; break;
            case Key.Up when index > 0: Select(effects[index - 1].Id); e.Handled = true; break;
            case Key.Down when index < effects.Count - 1: Select(effects[index + 1].Id); e.Handled = true; break;
            case Key.Delete or Key.Back: Remove(effects[index].Id); e.Handled = true; break;
        }
    }

    private int RowIndexAt(Point point)
    {
        for (var index = 0; index < _rows.Count; index++)
        {
            var bounds = _rows[index].Bounds;
            if (point.Y < bounds.Y + bounds.Height / 2) return index;
        }
        return _rows.Count;
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Visual host || !e.GetCurrentPoint(host).Properties.IsLeftButtonPressed) return;
        var point = e.GetPosition(_list);
        _dragFrom = _rows.FindIndex(row => row.Bounds.Contains(point));
        _dragOrigin = point;
        _dragging = false;
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_dragFrom < 0) return;
        var point = e.GetPosition(_list);
        if (!_dragging && Math.Abs(point.Y - _dragOrigin.Y) < DragThreshold) return;
        _dragging = true;
        _dropIndex = RowIndexAt(point);
        var y = _dropIndex < _rows.Count ? _rows[_dropIndex].Bounds.Y - 1 : (_rows.Count > 0 ? _rows[^1].Bounds.Bottom : 0);
        _dropMarker.Width = _list.Bounds.Width;
        Canvas.SetTop(_dropMarker, y);
        _dropMarker.IsVisible = true;
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        var from = _dragFrom;
        var dragged = _dragging;
        _dragFrom = -1;
        _dropMarker.IsVisible = false;
        if (!dragged || from < 0) return;
        var to = _dropIndex > from ? _dropIndex - 1 : _dropIndex;
        Later(() => _dragging = false);
        Move(from, to);
        e.Handled = true;
    }

    private void Later(Action run) => Avalonia.Threading.Dispatcher.UIThread.Post(run);

    private Control Icon(string key, string? colorKey = null, double scale = 0.8)
    {
        if (_services.Icons.Find(key) is not { } geometry) return new Border { Width = _metrics.IconSize * scale };
        return new IconView(geometry, _metrics.IconSize * scale) { VerticalAlignment = VerticalAlignment.Center }
            .Themed(IconView.ForegroundProperty, colorKey ?? Palette.Keys.TextPrimary);
    }

    private Control IconLabel(string icon, string label)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = _metrics.SpacingSmall };
        row.Children.Add(Icon(icon, "PulseAccent"));
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }
}
