using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using PulseOffice.Localization;
using PulseOffice.Platform;
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

public enum MotionEditorLayout
{
    Dialog,
    Panel,
}

public sealed record TransitionEditorServices(
    ITransitionCatalog Catalog,
    TransitionStage Stage,
    IMeasureUnits Units,
    IIconCatalog Icons,
    ITextCatalog Texts,
    IGalleryNavigator Navigator,
    IFileDialogService? Files = null);

public sealed record TransitionEditorContext(Presentation Deck, ITransitionSamples Samples);

public sealed class TransitionEditor : UserControl
{
    private static readonly AdvanceMode[] AdvanceModes = [AdvanceMode.OnClick, AdvanceMode.AfterTime, AdvanceMode.OnClickOrAfterTime];

    private static readonly string[] SoundExtensions =["*.wav", "*.mp3", "*.ogg", "*.m4a", "*.aac", "*.flac"];

    private readonly TransitionEditorServices _services;
    private readonly TransitionEditorContext _context;
    private readonly MotionEditorLayout _layout;
    private readonly DesignTokens.Metrics _metrics = DesignTokens.For(InterfaceDensity.Compact);
    private readonly ThumbnailGallery<TransitionKind> _gallery;
    private readonly ContentControl _directionHost = new();
    private readonly DialogRow _directionRow;
    private readonly MeasureField _duration;
    private readonly IconChoiceGroup _advance;
    private readonly MeasureField _after;
    private readonly ComboBox _sound = new() { MinWidth = 120, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly CheckBox _loop = new();
    private readonly List<string> _sounds = [];
    private IconChoiceGroup? _directions;
    private IReadOnlyList<TransitionDirection>? _directionChoices;
    private SlideTransition _transition;
    private bool _syncing;

    public TransitionEditor(SlideTransition initial, TransitionEditorContext context, TransitionEditorServices services, MotionEditorLayout layout)
    {
        _transition = initial ?? throw new ArgumentNullException(nameof(initial));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _layout = layout;

        _gallery = Gallery();
        _duration = Seconds(_transition.DurationMs / 1000.0, 0, 30, Texts["deck.motion.duration"], [0.25, 0.5, 1, 1.5, 2, 3, 5]);
        _duration.ValueCommitted += (_, value) => Update(_transition with { DurationMs = Milliseconds(value) });

        _advance = new IconChoiceGroup(
            [
                new IconChoice("onClick", "deck.motion.advance.onClick", Texts["deck.transitionDialog.onClick"]),
                new IconChoice("afterTime", "deck.motion.advance.afterTime", Texts["deck.transitionDialog.afterTime"]),
                new IconChoice("onClickOrAfterTime", "deck.motion.advance.afterTime", Texts["deck.transitionDialog.onClickOrAfterTime"]),
            ],
            services.Icons, _metrics, Texts["deck.transitionDialog.advance"], Array.IndexOf(AdvanceModes, _transition.Advance), showLabels: true);
        _advance.SelectionChanged += (_, index) =>
            Update(_transition with { Advance = AdvanceModes[Math.Clamp(index, 0, AdvanceModes.Length - 1)] });

        _after = Seconds(_transition.AdvanceAfterMs / 1000.0, 0, 3600, Texts["deck.motion.after"], [1, 2, 3, 5, 10, 15, 30, 60]);
        _after.ValueCommitted += (_, value) => Update(_transition with { AdvanceAfterMs = Milliseconds(value) });

        AutomationProperties.SetName(_sound, Texts["deck.motion.sound"]);
        _sound.SelectionChanged += (_, _) => OnSoundChosen();
        _loop.Content = Texts["deck.transitionDialog.loop"];
        _loop.IsCheckedChanged += (_, _) =>
        {
            if (!_syncing) Update(_transition with { LoopSoundUntilNext = _loop.IsChecked == true });
        };

        var effect = new DialogSection(Texts["deck.motion.effectOptions"], _metrics);
        _directionRow = effect.Add(Texts["deck.transitionDialog.direction"], _directionHost);
        var timing = new DialogSection(Texts["deck.motion.timing"], _metrics);
        var durationRow = timing.Add(Texts["deck.motion.duration"], _duration, editorWidth: 240);
        if (durationRow.Label is { } scrub) _duration.AttachScrubber(scrub);
        timing.Add(Texts["deck.transitionDialog.advance"], _advance, editorWidth: 260);
        var afterRow = timing.Add(Texts["deck.motion.after"], _after, editorWidth: 240);
        if (afterRow.Label is { } afterScrub) _after.AttachScrubber(afterScrub);
        var sound = new DialogSection(Texts["deck.motion.sound"], _metrics);
        sound.Add(Texts["deck.motion.sound"], SoundRow(), editorWidth: 300);
        sound.Add(null, _loop, DialogRowKind.Indented);

        var applyAll = new Button
        {
            Content = IconLabel("deck.motion.applyAll", Texts["deck.motion.applyAll"]),
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall / 2),
        };
        AutomationProperties.SetName(applyAll, Texts["deck.motion.applyAll"]);
        applyAll.Click += (_, _) => ApplyToAllRequested?.Invoke(this, _transition);
        ApplyAllButton = applyAll;

        var settings = new StackPanel { Spacing = _metrics.SpacingLarge };
        Grid.SetIsSharedSizeScope(settings, true);
        settings.Children.Add(effect);
        settings.Children.Add(timing);
        settings.Children.Add(sound);
        if (layout == MotionEditorLayout.Panel) settings.Children.Add(applyAll);
        Settings = settings;

        if (layout == MotionEditorLayout.Panel)
        {
            var stack = new StackPanel { Spacing = _metrics.SpacingMedium };
            stack.Children.Add(_gallery);
            stack.Children.Add(settings);
            Content = stack;
        }
        else
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*") };
            var divider = new Border { Width = 1, Margin = new Thickness(_metrics.SpacingLarge, 0) }
                .Themed(Border.BackgroundProperty, Palette.Keys.Border);
            Grid.SetColumn(divider, 1);
            Grid.SetColumn(settings, 2);
            grid.Children.Add(_gallery);
            grid.Children.Add(divider);
            grid.Children.Add(settings);
            Content = grid;
        }

