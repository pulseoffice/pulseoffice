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

internal sealed class DeckModeViews(DeckViewState state, IDeckViewHost host, ISlideRenderer renderer, DeckMasterViews masters, DeckTextCorrection correction, PulseOffice.UI.Controls.Objects.IAnimatedImageProbe stillImages, IDeckOutlineText outlineText, IOutlineSlideMatching outlineMatching, IOutlineBlockMove outlineMove, IOutlineBlockTransfer outlineTransfer, DeckSectionHeaders sectionHeaders, SlideStripLayout stripLayout)
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

    private SlideDragSession? _drag;

    private SlideSorterStrip? _sorterStrip;
    private ScrollViewer? _sorterView;

    private Control BuildSorter()
    {
        if (_sorterStrip is null || _sorterView is null)
        {
            _sorterStrip = new SlideSorterStrip(new Thickness(_metrics.SpacingLarge), SorterFrame, sectionHeaders, stripLayout);
            _drag = new SlideDragSession(sectionHeaders, _deck, _actions, horizontal: true);
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

        frame.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(frame).Properties.IsLeftButtonPressed && _sorterStrip is not null && _drag is { } drag)
                drag.Begin(slide, e.GetPosition(_sorterStrip.Panel), drag.PlatformSlop(frame, e.Pointer.Type));
        };
        frame.PointerMoved += (_, e) =>
        {
            if (_sorterStrip is not null && _drag is { From: >= 0 } drag) drag.Track(_sorterStrip.Panel, e.GetPosition(_sorterStrip.Panel), _sorterStrip.Frames);
        };
        frame.PointerCaptureLost += (_, _) => _drag?.Cancel();
        frame.PointerReleased += (_, e) =>
        {
            if (_sorterStrip is null || _drag is null) return;
            if (!_drag.Finish(_sorterStrip.Panel, e.GetPosition(_sorterStrip.Panel), _sorterStrip.Frames)) _host.Select(slide);
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
        editor.AddHandler(TextBox.CuttingToClipboardEvent, (_, _) => DetachCutBlocks(editor), Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        editor.TextChanged += (_, _) => ReattachCutBlocks(editor.Text ?? string.Empty);
        _outlineEditor = editor;

        return new Border { Child = editor }.Themed(Border.BackgroundProperty, Palette.Keys.Canvas);
    }

    private string BuildOutlineText()
    {
        var text = _outlineText.Render(_deck.Slides);
        _outlineCut = [];
        _outlineDetached = [];
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

    private readonly IOutlineBlockTransfer _outlineTransfer = outlineTransfer;

    private List<(Slide Slide, OutlineEntry Original, OutlineEntry Shown)> _outlineCut = [];

    private List<Slide> _outlineDetached = [];

    private IReadOnlyList<OutlineEntry> _outlineAfterCut = [];

    private void DetachCutBlocks(TextBox editor)
    {
        var text = editor.Text ?? string.Empty;
        var start = Math.Min(editor.SelectionStart, editor.SelectionEnd);
        var length = Math.Abs(editor.SelectionEnd - editor.SelectionStart);
        var shown = _outlineText.Parse(text);
        if (_outlineTransfer.Covered(text, start, length) is not { } range
            || shown.Count != _outlineSlides.Count || shown.Count != _outlineEntries.Count) return;
        _outlineCut = [.. Enumerable.Range(range.First, range.Count).Select(index => (_outlineSlides[index], _outlineEntries[index], shown[index]))];
        _outlineDetached.AddRange(_outlineCut.Select(cut => cut.Slide));
        _outlineSlides.RemoveRange(range.First, range.Count);
        var entries = _outlineEntries.ToList();
        entries.RemoveRange(range.First, range.Count);
        _outlineEntries = entries;
        _outlineAfterCut = [.. shown.Take(range.First).Concat(shown.Skip(range.First + range.Count))];
    }

    private void ReattachCutBlocks(string text)
    {
        if (_outlineCut.Count == 0) return;
        var shown = _outlineText.Parse(text);
        if (shown.Count == _outlineAfterCut.Count) return;
        var cut = _outlineCut;
        _outlineCut = [];
        if (_outlineAfterCut.Count != _outlineSlides.Count
            || _outlineTransfer.InsertedAt(_outlineAfterCut, shown, [.. cut.Select(item => item.Shown)]) is not { } at) return;
        _outlineSlides.InsertRange(at, cut.Select(item => item.Slide));
        var entries = _outlineEntries.ToList();
        entries.InsertRange(at, cut.Select(item => item.Original));
        _outlineEntries = entries;
        foreach (var item in cut) _outlineDetached.Remove(item.Slide);
    }

    private void MoveOutlineBlock(TextBox editor, bool up)
    {
        var text = editor.Text ?? string.Empty;
        var caret = Math.Clamp(editor.CaretIndex, 0, text.Length);
        var normalized = text.Replace("\r\n", "\n");
        var line = text[..caret].Replace("\r\n", "\n").Count(c => c == '\n');
        var (block, blocks) = _outlineMove.Locate(normalized, line);
        var (moved, at) = _outlineMove.Move(normalized, line, up);
        if (moved == normalized) return;
        FollowBlockMove(block, up ? block - 1 : block + 1, blocks);
        editor.Text = moved;
        var lines = moved.Split('\n');
        editor.CaretIndex = lines.Take(at).Sum(part => part.Length + 1);
    }

    private void FollowBlockMove(int block, int other, int blocks)
    {
        if (blocks != _outlineSlides.Count || blocks != _outlineEntries.Count || block < 0 || other < 0 || block >= blocks || other >= blocks) return;
        (_outlineSlides[block], _outlineSlides[other]) = (_outlineSlides[other], _outlineSlides[block]);
        var entries = _outlineEntries.ToList();
        (entries[block], entries[other]) = (entries[other], entries[block]);
        _outlineEntries = entries;
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
        var removed = _outlineSlides.Where((_, index) => !used.Contains(index))
            .Concat(_outlineDetached.Where(slide => _deck.IndexOf(slide) >= 0)).ToList();
        _outlineDetached = [];
        _outlineCut = [];
        if (removed.Count == 0 && changes.Count == entries.Count && changes.Select(change => change.Slide).SequenceEqual(_deck.Slides)
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
