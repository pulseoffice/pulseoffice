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

internal sealed class DeckModeViews(DeckViewState state, IDeckViewHost host, ISlideRenderer renderer, DeckMasterViews masters, DeckTextCorrection correction, PulseOffice.UI.Controls.Objects.IAnimatedImageProbe stillImages)
{
    private readonly DeckViewState _state = state;
    private readonly IDeckViewHost _host = host;
    private readonly Presentation _deck = state.Deck;
    private readonly DeckActions _actions = state.Actions;
    private readonly ITextCatalog _texts = state.Texts;
    private readonly DesignTokens.Metrics _metrics = state.Metrics;
    private readonly ISlideRenderer _renderer = renderer;
    private readonly PulseOffice.UI.Controls.Objects.IAnimatedImageProbe _stillImages = stillImages;
    private readonly DeckMasterViews _masters = masters;
    private readonly DeckTextCorrection _correction = correction;

    private TextBox? _modeNotes;
    private TextBox? _outlineEditor;

    public Control Build(DeckViewMode mode, Control normal) => mode switch
    {
        DeckViewMode.Sorter => BuildSorter(),
        DeckViewMode.Outline => BuildOutline(),
        DeckViewMode.Notes => BuildNotes(),
        DeckViewMode.Master => _masters.BuildMaster(),
        DeckViewMode.NotesMaster => _masters.BuildPrintMaster(PrintMasterKind.Notes),
        DeckViewMode.HandoutMaster => _masters.BuildPrintMaster(PrintMasterKind.Handout),
        _ => normal,
    };

    public void CommitPendingEdits(Slide? slide)
    {
        if (slide is not null && _modeNotes is { IsVisible: true } notes && TopLevel.GetTopLevel(notes) is not null) _actions.SetNotes(slide, notes.Text ?? string.Empty);
        if (_outlineEditor is { } outline && TopLevel.GetTopLevel(outline) is not null) ApplyOutline(outline.Text ?? string.Empty);
    }

    private int _dragFrom = -1;

    private SlideSorterStrip? _sorterStrip;
    private ScrollViewer? _sorterView;

    private Control BuildSorter()
    {
        if (_sorterStrip is null || _sorterView is null)
        {
            _sorterStrip = new SlideSorterStrip(new Thickness(_metrics.SpacingLarge), SorterFrame);
            _sorterView = new ScrollViewer
            {
                Content = _sorterStrip.Panel,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            }.Themed(ScrollViewer.BackgroundProperty, Palette.Keys.Canvas);
        }
        _sorterStrip.Update(_deck, _state.Current);
        return _sorterView;
    }

