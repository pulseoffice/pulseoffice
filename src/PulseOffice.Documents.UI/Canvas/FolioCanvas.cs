using PulseOffice.Foundation.Zoom;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Layout.Queries;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.UI.Controls.Text.Rich;

namespace PulseOffice.Documents.UI.Canvas;

public sealed record FolioCanvasServices(
    IPageDesk Desk,
    IPageRenderer Renderer,
    IPageSurfaceFactory Surfaces,
    ILayoutQueries Queries,
    ILineOffsets Geometry,
    IParagraphOrder Order,
    IFolioProofing Proofing,
    SpellingWaves Waves,
    IFolioObjectHits Objects,
    PulseOffice.Drawing.Shapes.IObjectManipulator Manipulator,
    IFolioObjectMover Mover,
    IFolioViewMarks Marks,
    IFolioChangeBars Bars,
    IFolioCommentMargin Comments,
    Scripting.IFolioFormEvents FormEvents,
    IFolioViewLayout ViewLayout,
    PulseOffice.Documents.Layout.Flow.IContinuousLayout Continuous,
    IFolioLineNumberMarks Lines,
    IFolioSpotlight Spotlight,
    IFolioFieldNames FieldNames,
    PulseOffice.Localization.ITextCatalog Texts,
    IFolioDrawingTools Tools,
    PulseOffice.Drawing.Freeform.IPathAuthoring Authoring,
    Commands.IFolioContentControls ContentControls,
    Editing.IFolioObjectEditing ObjectEditing,
    PulseOffice.UI.Controls.Shapes.IAdjustHandleChrome Adjusting,
    Editing.IFolioObjectLinks ObjectLinks,
    Editing.IFolioLinkTips LinkTips)
{
    public PulseOffice.Documents.Objects.IGroupMemberText GroupText { get; init; } = new PulseOffice.Documents.Objects.GroupMemberText();

    public PulseOffice.Drawing.Shapes.IObjectSnapping Snapping { get; init; } = new PulseOffice.Drawing.Shapes.ObjectSnapping();

    public ILayoutReadOnlyText ReadOnlyText { get; init; } = new LayoutReadOnlyText();

    public PulseOffice.UI.Controls.Shapes.IObjectFrameChrome Frame { get; init; } =
        new PulseOffice.UI.Controls.Shapes.ObjectFrameChrome(Manipulator, new PulseOffice.UI.Controls.Shapes.FrameStyles().Dashed);
}

public sealed class FolioCanvas : Control
{
    public static readonly StyledProperty<IBrush?> DeskProperty = AvaloniaProperty.Register<FolioCanvas, IBrush?>(nameof(Desk));

    public static readonly StyledProperty<IBrush?> AccentProperty = AvaloniaProperty.Register<FolioCanvas, IBrush?>(nameof(Accent));

    private readonly FolioEditor _editor;
    private readonly FolioCanvasServices _services;
    private readonly ScrollViewer _scroll;
    private readonly DispatcherTimer _blink;
    private readonly BoxShadows _shadow = new(new BoxShadow { OffsetY = 2, Blur = 10, Color = Color.FromArgb(46, 15, 23, 42) },
        [new BoxShadow { OffsetY = 0, Blur = 1, Color = Color.FromArgb(38, 15, 23, 42) }]);
    private DeskArrangement _arrangement = new([], 0, 0, PageDesk.PointsToPixels);
    private bool _caretShown = true;
    private bool _dragging;
    private bool _hasKeyboard;

    static FolioCanvas()
    {
        AffectsRender<FolioCanvas>(DeskProperty, AccentProperty);
    }

    public FolioCanvas(FolioEditor editor, FolioCanvasServices services, ScrollViewer scroll)
    {
        _editor = editor;
        _services = services;
        _scroll = scroll;
        _overlays = new FolioTextOverlays(editor, services);
        _objectGestures = new FolioObjectGestures(this, editor, services);
        Focusable = false;
        Cursor = new Cursor(StandardCursorType.Ibeam);
        _editor.Brush.Changed += (_, _) => Cursor = new Cursor(_editor.Brush.Sample is null ? StandardCursorType.Ibeam : StandardCursorType.Cross);
        _blink = new DispatcherTimer(TimeSpan.FromMilliseconds(530), DispatcherPriority.Render, (_, _) =>
        {
            _caretShown = !_caretShown;
            InvalidateVisual();
        });
        AttachedToVisualTree += (_, _) =>
        {
            _services.Marks.Changed += OnMarksChanged;
            _services.ViewLayout.Changed += OnMarksChanged;
            _services.Spotlight.Changed += OnMarksChanged;
            _services.FieldNames.Changed += OnMarksChanged;
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _services.Marks.Changed -= OnMarksChanged;
            _services.ViewLayout.Changed -= OnMarksChanged;
            _services.Spotlight.Changed -= OnMarksChanged;
            _services.FieldNames.Changed -= OnMarksChanged;
        };
        _scroll.ScrollChanged += (_, e) =>
        {
            if (e.ViewportDelta != default) InvalidateMeasure();
            InvalidateVisual();
        };
    }

