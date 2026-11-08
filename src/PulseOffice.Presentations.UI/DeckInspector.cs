using PulseOffice.Document.Tables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;
using System;
using System.Linq;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

internal sealed class DeckInspector(DeckViewState state, IDeckViewHost host, DeckAnimationPlayer player, PulseOffice.Presentations.UI.Motion.IDeckElementNames names)
{
    private readonly DeckViewState _state = state;
    private readonly IDeckViewHost _host = host;
    private readonly Presentation _deck = state.Deck;
    private readonly DeckActions _actions = state.Actions;
    private readonly ITextCatalog _texts = state.Texts;

    private readonly PulseOffice.Presentations.UI.Motion.IDeckElementNames _names = names;
    private readonly DesignTokens.Metrics _metrics = state.Metrics;
    private readonly DeckAnimationPlayer _player = player;

    private readonly StackPanel _inspector = new() { Spacing = 8 };

    public void ApplyReadOnly() => _inspector.IsEnabled = !_actions.IsReadOnly;

    public void ResetMotion()
    {
        _transitionSlide = null;
        _animationSlide = null;
        BuildInspector();
    }

    public Border BuildPanel() => new Border
    {
        Width = 240,
        Child = _inspectorScroll = new ScrollViewer
        {
            Content = new Border { Child = _inspector, Padding = new Thickness(_metrics.SpacingMedium) },
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        },
        BorderThickness = new Thickness(1, 0, 0, 0),
    }
        .Themed(Border.BackgroundProperty, Palette.Keys.Surface)
        .Themed(Border.BorderBrushProperty, Palette.Keys.Border);

    private ScrollViewer? _inspectorScroll;

    public void BuildInspector()
    {
        var offset = _inspectorScroll?.Offset;
        _inspector.Children.Clear();
        if (offset is { } kept && _inspectorScroll is { } scroll)
            Avalonia.Threading.Dispatcher.UIThread.Post(() => scroll.Offset = kept, Avalonia.Threading.DispatcherPriority.Loaded);
        if (_state.Current is not { } slide) return;

        if (_host.MotionEditors is { } motion)
        {
            _inspector.Children.Add(Caption(_texts["deck.transition"]));
            _inspector.Children.Add(TransitionPanel(motion, slide));
            _inspector.Children.Add(new Border { Height = _metrics.SpacingMedium });
            _inspector.Children.Add(AnimationPanel(motion, slide));
        }

        BuildInteractions(slide);
    }

    private static readonly InteractionKind[] InteractionKinds =
    [
        InteractionKind.None,
        InteractionKind.NextSlide,
        InteractionKind.PreviousSlide,
        InteractionKind.FirstSlide,
        InteractionKind.LastSlide,
        InteractionKind.StopShow,
    ];

    private void BuildInteractions(Slide slide)
    {
        _inspector.Children.Add(new TextBlock
        {
            Text = _texts["deck.interactions"].ToUpperInvariant(),
            FontSize = Math.Max(9, _metrics.FontSizeSmall - 1),
            LetterSpacing = 0.6,
            Margin = new Thickness(0, _metrics.SpacingLarge, 0, _metrics.SpacingSmall / 2),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        var any = false;
        foreach (var element in slide.InZOrder())
        {
            if (element is not TextElement && element is not MediaElement) continue;
            any = true;
            _inspector.Children.Add(InteractionRow(slide, element));
        }

        if (!any)
            _inspector.Children.Add(new TextBlock
            {
                Text = _texts["deck.interactions.none"],
                FontSize = _metrics.FontSizeSmall,
                TextWrapping = TextWrapping.Wrap,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
    }

    private Control InteractionRow(Slide slide, SlideElement element)
    {
        var name = _names.Of(slide, element.Id);

        var label = new TextBlock
        {
            Text = name,
            FontSize = _metrics.FontSizeSmall,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, _metrics.SpacingSmall, 0),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);
        ToolTip.SetTip(label, name);

        var existing = slide.Interaction(element.Id);
        var current = existing?.Kind ?? InteractionKind.None;
        var kept = Array.IndexOf(InteractionKinds, current) < 0;
        InteractionKind[] kinds = kept ? [.. InteractionKinds, current] : InteractionKinds;
        var picker = new ComboBox
        {
            ItemsSource = kinds.Select((k, index) => _texts[$"{(kept && index == kinds.Length - 1 ? "deck.interactionDialog" : "deck.interaction")}.{char.ToLowerInvariant(k.ToString()[0])}{k.ToString()[1..]}"]).ToArray(),
            SelectedIndex = Math.Max(0, Array.IndexOf(kinds, current)),
            MinWidth = 136,
            VerticalAlignment = VerticalAlignment.Center,
        };
        picker.SelectionChanged += (_, _) =>
        {
            if (_state.Syncing || picker.SelectedIndex < 0) return;
            var kind = kinds[picker.SelectedIndex];
            if (kind == current && existing is not null) return;
            var action = kind == InteractionKind.None
                ? null
                : new InteractiveAction { TargetId = element.Id, Kind = kind, SoundPath = existing?.SoundPath };
            _actions.SetInteraction(slide, element.Id, action);
        };

        Avalonia.Automation.AutomationProperties.SetName(picker, name);
        Avalonia.Automation.AutomationProperties.SetLabeledBy(picker, label);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, _metrics.SpacingSmall / 2) };
        Grid.SetColumn(label, 0);
        Grid.SetColumn(picker, 1);
        row.Children.Add(label);
        row.Children.Add(picker);
        return row;
    }


    private PulseOffice.Presentations.UI.Motion.TransitionEditor? _transitionPanel;
    private Slide? _transitionSlide;
    private PulseOffice.Presentations.UI.Motion.AnimationPane? _animationPane;
    private Slide? _animationSlide;

    public PulseOffice.Presentations.UI.Motion.TransitionEditor? TransitionPanelEditor => _transitionPanel;

    public PulseOffice.Presentations.UI.Motion.AnimationPane? AnimationPanelPane => _animationPane;

    private Control TransitionPanel(PulseOffice.Presentations.UI.Motion.IDeckMotionEditors motion, Slide slide)
    {
        if (_transitionPanel is null || !ReferenceEquals(_transitionSlide, slide))
        {
            _transitionSlide = slide;
            _transitionPanel = motion.Transition(_deck, slide, PulseOffice.Presentations.UI.Motion.MotionEditorLayout.Panel);
            _transitionPanel.Changed += (_, transition) => _actions.SetTransition(slide, transition);
            _transitionPanel.ApplyToAllRequested += (_, transition) => _actions.SetTransitions([.. _deck.Slides], transition);
        }
        else _transitionPanel.Load(slide.Transition);
        return _transitionPanel;
    }

    private Control AnimationPanel(PulseOffice.Presentations.UI.Motion.IDeckMotionEditors motion, Slide slide)
    {
        if (_animationPane is null || !ReferenceEquals(_animationSlide, slide))
        {
            _animationSlide = slide;
            _animationPane = motion.Animations(new PulseOffice.Presentations.UI.Motion.AnimationPaneContext(
                slide,
                () => _state.Selected,
                id => _names.Of(slide, id),
                animations => _actions.ReplaceAnimations(slide, animations),
                _player.TogglePlay));
        }
        else _animationPane.Refresh();
        return _animationPane;
    }

    private Control Caption(string text) =>
        new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontSize = Math.Max(9, _metrics.FontSizeSmall - 1),
            LetterSpacing = 0.6,
            Margin = new Thickness(0, 0, 0, _metrics.SpacingSmall / 2),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
}