    private Border SorterFrame(Slide slide)
    {
        var index = _deck.IndexOf(slide);
        var thumb = new SlideView(_renderer, _stillImages)
        {
            Deck = _deck,
            Size = _deck.Size, DeckTheme = _deck.Theme, Master = _deck.Master, Padding = 0,
            Width = 220, Height = 124,
        };
        thumb.Chrome = _state.ChromeFor(slide);
        thumb.Fields = new PulseOffice.Presentations.SlideFieldsFactory().For(_deck, slide);
        thumb.Show(slide);

        var label = new TextBlock
        {
            Text = (index + 1).ToString(System.Globalization.CultureInfo.CurrentCulture),
            FontSize = _metrics.FontSizeSmall,
            Margin = new Thickness(_metrics.SpacingSmall / 2, 0, 0, _metrics.SpacingSmall / 2),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var current = ReferenceEquals(slide, _state.Current);
        var frame = new Border
        {
            Child = new StackPanel { Children = { label, thumb } },
            Margin = new Thickness(_metrics.SpacingMedium),
            Padding = new Thickness(_metrics.SpacingSmall),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            BorderThickness = new Thickness(current ? 2 : 1),
            Cursor = new Cursor(StandardCursorType.Hand),
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceRaised);

        frame = frame.Themed(Border.BorderBrushProperty, current ? PulseOffice.UI.Theming.AccentService.AccentKey : Palette.Keys.Border);

        frame.PointerPressed += (_, _) => _dragFrom = _deck.IndexOf(slide);
        frame.PointerReleased += (_, e) =>
        {
            if (_sorterStrip is null) return;
            var dropIndex = _sorterStrip.DropTarget(e.GetPosition(_sorterStrip.Panel));
            if (dropIndex < 0 || dropIndex == _dragFrom) _host.Select(slide);
            else if (_dragFrom >= 0 && _dragFrom < _deck.Slides.Count) _actions.MoveSlide(_deck.Slides[_dragFrom], dropIndex);
            _dragFrom = -1;
        };
        frame.DoubleTapped += (_, _) => { _host.Select(slide); _host.SetViewMode(DeckViewMode.Normal); };
        return frame;
    }

    private Control BuildNotes()
    {
        var canvas = new SlideView(_renderer)
        {
            Deck = _deck,
            Size = _deck.Size, DeckTheme = _deck.Theme, Master = _deck.Master,
            Padding = _metrics.SpacingLarge,
        };
        canvas.Chrome = _state.ChromeFor(_state.Current);
        canvas.Fields = new PulseOffice.Presentations.SlideFieldsFactory().For(_deck, _state.Current);
        canvas.Show(_state.Current);

        var notes = new TextBox
        {
            Text = _state.Current?.Notes ?? string.Empty,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            PlaceholderText = _texts["deck.notes.placeholder"],
            Margin = new Thickness(_metrics.SpacingLarge),
            MinHeight = 120,
        };
        notes.LostFocus += (_, _) =>
        {
            if (_state.Current is { } slide) _actions.SetNotes(slide, notes.Text ?? string.Empty);
        };
        _modeNotes = notes;

        var grid = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        Grid.SetRow(canvas, 0);
        Grid.SetRow(notes, 1);
        grid.Children.Add(canvas);
        grid.Children.Add(notes);

        return new Border { Child = grid }.Themed(Border.BackgroundProperty, Palette.Keys.Canvas);
    }

    private Control BuildOutline()
    {
        var editor = new TextBox
        {
            Text = BuildOutlineText(),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Menlo, Consolas, monospace"),
            Margin = new Thickness(_metrics.SpacingLarge),
            VerticalContentAlignment = VerticalAlignment.Top,
        };
        editor.LostFocus += (_, _) => ApplyOutline(editor.Text ?? string.Empty);
        _outlineEditor = editor;

        return new Border { Child = editor }.Themed(Border.BackgroundProperty, Palette.Keys.Canvas);
    }

    private string BuildOutlineText()
    {
        var lines = new List<string>();
        foreach (var slide in _deck.Slides)
        {
            lines.Add(slide.Placeholder(PlaceholderRole.Title)?.Text.PlainText ?? string.Empty);
            if (slide.Placeholder(PlaceholderRole.Body)?.Text.PlainText is { Length: > 0 } body)
                foreach (var bl in body.Replace("\r\n", "\n").Split('\n'))
                    lines.Add("\t" + bl);
        }
        _outlineOriginal = [.. lines.Select(line => line.TrimStart('\t'))];
        return string.Join(Environment.NewLine, lines);
    }

    private HashSet<string> _outlineOriginal = [];

    private string CorrectedLine(string line) => _outlineOriginal.Contains(line) ? line : _correction.Corrected(line);

    private void ApplyOutline(string text)
    {
        var slideIndex = -1;
        string? title = null;
        var body = new List<string>();
        var changes = new List<(Slide, PlaceholderRole, string)>();

        void Flush()
        {
            if (slideIndex < 0 || slideIndex >= _deck.Slides.Count) return;
            var slide = _deck.Slides[slideIndex];
            changes.Add((slide, PlaceholderRole.Title, CorrectedLine(title ?? string.Empty)));
            changes.Add((slide, PlaceholderRole.Body, string.Join(Environment.NewLine, body.Select(CorrectedLine))));
        }

        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (raw.StartsWith('\t') || raw.StartsWith("    ", StringComparison.Ordinal))
            {
                body.Add(raw.TrimStart('\t', ' '));
            }
            else
            {
                Flush();
                slideIndex++;
                title = raw;
                body = [];
            }
        }
        Flush();
        _actions.SetSlideTexts(changes);
    }
}
