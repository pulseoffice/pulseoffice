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

internal sealed class DeckFilmstrip(DeckViewState state, IDeckViewHost host, ISlideRenderer renderer, ISlideKeyNavigation slideKeys, PulseOffice.UI.Controls.Services.IContextMenuFactory contextMenus, PulseOffice.UI.Controls.Objects.IAnimatedImageProbe stillImages, DeckSectionHeaders sectionHeaders, SlideStripLayout layout)
{
    private readonly SlideDragSession _drag = new(sectionHeaders, state.Deck, state.Actions, horizontal: false);
    private IReadOnlyList<Control> _frames = [];
    private readonly DeckViewState _state = state;
    private readonly IDeckViewHost _host = host;
    private readonly Presentation _deck = state.Deck;
    private readonly ITextCatalog _texts = state.Texts;
    private readonly DesignTokens.Metrics _metrics = state.Metrics;
    private readonly ISlideRenderer _renderer = renderer;
    private readonly ISlideKeyNavigation _slideKeys = slideKeys;
    private readonly PulseOffice.UI.Controls.Services.IContextMenuFactory _contextMenus = contextMenus;

    private readonly StackPanel _filmstrip = new() { Spacing = 8 };
    private SlideView? _currentThumbnail;
    private Control? _currentThumbnailFrame;
    private bool _filmstripHadFocus;
    private TextBlock? _slidesCaption;
    private readonly SlideFrameCache<FilmstripEntry> _thumbnails = new();
    private readonly PulseOffice.UI.Controls.Objects.IAnimatedImageProbe _stillImages = stillImages;
    private readonly DeckSectionHeaders _headers = sectionHeaders;
    private readonly SlideStripLayout _layout = layout;

    public StackPanel Panel => _filmstrip;

    public SlideView? CurrentThumbnail => _currentThumbnail;

    public Control? CurrentThumbnailFrame => _currentThumbnailFrame;

    public void RefreshCaption()
    {
        if (_slidesCaption is not null) _slidesCaption.Text = _texts["deck.slides"].ToUpperInvariant();
    }

    public Border BuildRail()
    {

        var rail = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };

        var caption = new TextBlock
        {
            Text = _texts["deck.slides"].ToUpperInvariant(),
            FontSize = Math.Max(9, _metrics.FontSizeSmall - 1),
            LetterSpacing = 0.6,
            Margin = new Thickness(_metrics.SpacingMedium, _metrics.SpacingMedium, 0, _metrics.SpacingSmall),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        _slidesCaption = caption;

        _filmstrip.AddHandler(InputElement.KeyDownEvent, OnFilmstripKey, Avalonia.Interactivity.RoutingStrategies.Bubble);
        Avalonia.Automation.AutomationProperties.SetName(_filmstrip, _texts["deck.slides"]);
        Avalonia.Automation.AutomationProperties.SetControlTypeOverride(_filmstrip, Avalonia.Automation.Peers.AutomationControlType.List);
        var scroll = new ScrollViewer
        {
            Content = new Border
            {
                Child = _filmstrip,
                Padding = new Thickness(_metrics.SpacingMedium, 0, _metrics.SpacingMedium, _metrics.SpacingMedium),
            },
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };

        Grid.SetRow(caption, 0);
        Grid.SetRow(scroll, 1);
        rail.Children.Add(caption);
        rail.Children.Add(scroll);

        var railPanel = new Border
        {
            Width = 210,
            Child = rail,
            BorderThickness = new Thickness(0, 0, 1, 0),
        }
            .Themed(Border.BackgroundProperty, Palette.Keys.Surface)
            .Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        return railPanel;
    }

