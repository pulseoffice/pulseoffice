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

internal sealed class DeckModeViews(DeckViewState state, IDeckViewHost host, ISlideRenderer renderer, DeckMasterViews masters, DeckTextCorrection correction, PulseOffice.UI.Controls.Objects.IAnimatedImageProbe stillImages, IDeckOutlineText outlineText, IOutlineSlideMatching outlineMatching, IOutlineBlockMove outlineMove, DeckSectionHeaders sectionHeaders, SlideStripLayout stripLayout)
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
            _sorterStrip = new SlideSorterStrip(new Thickness(_metrics.SpacingLarge), SorterFrame, sectionHeaders, stripLayout);
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
        editor.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.KeyModifiers != (KeyModifiers.Alt | KeyModifiers.Shift) || e.Key is not (Key.Up or Key.Down)) return;
            MoveOutlineBlock(editor, e.Key == Key.Up);
            e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _outlineEditor = editor;

        return new Border { Child = editor }.Themed(Border.BackgroundProperty, Palette.Keys.Canvas);
    }

    private string BuildOutlineText()
    {
        var text = _outlineText.Render(_deck.Slides);
        _outlineSlides = [.. _deck.Slides];
        _outlineEntries = _outlineText.Parse(text);
        _outlineOriginal = [.. _outlineText.Parse(text).SelectMany(entry => entry.Body.Select(line => line.Text).Prepend(entry.Title))];
        return text;
    }

    private readonly IDeckOutlineText _outlineText = outlineText;

    private readonly IOutlineSlideMatching _outlineMatching = outlineMatching;

    private readonly IOutlineBlockMove _outlineMove = outlineMove;

    private HashSet<string> _outlineOriginal = [];

    private List<Slide> _outlineSlides = [];

    private IReadOnlyList<OutlineEntry> _outlineEntries = [];

    private void MoveOutlineBlock(TextBox editor, bool up)
    {
        var text = editor.Text ?? string.Empty;
        var caret = Math.Clamp(editor.CaretIndex, 0, text.Length);
        var normalized = text.Replace("\r\n", "\n");
        var line = text[..caret].Replace("\r\n", "\n").Count(c => c == '\n');
        var (moved, at) = _outlineMove.Move(normalized, line, up);
        if (moved == normalized) return;
        editor.Text = moved;
        var lines = moved.Split('\n');
        editor.CaretIndex = lines.Take(at).Sum(part => part.Length + 1);
    }

    private string CorrectedLine(string line) => _outlineOriginal.Contains(line) ? line : _correction.Corrected(line);

    private void ApplyOutline(string text)
    {
        var entries = _outlineText.Parse(text)
            .Select(entry => new OutlineEntry(CorrectedLine(entry.Title), [.. entry.Body.Select(line => line with { Text = CorrectedLine(line.Text) })]))
            .ToList();
        var originals = _outlineSlides.Where(slide => _deck.IndexOf(slide) >= 0).ToList();
        IReadOnlyList<int?> matches = originals.Count == _outlineSlides.Count
            ? _outlineMatching.Match(_outlineEntries, entries)
            : [.. entries.Select((_, index) => index < originals.Count ? (int?)index : null)];
        if (originals.Count != _outlineSlides.Count) _outlineSlides = originals;

        var changes = new List<(Slide Slide, string Title, IReadOnlyList<OutlineLine> Body)>();
        for (var i = 0; i < entries.Count; i++)
            if (matches[i] is { } kept) changes.Add((_outlineSlides[kept], entries[i].Title, entries[i].Body));
        var used = matches.OfType<int>().ToHashSet();
        var removed = _outlineSlides.Where((_, index) => !used.Contains(index)).ToList();
        if (removed.Count == 0 && changes.Count == entries.Count && changes.Select(change => change.Slide).SequenceEqual(_outlineSlides)
            && entries.Count == _outlineSlides.Count && OutlineUnchanged(entries)) return;

        using var step = _actions.History.BeginTransaction(_texts["deck.action.editText"]);
        _actions.SetSlideOutline(changes);

        var order = new List<Slide>();
        for (var i = 0; i < entries.Count; i++)
        {
            if (matches[i] is { } kept) { order.Add(_outlineSlides[kept]); continue; }
            var entry = entries[i];
            if (entry.Title.Length == 0 && entry.Body.All(line => line.Text.Length == 0)) continue;
            var after = order.Count == 0 ? -1 : _deck.IndexOf(order[^1]);
            var created = _actions.InsertOutlineSlides(after,
                [new OutlineSlide(entry.Title, [.. entry.Body.Select(line => new TextParagraph(line.Text) { BulletLevel = line.Level })])],
                "deck.action.editText");
            if (created.Count > 0) order.Add(created[0]);
        }

        foreach (var slide in removed) _actions.DeleteSlide(slide);
        for (var position = 0; position < order.Count; position++)
            if (_deck.IndexOf(order[position]) != position && _deck.IndexOf(order[position]) >= 0) _actions.MoveSlide(order[position], position);

        _outlineSlides = [.. _deck.Slides];
        _outlineEntries = entries;
    }

    private bool OutlineUnchanged(IReadOnlyList<OutlineEntry> entries) =>
        entries.Count == _outlineEntries.Count
        && entries.Select((entry, index) => entry.Title == _outlineEntries[index].Title && entry.Body.SequenceEqual(_outlineEntries[index].Body)).All(same => same);
}