        Sync();
    }

    public event EventHandler<SlideTransition>? Changed;

    public event EventHandler<SlideTransition>? ApplyToAllRequested;

    public SlideTransition Transition => _transition;

    public ThumbnailGallery<TransitionKind> TransitionGallery => _gallery;

    public IconChoiceGroup? Directions => _directions;

    public MeasureField Duration => _duration;

    public IconChoiceGroup Advance => _advance;

    public MeasureField AdvanceAfter => _after;

    public ComboBox Sound => _sound;

    public Button ApplyAllButton { get; }

    public Control Settings { get; }

    public bool DirectionVisible => _directionRow.IsVisible;

    private ITextCatalog Texts => _services.Texts;

    public void Load(SlideTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        _transition = transition;
        Sync();
    }

    public void ChooseKind(TransitionKind kind) => Update(_services.Catalog.Choose(_transition, kind));

    public void ChooseDirection(TransitionDirection direction)
    {
        if (_services.Catalog.Find(_transition.Kind).Directions.Contains(direction))
            Update(_transition with { Direction = direction });
    }

    public void ChooseSound(string? path) => Update(_transition with { SoundPath = string.IsNullOrWhiteSpace(path) ? null : path });

    private ThumbnailGallery<TransitionKind> Gallery()
    {
        var panel = _layout == MotionEditorLayout.Panel;
        var tile = panel ? 66.0 : 104.0;
        var thumbnailWidth = tile - 8;
        var gallery = new ThumbnailGallery<TransitionKind>(
            [.. _services.Catalog.Entries.Select(entry => new ThumbnailGalleryItem<TransitionKind>(
                entry.Kind, KindName(entry.Kind), Texts[$"deck.motion.group.{GroupKey(entry.Group)}"], entry.Kind.ToString()))],
            new TransitionGalleryPainter(_services.Stage, _services.Catalog, _context.Samples, () => _transition),
            _services.Navigator,
            new ThumbnailGalleryOptions
            {
                TileWidth = tile,
                ThumbnailWidth = thumbnailWidth,
                ThumbnailHeight = Math.Round(thumbnailWidth * 9 / 16),
                FrameRadius = _metrics.RadiusTight,
                Columns = panel ? 3 : 4,
                Scrolls = !panel,
                MaxViewportHeight = 380,
                AccessibleName = Texts["deck.transition"],
            },
            _metrics);
        gallery.SelectionChanged += (_, kind) =>
        {
            ChooseKind(kind);
            PlaySelected();
        };
        return gallery;
    }

    public void PlaySelected()
    {
        var index = _gallery.SelectedIndex;
        if (index >= 0 && _gallery.TileOf(index) is { } tile)
            foreach (var thumbnail in tile.GetVisualDescendants().OfType<TransitionThumbnail>())
                thumbnail.Play();
    }

    private string GroupKey(TransitionGroup group) => group switch
    {
        TransitionGroup.Exciting => "exciting",
        TransitionGroup.Dynamic => "dynamic",
        _ => "subtle",
    };

    private string KindName(TransitionKind kind) =>
        Texts[$"deck.transition.{char.ToLowerInvariant(kind.ToString()[0])}{kind.ToString()[1..]}"];

    private string DirectionName(TransitionDirection direction) =>
        Texts[$"deck.direction.{char.ToLowerInvariant(direction.ToString()[0])}{direction.ToString()[1..]}"];

    private MeasureField Seconds(double initial, double minimum, double maximum, string name, IReadOnlyList<double> presets) =>
        new(new MeasureEntry(new MeasureOptions("s", minimum, maximum, 0.25) { LargeStep = 1, Presets = presets, ShowSlider = _layout == MotionEditorLayout.Dialog },
            _services.Units, CultureInfo.CurrentCulture),
            initial, Texts, _metrics, _services.Icons, name, boxWidth: 64)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
        };

    private int Milliseconds(double seconds) => (int)Math.Round(seconds * 1000);

    private Control SoundRow()
    {
        var browse = new Button { Content = Texts["deck.motion.browse"], VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(browse, Texts["deck.motion.browseSound"]);
        browse.IsEnabled = _services.Files is not null;
        browse.Click += async (_, _) => await BrowseAsync().ConfigureAwait(true);
        if (_layout == MotionEditorLayout.Panel)
            return new StackPanel { Spacing = _metrics.SpacingSmall, Children = { _sound, browse } };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = _metrics.SpacingSmall };
        Grid.SetColumn(browse, 1);
        row.Children.Add(_sound);
        row.Children.Add(browse);
        return row;
    }

    private async Task BrowseAsync()
    {
        if (_services.Files is not { } files) return;
        await using var chosen = await files.OpenFileAsync(Texts["deck.motion.browseSound"],
            [new FileFilter(Texts["deck.motion.soundFiles"], SoundExtensions)]).ConfigureAwait(true);
        if (chosen is null) return;
        ChooseSound(chosen.Path);
    }

    private void OnSoundChosen()
    {
        if (_syncing || _sound.SelectedIndex < 0) return;
        ChooseSound(_sound.SelectedIndex == 0 ? null : _sounds[_sound.SelectedIndex - 1]);
    }

    private Control IconLabel(string icon, string label)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = _metrics.SpacingSmall };
        if (_services.Icons.Find(icon) is { } geometry)
            row.Children.Add(new IconView(geometry, _metrics.IconSize * 0.8) { VerticalAlignment = VerticalAlignment.Center }
                .Themed(IconView.ForegroundProperty, Palette.Keys.TextPrimary));
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    private void Update(SlideTransition transition)
    {
        if (_syncing || transition == _transition) return;
        _transition = transition;
        Sync();
        Changed?.Invoke(this, transition);
    }

    private void Sync()
    {
        _syncing = true;
        try
        {
            _gallery.Select(_transition.Kind);
            _gallery.RefreshThumbnails();
            SyncDirections();
            _duration.Value = _transition.DurationMs / 1000.0;
            _duration.IsEnabled = !_transition.IsNone;
            _advance.SelectedIndex = Array.IndexOf(AdvanceModes, _transition.Advance);
            _after.Value = _transition.AdvanceAfterMs / 1000.0;
            _after.IsEnabled = _transition.AdvancesAfterTime;
            SyncSounds();
            _loop.IsChecked = _transition.LoopSoundUntilNext;
            _loop.IsEnabled = _transition.SoundPath is not null;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SyncDirections()
    {
        var entry = _services.Catalog.Find(_transition.Kind);
        _directionRow.IsVisible = entry.Directions.Count > 0;
        if (entry.Directions.Count == 0)
        {
            _directions = null;
            _directionChoices = null;
            _directionHost.Content = null;
            return;
        }

        var keys = entry.Directions.Select(direction => direction.ToString()).ToList();
        if (_directions is null || !ReferenceEquals(_directionChoices, entry.Directions))
        {
            _directionChoices = entry.Directions;
            _directions = new IconChoiceGroup(
                [.. entry.Directions.Select(direction => new IconChoice(direction.ToString(),
                    $"deck.motion.direction.{direction.ToString().ToLowerInvariant()}", DirectionName(direction)))],
                _services.Icons, _metrics, Texts["deck.transitionDialog.direction"], Math.Max(0, keys.IndexOf(_transition.Direction.ToString())));
            _directions.SelectionChanged += (_, index) => ChooseDirection(entry.Directions[index]);
            _directionHost.Content = _directions;
        }
        else _directions.SelectKey(_transition.Direction.ToString());
    }

    private void SyncSounds()
    {
        _sounds.Clear();
        _sounds.AddRange(_services.Catalog.SoundsIn(_context.Deck));
        if (_transition.SoundPath is { } current && !_sounds.Contains(current, StringComparer.Ordinal)) _sounds.Add(current);
        _sound.ItemsSource = new List<string>([Texts["deck.motion.noSound"], .. _sounds.Select(path => Path.GetFileName(path) is { Length: > 0 } name ? name : path)]);
        _sound.SelectedIndex = _transition.SoundPath is { } chosen ? _sounds.IndexOf(chosen) + 1 : 0;
    }
}