    public void BuildFilmstrip()
    {
        _filmstripHadFocus |= _filmstrip.IsKeyboardFocusWithin;
        _currentThumbnail = null;
        _currentThumbnailFrame = null;

        var entries = _thumbnails.Refresh(_deck, _state.Current, Thumbnail);
        _frames = [.. entries.Select(entry => entry.Frame)];
        var children = new List<Control>(entries.Count);
        _headers.Begin();
        foreach (var item in _layout.Items(_deck))
        {
            if (item.Header is { } header)
            {
                children.Add(_headers.For(header));
                continue;
            }
            var entry = entries[item.SlideIndex];
            Refresh(entry, item.SlideIndex);
            children.Add(entry.Frame);
        }
        _headers.End();

        if (_filmstrip.Children.Count != children.Count || !_filmstrip.Children.Zip(children).All(pair => ReferenceEquals(pair.First, pair.Second)))
        {
            _filmstrip.Children.Clear();
            _filmstrip.Children.AddRange(children);
        }

        if (!_filmstripHadFocus || _currentThumbnailFrame is not { } frame) return;
        _filmstripHadFocus = false;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            frame.Focus(NavigationMethod.Directional);
            frame.BringIntoView();
        });
    }

    private void OnFilmstripKey(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _state.Current is null) return;
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            _host.FocusDocument();
            e.Handled = true;
            return;
        }
        if (_slideKeys.Target(e.Key, e.KeyModifiers, _deck.IndexOf(_state.Current), _deck.Slides.Count, listFocused: true) is not { } target) return;
        e.Handled = true;
        if (target == _deck.IndexOf(_state.Current)) return;
        _filmstripHadFocus = true;
        _host.GoToSlide(target);
    }

    private void Refresh(FilmstripEntry entry, int index)
    {
        var chrome = _state.ChromeFor(entry.Slide);
        if (entry.Thumb.Chrome != chrome) entry.Thumb.Chrome = chrome;
        Avalonia.Automation.AutomationProperties.SetName(entry.Frame, SlideAccessibleName(entry.Slide, index));
        if (!ReferenceEquals(entry.Slide, _state.Current)) return;
        _currentThumbnail = entry.Thumb;
        _currentThumbnailFrame = entry.Frame;
        entry.Thumb.LiveText = _host.LiveEditing();
    }

    private FilmstripEntry Thumbnail(Slide slide, int index)
    {
        var thumb = new SlideView(_renderer, _stillImages)
        {
            Deck = _deck,
            Size = _deck.Size,
            DeckTheme = _deck.Theme,
            Master = _deck.Master,
            Padding = 0,
            Height = 96,
        };
        thumb.Chrome = _state.ChromeFor(slide);
        thumb.Fields = new PulseOffice.Presentations.SlideFieldsFactory().For(_deck, slide);
        thumb.Show(slide);
        if (slide.Hidden) thumb.Opacity = 0.35;

        var number = new TextBlock
        {
            Text = (index + 1).ToString(System.Globalization.CultureInfo.CurrentCulture),
            FontSize = _metrics.FontSizeSmall,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, _metrics.SpacingSmall, 0),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(number, 0);
        Grid.SetColumn(thumb, 1);
        row.Children.Add(number);
        row.Children.Add(thumb);

        var active = ReferenceEquals(slide, _state.Current);

        var frame = new Border
        {
            Child = row,
            Padding = new Thickness(_metrics.SpacingSmall),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            BorderThickness = new Thickness(active ? 2 : 1),
            Cursor = new Cursor(StandardCursorType.Hand),
            Focusable = true,
            IsTabStop = active,
            FocusAdorner = new Avalonia.Controls.Templates.FuncTemplate<Control>(() => new Border
            {
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(_metrics.RadiusTight + 3),
                Margin = new Thickness(-3),
            }.Themed(Border.BorderBrushProperty, PulseOffice.UI.Theming.AccentService.AccentKey)),
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceRaised);
        Avalonia.Automation.AutomationProperties.SetControlTypeOverride(frame, Avalonia.Automation.Peers.AutomationControlType.ListItem);

        frame = frame.Themed(Border.BorderBrushProperty, active ? PulseOffice.UI.Theming.AccentService.AccentKey : Palette.Keys.Border);

        frame.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(frame).Properties.IsLeftButtonPressed)
                _drag.Begin(slide, e.GetPosition(_filmstrip), _drag.PlatformSlop(frame, e.Pointer.Type));
        };
        frame.PointerMoved += (_, e) =>
        {
            if (_drag.From >= 0) _drag.Track(_filmstrip, e.GetPosition(_filmstrip), _frames);
        };
        frame.PointerCaptureLost += (_, _) => _drag.Cancel();
        frame.PointerReleased += (_, e) =>
        {
            _filmstripHadFocus = true;
            if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Left
                && _drag.Finish(_filmstrip, e.GetPosition(_filmstrip), _frames)) return;
            _host.Select(slide);
            if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Right) _contextMenus.Open("slide", _host.View);
        };

        return new FilmstripEntry(slide, frame, thumb);
    }

    private sealed record FilmstripEntry(Slide Slide, Border Frame, SlideView Thumb);

    private string SlideAccessibleName(Slide slide, int index)
    {
        var position = _texts.Format("deck.slide.accessibleName", index + 1, _deck.Slides.Count);
        var title = slide.Elements.OfType<TextElement>()
            .FirstOrDefault(element => element.Text.Role is PlaceholderRole.Title)?.Text.PlainText;
        var named = string.IsNullOrWhiteSpace(title) ? position : $"{position}: {title.Trim()}";
        return slide.Hidden ? $"{named} ({_texts["deck.slide.accessibleHidden"]})" : named;
    }
}