    private void OnMarksChanged(object? sender, EventArgs args)
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    public IBrush? Desk
    {
        get => GetValue(DeskProperty);
        set => SetValue(DeskProperty, value);
    }

    public IBrush? Accent
    {
        get => GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    public ViewZoom Zoom { get; private set; } = ZoomLimits.Pages.At(1);

    public double Scale => _arrangement.Scale;

    public bool HasKeyboard
    {
        get => _hasKeyboard;
        set
        {
            _hasKeyboard = value;
            UpdateBlink();
        }
    }

    public bool IsBlinking => _blink.IsEnabled;

    public event EventHandler? PointerPlaced;

    public event EventHandler? MenuRequested;

    public void SetZoom(ViewZoom zoom)
    {
        Zoom = zoom;
        InvalidateMeasure();
        InvalidateVisual();
    }

    public void Wake()
    {
        _caretShown = true;
        if (_blink.IsEnabled)
        {
            _blink.Stop();
            _blink.Start();
        }
        InvalidateVisual();
    }

    public void Refresh()
    {
        InvalidateMeasure();
        Wake();
    }

    public Rect CaretRectangle
    {
        get
        {
            if (Caret() is not { } caret) return new Rect(0, 0, 1, 16);
            return caret;
        }
    }

    public void EnsureCaretVisible()
    {
        if (Caret() is not { } caret) return;
        var offset = _scroll.Offset;
        var viewport = _scroll.Viewport;
        var y = offset.Y;
        if (caret.Top < offset.Y + 8) y = caret.Top - 8;
        else if (caret.Bottom > offset.Y + viewport.Height - 8) y = caret.Bottom - viewport.Height + 8;
        var x = offset.X;
        if (caret.Left < offset.X) x = caret.Left - 16;
        else if (caret.Right > offset.X + viewport.Width) x = caret.Right - viewport.Width + 16;
        _scroll.Offset = new Vector(Math.Max(0, x), Math.Max(0, y));
    }

    public int VisibleLines()
    {
        var caret = Caret();
        var height = caret?.Height ?? 16;
        return Math.Max(1, (int)(_scroll.Viewport.Height / Math.Max(4, height)) - 1);
    }

    private double Sidebar => _services.Comments.Shows(_editor.Document) ? _services.Comments.Width : 0;

    private readonly Dictionary<int, IReadOnlyList<CommentBalloon>> _balloons = [];

    private readonly FolioTextOverlays _overlays;

    private readonly FolioObjectGestures _objectGestures;

    protected override Size MeasureOverride(Size availableSize)
    {
        var sidebar = Sidebar;
        var pages = _editor.DisplayLayout.Pages.Select(page => (page.Width + sidebar, page.Height)).ToList();
        var viewport = _scroll.Viewport;
        var width = viewport.Width > 0 ? viewport.Width : double.IsFinite(availableSize.Width) ? availableSize.Width : 900;
        var height = viewport.Height > 0 ? viewport.Height : double.IsFinite(availableSize.Height) ? availableSize.Height : 700;
        if (FitWeb(width)) pages = [.. _editor.DisplayLayout.Pages.Select(page => (page.Width + sidebar, page.Height))];
        var scale = _services.Desk.ScaleFor(Zoom, pages, width, height);
        _arrangement = _services.Desk.Arrange(pages, scale, width, Arrangement(), Crops());
        return new Size(_arrangement.Width, _arrangement.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Math.Abs(finalSize.Width - _arrangement.Width) > 0.5)
        {
            var sidebar = Sidebar;
            var pages = _editor.DisplayLayout.Pages.Select(page => (page.Width + sidebar, page.Height)).ToList();
            _arrangement = _services.Desk.Arrange(pages, _arrangement.Scale, finalSize.Width, Arrangement(), Crops());
        }
        return finalSize;
    }

    public IReadOnlyList<PageSlot> Slots => _arrangement.Pages;

    private DeskLayout Arrangement() => _services.ViewLayout.Mode == FolioViewMode.Print ? _services.ViewLayout.Desk : DeskLayout.Single;

    private IReadOnlyList<(double Top, double Bottom)>? Crops() =>
        _services.ViewLayout.Mode == FolioViewMode.Print && !_services.ViewLayout.Whitespace
            ? [.. _editor.DisplayLayout.Pages.Select(page => (Math.Max(0, page.Content.Y - 4), Math.Max(0, page.Height - page.Content.Bottom - 4)))]
            : null;

    private bool FitWeb(double viewportWidth)
    {
        PulseOffice.Documents.Layout.Flow.ContinuousPage? wanted = _services.ViewLayout.Mode switch
        {
            FolioViewMode.Web => new(Math.Max(144, Math.Round((viewportWidth - PageDesk.Margin * 2 - Sidebar) / ((Zoom.Mode == ViewZoomMode.Fixed ? Zoom.Factor : 1) * PageDesk.PointsToPixels)))),
            FolioViewMode.Draft => new(null),
            _ => null,
        };
        var current = _services.Continuous.Page;
        if (current == wanted || current?.Width is { } a && wanted?.Width is { } b && Math.Abs(a - b) < 1) return false;
        _services.Continuous.Page = wanted;
        _editor.Reflow();
        return true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateBlink();
    }

    private void UpdateBlink()
    {
        if (_hasKeyboard && VisualRoot is not null)
        {
            if (!_blink.IsEnabled) _blink.Start();
            return;
        }
        _blink.Stop();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _blink.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Desk ?? Brushes.LightGray, new Rect(Bounds.Size));
        var layout = _editor.DisplayLayout;
        var top = _scroll.Offset.Y - 50;
        var bottom = _scroll.Viewport.Height > 0 ? _scroll.Offset.Y + _scroll.Viewport.Height + 50 : Bounds.Height;
        var cells = _editor.Preedit is null ? _editor.CellRange() : null;
        var selection = cells is null ? _overlays.SelectionMap() : [];
        var marks = _services.FieldNames.Apply(_services.Spotlight.Apply(_services.Lines.Apply(_services.Marks.Current with { ChangeBar = _services.Bars.For(_editor.Document) }, _editor.Document, layout), _editor.Document));
        var sheet = _services.Comments.Shows(_editor.Document) ? _services.Comments.Sheet(_editor.Document) : null;
        var current = sheet is null ? null : sheet.Anchors.LastOrDefault(anchor => _overlays.Covers(anchor, _editor.Caret))?.Comment.Id;
        var controls = _overlays.ControlRanges(layout, marks.FieldShadings);
        _balloons.Clear();
        foreach (var slot in _arrangement.Pages.Where(slot => slot.Intersects(top, bottom)))
        {
            if (slot.Index >= layout.Pages.Count) continue;
            var page = layout.Pages[slot.Index];
            var rect = new Rect(slot.X, slot.VisibleTop, page.Width * Scale, slot.VisibleBottom - slot.VisibleTop);
            context.DrawRectangle(Brushes.White, null, new RoundedRect(rect), _shadow);
            using var shift = context.PushTransform(Matrix.CreateScale(Scale, Scale) * Matrix.CreateTranslation(slot.X, slot.Y));
            using (context.PushClip(new Rect(0, slot.CropTop / Scale, page.Width, (slot.Height - slot.CropTop - slot.CropBottom) / Scale)))
            {
                if (sheet is not null) _overlays.PaintCommentRanges(context, page, sheet);
                _overlays.PaintControls(context, page, controls, Accent);
                _overlays.PaintSelection(context, page, selection, Accent);
                _overlays.PaintLayoutText(context, page, Accent);
                _services.Renderer.Paint(_services.Surfaces.Screen(context, _editor.Document.Language), page, Content, marks);
                _overlays.PaintGrids(context, page, (top - slot.Y) / Scale, (bottom - slot.Y) / Scale, Scale, Accent, _objectGestures.Helpline(page.Index));
                if (cells is not null) _overlays.PaintCells(context, page, cells, Accent);
                PaintColumnDrag(context, slot);
                _overlays.PaintWaves(context, page);
                _objectGestures.PaintObjectSelection(context, page, Accent);
                _objectGestures.PaintAdjustHandles(context, page);
                _objectGestures.PaintOtherSelections(context, page, Accent);
            }
            if (sheet is null) continue;
            var balloons = _services.Comments.Place(_editor.Document, sheet, page);
            _balloons[slot.Index] = balloons;
            _services.Comments.Paint(context, page, balloons, current, Scale);
        }
        PaintCaret(context, layout);
        PaintOffer(context);
        PaintSketch(context);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsRightButtonPressed)
        {
            PointerPlaced?.Invoke(this, EventArgs.Empty);
            if (_objectGestures.ObjectAt(point.Position) is { } target)
            {
                _editor.SelectObject(target.Box.ObjectId);
                MenuRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }
            if (PositionAt(point.Position) is { } at && !Inside(at)) _editor.PlaceCaret(at, false);
            MenuRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }
        if (!point.Properties.IsLeftButtonPressed) return;
        PointerPlaced?.Invoke(this, EventArgs.Empty);
        if (BeginDrawing(point.Position, e)) return;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && _objectGestures.ObjectAt(point.Position) is { } added)
        {
            _editor.ToggleObject(added.Box.ObjectId);
            e.Handled = true;
            return;
        }
        if (_offer is { } offer && _editor.SelectedObjectIds.Count == 0 && offer.Button.Contains(point.Position))
        {
            _offer = null;
            RegionRequested?.Invoke(this, offer);
            e.Handled = true;
            return;
        }
        if (BalloonAt(point.Position) is { } balloon && _services.Comments.Sheet(_editor.Document).Anchors.FirstOrDefault(anchor => anchor.Comment.Id == balloon) is { } chosen)
        {
            _editor.Select(chosen.Start, chosen.End);
            e.Handled = true;
            return;
        }
        if (FollowsLink(e.KeyModifiers) && _objectGestures.ObjectAt(point.Position) is { } linked && _objectGestures.PointMember(linked, point.Position) is { } member
            && _services.ObjectLinks.EffectiveLink(_editor, member) is not null)
        {
            _services.ObjectLinks.Follow(_editor, member);
            e.Handled = true;
            return;
        }
        if (_objectGestures.ObjectAt(point.Position) is { } control && _services.FormEvents.Activate(_editor, control.Box.ObjectId))
        {
            e.Handled = true;
            return;
        }
        if (_objectGestures.BeginAdjust(point.Position, e.ClickCount) || _objectGestures.BeginObjectGesture(point.Position, e.ClickCount))
        {
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }
        if (e.ClickCount >= 2 && _objectGestures.ObjectAt(point.Position) is { } framed && _editor.Objects.EnterFrame(framed.Box.ObjectId, PositionAt(point.Position)))
        {
            e.Handled = true;
            return;
        }
        if (_objectGestures.ObjectAt(point.Position) is { } hit)
        {
            _editor.SelectObject(hit.Box.ObjectId);
            _objectGestures.PointMember(hit, point.Position);
            if (_objectGestures.BeginObjectGesture(point.Position, e.ClickCount)) e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }
        if (e.ClickCount == 1 && !_editor.ReadOnly() && BoundaryAt(point.Position) is { } boundary)
        {
            _column = boundary;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }
        if (_services.ViewLayout.DirectCursor && e.ClickCount == 1 && !_editor.ReadOnly() && DirectTarget(point.Position) is { } direct)
        {
            DirectCursorRequested?.Invoke(this, direct);
            e.Handled = true;
            return;
        }
        if (_services.ViewLayout.BlockSelection && e.ClickCount == 1 && _arrangement.Pages.FirstOrDefault(slot => slot.Contains(point.Position.X, point.Position.Y)) is { } blockSlot)
        {
            _block = (blockSlot.Index, point.Position, point.Position);
            _dragging = true;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }
        if (ReadOnlyTextAt(point.Position) is { } notice)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && PositionAt(point.Position) is { } reach) _editor.Select(_editor.Anchor, reach, notice);
            else _editor.SelectLayoutText(notice);
            e.Handled = true;
            return;
        }
        if (PositionAt(point.Position) is not { } position) return;
        if (e.ClickCount == 1 && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && _services.ContentControls.Activate(_editor, position))
        {
            e.Handled = true;
            return;
        }
        if (e.ClickCount >= 3) _editor.SelectParagraph(position);
        else if (e.ClickCount == 2) _editor.SelectWord(position);
        else
        {
            _editor.PlaceCaret(position, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            _dragging = true;
            e.Pointer.Capture(this);
        }
        e.Handled = true;
    }

    private bool Inside(PulseOffice.Documents.Editing.DocumentPosition position)
    {
        if (!_editor.HasSelection) return false;
        var range = _editor.Selection;
        var order = _editor.Services.Order;
        return order.Compare(_editor.Document, range.Start, position) <= 0 && order.Compare(_editor.Document, position, range.End) <= 0;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Drawing && Sketch.Active && PagePoint(e.GetPosition(this), _sketchPage) is { } drawn)
        {
            Sketch.Move(_services.Tools.Tool, drawn, e.GetCurrentPoint(this).Properties.IsLeftButtonPressed);
            InvalidateVisual();
            e.Handled = true;
            return;
        }
        if (_objectGestures.Drag(e.GetPosition(this), e.KeyModifiers.HasFlag(KeyModifiers.Shift)))
        {
            InvalidateVisual();
            e.Handled = true;
            return;
        }
        if (_column is { } column)
        {
            _column = column with { Preview = (e.GetPosition(this).X - column.Slot.X) / Scale };
            InvalidateVisual();
            e.Handled = true;
            return;
        }
        if (!_dragging || !ReferenceEquals(e.Pointer.Captured, this))
        {
            UpdateOffer(e.GetPosition(this));
            UpdateCursor(e.GetPosition(this));
            ShowLinkTip(e.GetPosition(this));
            return;
        }
        if (_block is { } block)
        {
            _block = block with { To = e.GetPosition(this) };
            SelectBlock();
            e.Handled = true;
            return;
        }
        if (PositionAt(e.GetPosition(this)) is { } position) _editor.Select(_editor.Anchor, position, ReadOnlyTextAt(e.GetPosition(this)));
        e.Handled = true;
    }

    private (int Page, Point From, Point To)? _block;

    public event EventHandler<PulseOffice.Documents.Editing.DirectCursorTarget>? DirectCursorRequested;

    private PulseOffice.Documents.Editing.DirectCursorTarget? DirectTarget(Point point)
    {
        var layout = _editor.DisplayLayout;
        if (_arrangement.Pages.FirstOrDefault(slot => slot.Contains(point.X, point.Y)) is not { } slot || slot.Index != layout.Pages.Count - 1) return null;
        var page = layout.Pages[slot.Index];
        var x = (point.X - slot.X) / Scale;
        var y = (point.Y - slot.Y) / Scale;
        var last = page.Lines.Where(line => line.Story == PulseOffice.Documents.Layout.Tree.StoryKind.Body).MaxBy(line => line.Top + line.Line.Height);
        if (last is null) return null;
        var bottom = last.Top + last.Line.Height;
        if (y <= bottom + 2 || y >= page.Content.Bottom || x < page.Content.X || x > page.Content.Right) return null;
        var lines = (int)Math.Ceiling((y - bottom) / Math.Max(1, last.Line.Height));
        return new PulseOffice.Documents.Editing.DirectCursorTarget(last.ParagraphId, lines, x - page.Content.X);
    }

    private void SelectBlock()
    {
        if (_block is not { } block || block.Page >= _arrangement.Pages.Count) return;
        var slot = _arrangement.Pages[block.Page];
        var left = (Math.Min(block.From.X, block.To.X) - slot.X) / Scale;
        var right = (Math.Max(block.From.X, block.To.X) - slot.X) / Scale;
        var top = (Math.Min(block.From.Y, block.To.Y) - slot.Y) / Scale;
        var bottom = (Math.Max(block.From.Y, block.To.Y) - slot.Y) / Scale;
        var ranges = _services.Queries.Block(_editor.DisplayLayout, block.Page, left, top, right, bottom);
        if (ranges.Count > 0) _editor.SelectBlock(ranges);
        InvalidateVisual();
    }

    private sealed record ColumnDrag(PulseOffice.Documents.Blocks.TableBlock Table, int Column, double Edge, double Top, double Bottom, PageSlot Slot)
    {
        public double Preview { get; init; } = Edge;
    }

    private ColumnDrag? _column;

    private PulseOffice.UI.Controls.Drawing.FreeformSketch? _sketch;
    private int _sketchPage = -1;

    private PulseOffice.UI.Controls.Drawing.FreeformSketch Sketch => _sketch ??= new(_services.Authoring, 6 / Math.Max(0.1, Scale));

    public event EventHandler<DrawnSketch>? Drawn;

    private bool Drawing => _services.Tools.Tool is not (PulseOffice.UI.Controls.Drawing.DrawingTool.None or PulseOffice.UI.Controls.Drawing.DrawingTool.TextBox);

    public void CancelDrawing()
    {
        Sketch.Cancel();
        _sketchPage = -1;
        _services.Tools.Finish();
        InvalidateVisual();
    }

    public bool CompleteDrawing()
    {
        if (!Sketch.Active) return false;
        Deliver(Sketch.Complete(_services.Tools.Tool, _services.Tools.Filled, false));
        return true;
    }

    private PulseOffice.Drawing.Shapes.ShapePoint? PagePoint(Point point, int page)
    {
        if (page < 0 || page >= _arrangement.Pages.Count) return null;
        var slot = _arrangement.Pages[page];
        return new PulseOffice.Drawing.Shapes.ShapePoint((point.X - slot.X) / Scale, (point.Y - slot.Y) / Scale);
    }

    private bool BeginDrawing(Point point, PointerPressedEventArgs e)
    {
        if (!Drawing || _editor.ReadOnly()) return false;
        e.Handled = true;
        var page = Sketch.Active ? _sketchPage : _arrangement.Pages.FirstOrDefault(slot => slot.Contains(point.X, point.Y))?.Index ?? -1;
        if (PagePoint(point, page) is not { } at) return true;
        _sketchPage = page;
        if (!Sketch.Begin(_services.Tools.Tool, at, e.ClickCount, _services.Tools.Filled, out var done)) return true;
        if (_services.Tools.Tool is PulseOffice.UI.Controls.Drawing.DrawingTool.Freehand or PulseOffice.UI.Controls.Drawing.DrawingTool.Frame or PulseOffice.UI.Controls.Drawing.DrawingTool.Connector)
            e.Pointer.Capture(this);
        if (done is not null || !Sketch.Active) Deliver(done);
        InvalidateVisual();
        return true;
    }

    private void Deliver(PulseOffice.UI.Controls.Drawing.SketchResult? result)
    {
        var page = _sketchPage;
        var tool = _services.Tools.Tool;
        var filled = _services.Tools.Filled;
        _sketchPage = -1;
        _services.Tools.Finish();
        if (result is not null && page >= 0) Drawn?.Invoke(this, new DrawnSketch(page, tool, filled, result));
        InvalidateVisual();
    }

    private void PaintSketch(DrawingContext context)
    {
        if (!Sketch.Active || _sketchPage < 0 || _sketchPage >= _arrangement.Pages.Count) return;
        var slot = _arrangement.Pages[_sketchPage];
        Sketch.Paint(context, point => new Point(slot.X + point.X * Scale, slot.Y + point.Y * Scale), Accent ?? Brushes.DodgerBlue);
    }

    public sealed record RegionOffer(int Page, bool Header, Rect Button);

    private RegionOffer? _offer;

    public event EventHandler<RegionOffer>? RegionRequested;

    private void UpdateOffer(Point point)
    {
        RegionOffer? offer = null;
        if (_services.ViewLayout.HeaderFooterMenu && _services.ViewLayout.Mode == FolioViewMode.Print && !_editor.DocumentLocked() && _editor.SelectedObjectIds.Count == 0
            && _arrangement.Pages.FirstOrDefault(slot => slot.Contains(point.X, point.Y)) is { } slot && slot.Index < _editor.DisplayLayout.Pages.Count)
        {
            var page = _editor.DisplayLayout.Pages[slot.Index];
            var y = (point.Y - slot.Y) / Scale;
            var style = _editor.Document.Styles.Page.Find(page.StyleName);
            var header = y < page.Content.Y;
            var footer = y > page.Content.Bottom;
            if (style is not null && (header && !style.Header.Enabled || footer && !style.Footer.Enabled))
            {
                var edge = header ? page.Content.Y : page.Content.Bottom;
                var x = slot.X + page.Content.X * Scale;
                var top = slot.Y + edge * Scale - (header ? 26 : -4);
                offer = new RegionOffer(slot.Index, header, new Rect(x, top, 150, 22));
            }
        }
        if (offer == _offer) return;
        _offer = offer;
        InvalidateVisual();
    }

    private void PaintOffer(DrawingContext context)
    {
        if (_offer is not { } offer || offer.Page >= _arrangement.Pages.Count || _editor.SelectedObjectIds.Count > 0) return;
        var slot = _arrangement.Pages[offer.Page];
        var accent = Accent ?? Brushes.DodgerBlue;
        var lineY = offer.Header ? offer.Button.Bottom + 4 : offer.Button.Top - 4;
        context.DrawLine(new Pen(accent, 1, new DashStyle([4, 3], 0)), new Point(slot.X, lineY), new Point(slot.X + slot.Width, lineY));
        context.DrawRectangle(accent, null, offer.Button, 11, 11);
        var label = new FormattedText(_services.Texts[offer.Header ? "folio.region.addHeader" : "folio.region.addFooter"],
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface.Default, 11, Brushes.White);
        context.DrawText(label, new Point(offer.Button.X + (offer.Button.Width - label.Width) / 2, offer.Button.Y + (offer.Button.Height - label.Height) / 2));
    }

    private void UpdateCursor(Point point)
    {
        var wanted = _editor.Brush.Sample is not null || Drawing ? StandardCursorType.Cross
            : !_editor.ReadOnly() && BoundaryAt(point) is not null ? StandardCursorType.SizeWestEast
            : StandardCursorType.Ibeam;
        if (_cursor == wanted) return;
        _cursor = wanted;
        Cursor = new Cursor(wanted);
    }

    private StandardCursorType _cursor = StandardCursorType.Ibeam;

    private void ShowLinkTip(Point point) =>
        _services.LinkTips.Show(this, _objectGestures.ObjectAt(point) is { } hit
            ? _objectGestures.LocateMember(hit, point) is { } target ? _services.LinkTips.OfObject(_editor, target) : null
            : CharacterAt(point) is { } position ? _services.LinkTips.At(_editor, position) : null);

    private DocumentPosition? CharacterAt(Point point)
    {
        if (SlotAt(point) is not { } slot) return null;
        return _services.Queries.CharacterAt(_editor.DisplayLayout, slot.Index, (point.X - slot.X) / Scale, (point.Y - slot.Y) / Scale);
    }

    private ColumnDrag? BoundaryAt(Point point)
    {
        var layout = _editor.DisplayLayout;
        if (SlotAt(point) is not { } slot || slot.Index >= layout.Pages.Count) return null;
        var x = (point.X - slot.X) / Scale;
        var y = (point.Y - slot.Y) / Scale;
        var reach = 3 / Scale;
        foreach (var table in layout.Pages[slot.Index].Tables)
        {
            if (y < table.Bounds.Y || y > table.Bounds.Bottom) continue;
            foreach (var cell in table.Cells)
            {
                if (y < cell.Bounds.Y || y > cell.Bounds.Bottom || Math.Abs(x - cell.Bounds.Right) > reach) continue;
                if (cell.Lines.FirstOrDefault() is not { } line || _editor.Tables.Editing.Locate(_editor.Document, line.ParagraphId) is not { } at) continue;
                var column = at.Column + Math.Max(1, at.Table.Cell(at.Row, at.Column).ColumnSpan) - 1;
                return new ColumnDrag(at.Table, column, cell.Bounds.Right, table.Bounds.Y, table.Bounds.Bottom, slot);
            }
        }
        return null;
    }

    private void PaintColumnDrag(DrawingContext context, PageSlot slot)
    {
        if (_column is not { } drag || drag.Slot.Index != slot.Index) return;
        var pen = new Pen(Accent ?? Brushes.DodgerBlue, 1.5 / Scale) { DashStyle = DashStyle.Dash };
        context.DrawLine(pen, new Point(drag.Preview, drag.Top), new Point(drag.Preview, drag.Bottom));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (Drawing && Sketch.Active && Sketch.Release(_services.Tools.Tool, _services.Tools.Filled) is { } released)
        {
            e.Pointer.Capture(null);
            Deliver(released);
            e.Handled = true;
            return;
        }
        if (_column is { } column)
        {
            _column = null;
            e.Pointer.Capture(null);
            var current = column.Column < column.Table.Columns.Count ? column.Table.Columns[column.Column].ToPoints() : 0;
            if (Math.Abs(column.Preview - column.Edge) > 0.5) _editor.Cells.ResizeColumn(column.Table, column.Column, current + column.Preview - column.Edge);
            InvalidateVisual();
            e.Handled = true;
            return;
        }
        if (_objectGestures.Release(e.GetPosition(this), e.Pointer))
        {
            InvalidateVisual();
            e.Handled = true;
            return;
        }
        if (!_dragging) return;
        _dragging = false;
        _block = null;
        e.Pointer.Capture(null);
        if (_editor.Brush.Sample is not null) _editor.Brush.Paint();
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
        _objectGestures.Cancel();
        _column = null;
    }

    private PulseOffice.Document.Embedding.EmbeddedObject? Content(string objectId) =>
        _editor.Document.Objects.Find(objectId)?.Content is PulseOffice.Documents.Objects.EmbeddedContent embedded ? _services.GroupText.Painted(embedded) : null;

    internal PageSlot? SlotAt(Point point) =>
        _arrangement.Pages.FirstOrDefault(candidate => candidate.Contains(point.X, point.Y));

    private bool FollowsLink(KeyModifiers modifiers) =>
        (modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta)) && !modifiers.HasFlag(KeyModifiers.Shift);

    private LayoutText? ReadOnlyTextAt(Point point) =>
        SlotAt(point) is { } slot ? _services.ReadOnlyText.At(_editor.DisplayLayout, slot.Index, (point.X - slot.X) / Scale, (point.Y - slot.Y) / Scale) : null;

    public DocumentPosition? PositionAt(Point point)
    {
        if (_arrangement.Pages.Count == 0) return null;
        var slot = _arrangement.Pages.FirstOrDefault(candidate => candidate.Contains(point.X, point.Y))
            ?? _arrangement.Pages.MinBy(candidate => Distance(candidate, point))!;
        var x = (point.X - slot.X) / Scale;
        var y = Math.Clamp((point.Y - slot.Y) / Scale, slot.CropTop / Scale, (slot.Height - slot.CropBottom) / Scale);
        return _services.Queries.HitTest(_editor.DisplayLayout, slot.Index, x, y);
    }

    public (PageSlot Slot, PageBox Page)? CaretPage()
    {
        var layout = _editor.DisplayLayout;
        if (layout.Pages.Count == 0 || _arrangement.Pages.Count == 0) return null;
        var index = _services.Queries.Caret(layout, _editor.DisplayCaret)?.Page ?? 0;
        if (index >= layout.Pages.Count || index >= _arrangement.Pages.Count) index = 0;
        return (_arrangement.Pages[index], layout.Pages[index]);
    }

    private double Distance(PageSlot slot, Point point)
    {
        var dx = Math.Max(0, Math.Max(slot.X - point.X, point.X - slot.X - slot.Width));
        var dy = Math.Max(0, Math.Max(slot.VisibleTop - point.Y, point.Y - slot.VisibleBottom));
        return dx * dx + dy * dy;
    }

    private Rect? Caret()
    {
        var layout = _editor.DisplayLayout;
        if (_services.Queries.Caret(layout, _editor.DisplayCaret) is not { } caret || caret.Page >= _arrangement.Pages.Count) return null;
        var slot = _arrangement.Pages[caret.Page];
        return new Rect(slot.X + caret.X * Scale, slot.Y + caret.Y * Scale, Math.Max(1, Scale), Math.Max(4, caret.Height * Scale));
    }

    private void PaintCaret(DrawingContext context, DocumentLayout layout)
    {
        if (Caret() is not { } caret) return;
        var composing = _editor.Preedit is not null;
        if (!HasKeyboard || !_caretShown || _editor.HasSelection && !composing) return;
        using (CaretTurn(context, layout)) context.FillRectangle(Brushes.Black, new Rect(caret.X - 0.75, caret.Y, 1.5, caret.Height));
    }

    private DrawingContext.PushedState? CaretTurn(DrawingContext context, DocumentLayout layout)
    {
        if (_services.Queries.Caret(layout, _editor.DisplayCaret) is not { } caret || caret.Page >= layout.Pages.Count || caret.Page >= _arrangement.Pages.Count) return null;
        var paragraph = _editor.DisplayCaret.ParagraphId;
        var owner = layout.Pages[caret.Page].TurnedFrames.FirstOrDefault(item => item.Lines.Any(line => line.ParagraphId == paragraph && caret.Y >= line.Top - 0.5 && caret.Y <= line.Top + line.Line.Height + 0.5));
        if (owner is null) return null;
        var slot = _arrangement.Pages[caret.Page];
        var bounds = new Rect(slot.X + owner.Bounds.X * Scale, slot.Y + owner.Bounds.Y * Scale, owner.Bounds.Width * Scale, owner.Bounds.Height * Scale);
        return context.PushTransform(new ObjectTransform(owner.RotationDegrees).Around(bounds));
    }

    public string? BalloonAt(Point point)
    {
        if (SlotAt(point) is not { } slot || !_balloons.TryGetValue(slot.Index, out var balloons)) return null;
        var local = new Point((point.X - slot.X) / Scale, (point.Y - slot.Y) / Scale);
        return balloons.FirstOrDefault(balloon => balloon.Bounds.Contains(local))?.Anchor.Comment.Id;
    }
}
