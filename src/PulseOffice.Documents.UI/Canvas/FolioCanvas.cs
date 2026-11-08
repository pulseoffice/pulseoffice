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
    Editing.IFolioObjectLinks ObjectLinks)
{
    public PulseOffice.Documents.Objects.IGroupMemberText GroupText { get; init; } = new PulseOffice.Documents.Objects.GroupMemberText();

    public PulseOffice.Drawing.Shapes.IObjectSnapping Snapping { get; init; } = new PulseOffice.Drawing.Shapes.ObjectSnapping();

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
        Focusable = false;
        Cursor = new Cursor(StandardCursorType.Ibeam);
        _editor.BrushChanged += (_, _) => Cursor = new Cursor(_editor.Brush is null ? StandardCursorType.Ibeam : StandardCursorType.Cross);
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
        var selection = cells is null ? SelectionMap() : [];
        var marks = _services.FieldNames.Apply(_services.Spotlight.Apply(_services.Lines.Apply(_services.Marks.Current with { ChangeBar = _services.Bars.For(_editor.Document) }, _editor.Document, layout), _editor.Document));
        var sheet = _services.Comments.Shows(_editor.Document) ? _services.Comments.Sheet(_editor.Document) : null;
        var current = sheet is null ? null : sheet.Anchors.LastOrDefault(anchor => Covers(anchor, _editor.Caret))?.Comment.Id;
        var controls = ControlRanges(layout, marks.FieldShadings);
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
                if (sheet is not null) PaintCommentRanges(context, page, sheet);
                PaintControls(context, page, controls);
                PaintSelection(context, page, selection);
                _services.Renderer.Paint(_services.Surfaces.Screen(context, _editor.Document.Language), page, Content, marks);
                PaintGrids(context, page);
                if (cells is not null) PaintCells(context, page, cells);
                PaintColumnDrag(context, slot);
                PaintWaves(context, page);
                PaintObjectSelection(context, page);
                PaintAdjustHandles(context, page);
                PaintOtherSelections(context, page);
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
            if (ObjectAt(point.Position) is { } target)
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
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && ObjectAt(point.Position) is { } added)
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
        if (FollowsLink(e.KeyModifiers) && ObjectAt(point.Position) is { } linked && PointMember(linked, point.Position) is { } member
            && _services.ObjectLinks.EffectiveLink(_editor, member) is not null)
        {
            _services.ObjectLinks.Follow(_editor, member);
            e.Handled = true;
            return;
        }
        if (ObjectAt(point.Position) is { } control && _services.FormEvents.Activate(_editor, control.Box.ObjectId))
        {
            e.Handled = true;
            return;
        }
        if (BeginAdjust(point.Position, e.ClickCount) || BeginObjectGesture(point.Position, e.ClickCount))
        {
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }
        if (e.ClickCount >= 2 && ObjectAt(point.Position) is { } framed && _editor.EnterFrame(framed.Box.ObjectId, PositionAt(point.Position)))
        {
            e.Handled = true;
            return;
        }
        if (ObjectAt(point.Position) is { } hit)
        {
            _editor.SelectObject(hit.Box.ObjectId);
            PointMember(hit, point.Position);
            if (BeginObjectGesture(point.Position, e.ClickCount)) e.Pointer.Capture(this);
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
        if (_adjust is { } adjusting)
        {
            var at = e.GetPosition(this);
            _adjust = adjusting with
            {
                Grip = _services.Adjusting.Drag(adjusting.Grip, adjusting.Frame,
                    new Point((at.X - adjusting.Slot.X) / Scale, (at.Y - adjusting.Slot.Y) / Scale)),
            };
            InvalidateVisual();
            e.Handled = true;
            return;
        }
        if (_gesture is { } gesture)
        {
            var at = e.GetPosition(this);
            var dx = (at.X - gesture.Origin.X) / Scale;
            var dy = (at.Y - gesture.Origin.Y) / Scale;
            var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            var session = gesture.Session;
            session.Track(dx, dy, (at.X - gesture.Slot.X) / Scale, (at.Y - gesture.Slot.Y) / Scale, shift);
            session.Settle(Snap(session, gesture.Slot.Index));
            _gesture = gesture with { Moved = gesture.Moved || !session.Moving || Math.Abs(dx) + Math.Abs(dy) > 2 / Scale };
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
            return;
        }
        if (_block is { } block)
        {
            _block = block with { To = e.GetPosition(this) };
            SelectBlock();
            e.Handled = true;
            return;
        }
        if (PositionAt(e.GetPosition(this)) is { } position) _editor.PlaceCaret(position, extend: true);
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
        var wanted = _editor.Brush is not null || Drawing ? StandardCursorType.Cross
            : !_editor.ReadOnly() && BoundaryAt(point) is not null ? StandardCursorType.SizeWestEast
            : StandardCursorType.Ibeam;
        if (_cursor == wanted) return;
        _cursor = wanted;
        Cursor = new Cursor(wanted);
    }

    private StandardCursorType _cursor = StandardCursorType.Ibeam;

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

    private void PaintCells(DrawingContext context, PageBox page, PulseOffice.Documents.Editing.Tables.TableSelection cells)
    {
        var brush = new SolidColorBrush(Accent is ISolidColorBrush accent ? accent.Color : Colors.DodgerBlue, 0.22);
        foreach (var table in page.Tables.Where(table => table.TableId == cells.Table.Id))
            foreach (var cell in table.Cells.Where(cell => cells.Range.Contains(cell.Row, cell.Column)))
                context.FillRectangle(brush, new Rect(cell.Bounds.X, cell.Bounds.Y, cell.Bounds.Width, cell.Bounds.Height));
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
            if (Math.Abs(column.Preview - column.Edge) > 0.5) _editor.ResizeColumn(column.Table, column.Column, current + column.Preview - column.Edge);
            InvalidateVisual();
            e.Handled = true;
            return;
        }
        if (_adjust is { } adjusted)
        {
            _adjust = null;
            e.Pointer.Capture(null);
            if (adjusted.Grip.Changed)
                _services.ObjectEditing.Replace(_editor, value => value is PulseOffice.Drawing.Shapes.ShapeObject ? adjusted.Grip.Preview : value);
            InvalidateVisual();
            e.Handled = true;
            return;
        }
        if (_gesture is { } done)
        {
            _gesture = null;
            e.Pointer.Capture(null);
            if (done.Moved) Commit(done, e.GetPosition(this));
            InvalidateVisual();
            e.Handled = true;
            return;
        }
        if (!_dragging) return;
        _dragging = false;
        _block = null;
        e.Pointer.Capture(null);
        if (_editor.Brush is not null) _editor.PaintBrush();
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
        _gesture = null;
        _adjust = null;
        _column = null;
    }

    private PulseOffice.Document.Embedding.EmbeddedObject? Content(string objectId) =>
        _editor.Document.Objects.Find(objectId)?.Content is PulseOffice.Documents.Objects.EmbeddedContent embedded ? _services.GroupText.Painted(embedded) : null;

    private sealed record ObjectGesture(PulseOffice.Drawing.Shapes.ObjectTransformSession Session, Point Origin, PageSlot Slot, bool Moved = false)
    {
        public PulseOffice.Drawing.Shapes.ObjectHandle Handle => Session.Handle;

        public PulseOffice.Drawing.Shapes.ObjectBox Preview => Session.Preview;

        public double Rotation => Session.Rotation;
    }

    private sealed record ObjectHit(ObjectBox Box, PageSlot Slot);

    private ObjectGesture? _gesture;

    private PageSlot? SlotAt(Point point) =>
        _arrangement.Pages.FirstOrDefault(candidate => candidate.Contains(point.X, point.Y));

    private bool FollowsLink(KeyModifiers modifiers) =>
        (modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta)) && !modifiers.HasFlag(KeyModifiers.Shift);

    private FolioObjectLinkTarget? PointMember(ObjectHit hit, Point point) =>
        _services.ObjectLinks.Point(_editor, hit.Box, (point.X - hit.Slot.X) / Scale, (point.Y - hit.Slot.Y) / Scale);

    private ObjectHit? ObjectAt(Point point)
    {
        var layout = _editor.DisplayLayout;
        if (SlotAt(point) is not { } slot || slot.Index >= layout.Pages.Count) return null;
        return _services.Objects.At(layout.Pages[slot.Index], (point.X - slot.X) / Scale, (point.Y - slot.Y) / Scale) is { } box ? new ObjectHit(box, slot) : null;
    }

    private sealed record AdjustGesture(PulseOffice.UI.Controls.Shapes.AdjustHandleGrip Grip, PulseOffice.UI.Controls.Shapes.AdjustFrame Frame, PageSlot Slot);

    private AdjustGesture? _adjust;

    private (PulseOffice.Drawing.Shapes.ShapeObject Shape, PulseOffice.UI.Controls.Shapes.AdjustFrame Frame, PageSlot Slot)? Adjustable()
    {
        if (_editor.ReadOnly() || _editor.SelectedObjectIds.Count > 1 || _editor.SelectedObjectId is not { } id
            || _editor.Document.Objects.Find(id) is not { Content: PulseOffice.Documents.Objects.EmbeddedContent { Value: PulseOffice.Drawing.Shapes.ShapeObject shape } } item
            || _editor.ObjectLocked(item))
            return null;
        if (_services.Objects.Find(_editor.DisplayLayout, id, out var index) is not { } box || index >= _arrangement.Pages.Count) return null;
        return (shape, new PulseOffice.UI.Controls.Shapes.AdjustFrame(new Rect(box.Bounds.X, box.Bounds.Y, box.Bounds.Width, box.Bounds.Height), item.RotationDegrees, item.FlipHorizontal, item.FlipVertical), _arrangement.Pages[index]);
    }

    private double AdjustSize => (_services.Manipulator.HandleSize / 2 + 1) / Scale;

    private bool BeginAdjust(Point point, int clicks)
    {
        if (clicks > 1 || Adjustable() is not { } adjustable) return false;
        var local = new Point((point.X - adjustable.Slot.X) / Scale, (point.Y - adjustable.Slot.Y) / Scale);
        if (_services.Adjusting.Grip(adjustable.Shape, adjustable.Frame, local, AdjustSize + 1 / Scale) is not { } grip) return false;
        _adjust = new AdjustGesture(grip, adjustable.Frame, adjustable.Slot);
        return true;
    }

    private void PaintAdjustHandles(DrawingContext context, PageBox page)
    {
        if (_gesture is not null || Adjustable() is not { } adjustable || adjustable.Slot.Index != page.Index) return;
        if (_adjust is { } adjusting) _services.Adjusting.DrawPreview(context, adjusting.Grip, adjusting.Frame, AdjustSize, 1 / Scale);
        else _services.Adjusting.Draw(context, adjustable.Shape, adjustable.Frame, AdjustSize);
    }

    private bool BeginObjectGesture(Point point, int clicks)
    {
        if (clicks > 1 || _editor.ReadOnly() || _editor.SelectedObjectId is not { } id || _editor.Document.Objects.Find(id) is not { } item) return false;
        if (_services.Objects.Find(_editor.DisplayLayout, id, out var index) is not { } box || index >= _arrangement.Pages.Count) return false;
        var slot = _arrangement.Pages[index];
        var area = new PulseOffice.Drawing.Shapes.ObjectBox(box.Bounds.X, box.Bounds.Y, box.Bounds.Width, box.Bounds.Height);
        var handle = _services.Frame.HandleAt(new PulseOffice.UI.Controls.Shapes.FrameScene(area, item.RotationDegrees, Brushes.Transparent, 1 / Scale), new Point((point.X - slot.X) / Scale, (point.Y - slot.Y) / Scale));
        if (handle == PulseOffice.Drawing.Shapes.ObjectHandle.None) return false;
        _gesture = new ObjectGesture(_services.Manipulator.Begin(handle, area, item.RotationDegrees), point, slot);
        return true;
    }

    private void Commit(ObjectGesture gesture, Point release)
    {
        if (_editor.SelectedObjectId is not { } id) return;
        switch (gesture.Handle)
        {
            case PulseOffice.Drawing.Shapes.ObjectHandle.Body:
                var target = SlotAt(release) ?? gesture.Slot;
                var shiftX = (gesture.Slot.X - target.X) / Scale;
                var shiftY = (gesture.Slot.Y - target.Y) / Scale;
                _services.Mover.Move(_editor, id, new ObjectDrop(target.Index, gesture.Preview.X + shiftX, gesture.Preview.Y + shiftY,
                    (release.X - target.X) / Scale, (release.Y - target.Y) / Scale));
                break;
            case PulseOffice.Drawing.Shapes.ObjectHandle.Rotate:
                _editor.RotateSelectedObject(gesture.Rotation);
                break;
            default:
                _editor.ResizeSelectedObject(new PulseOffice.Foundation.Units.DocumentSize(
                    PulseOffice.Foundation.Units.Twip.FromPoints(Math.Max(1, gesture.Preview.Width)), PulseOffice.Foundation.Units.Twip.FromPoints(Math.Max(1, gesture.Preview.Height))));
                break;
        }
    }

    private PulseOffice.Drawing.Shapes.ObjectBox Snap(PulseOffice.Drawing.Shapes.ObjectTransformSession session, int page)
    {
        var view = _services.ViewLayout;
        var pages = _editor.DisplayLayout.Pages;
        var step = view.GridSpacing;
        if (!view.SnapToGrid || page >= pages.Count || step <= 0) return session.Preview;
        var sheet = pages[page];
        var content = sheet.Content;
        var left = content.X - Math.Ceiling(content.X / step) * step;
        var top = content.Y - Math.Ceiling(content.Y / step) * step;
        var lines = _services.Snapping.Lattice(left, top, sheet.Width, sheet.Height, step);
        return _services.Snapping.Snap(session, lines, step / 2);
    }

    private void PaintGrids(DrawingContext context, PageBox page)
    {
        var view = _services.ViewLayout;
        var content = page.Content;
        if (view.Grid)
        {
            var dot = new SolidColorBrush(Color.FromArgb(150, 100, 116, 139));
            var size = 1.2 / Scale;
            var dots = new StreamGeometry();
            using (var pen = dots.Open())
            {
                for (var y = content.Y; y <= content.Bottom + 0.01; y += view.GridSpacing)
                    for (var x = content.X; x <= content.Right + 0.01; x += view.GridSpacing)
                    {
                        pen.BeginFigure(new Point(x - size / 2, y - size / 2), true);
                        pen.LineTo(new Point(x + size / 2, y - size / 2));
                        pen.LineTo(new Point(x + size / 2, y + size / 2));
                        pen.LineTo(new Point(x - size / 2, y + size / 2));
                        pen.EndFigure(true);
                    }
            }
            context.DrawGeometry(dot, null, dots);
        }
        if (view.BaselineGrid)
        {
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(90, 37, 99, 235)), 0.5 / Scale);
            var spacing = Math.Max(6, (_editor.Document.Styles.DefaultCharacter.FontSize ?? 12) * 1.17);
            for (var y = content.Y + spacing; y <= content.Bottom; y += spacing)
                context.DrawLine(pen, new Point(content.X, y), new Point(content.Right, y));
        }
        if (view.Helplines && _gesture is { Moved: true } gesture && gesture.Slot.Index == page.Index)
        {
            var pen = new Pen(Accent ?? Brushes.DodgerBlue, 0.6 / Scale, new DashStyle([4, 3], 0));
            var area = gesture.Preview;
            foreach (var x in new[] { area.X, area.X + area.Width })
                context.DrawLine(pen, new Point(x, 0), new Point(x, page.Height));
            foreach (var y in new[] { area.Y, area.Y + area.Height })
                context.DrawLine(pen, new Point(0, y), new Point(page.Width, y));
        }
    }

    private void PaintOtherSelections(DrawingContext context, PageBox page)
    {
        var accent = Accent ?? Brushes.DodgerBlue;
        foreach (var id in _editor.SelectedObjectIds.Skip(1))
            if (_services.Objects.Find(_editor.DisplayLayout, id, out var index) is { } box && index == page.Index)
                _services.Frame.DrawSecondary(context, new PulseOffice.UI.Controls.Shapes.FrameScene(
                    new PulseOffice.Drawing.Shapes.ObjectBox(box.Bounds.X, box.Bounds.Y, box.Bounds.Width, box.Bounds.Height), box.RotationDegrees, accent, 1 / Scale));
    }

    private void PaintObjectSelection(DrawingContext context, PageBox page)
    {
        if (_editor.SelectedObjectId is not { } id || _services.Objects.Find(_editor.DisplayLayout, id, out var index) is not { } box || index != page.Index) return;
        var area = _gesture?.Preview ?? new PulseOffice.Drawing.Shapes.ObjectBox(box.Bounds.X, box.Bounds.Y, box.Bounds.Width, box.Bounds.Height);
        var rotation = _gesture?.Rotation ?? box.RotationDegrees;
        _services.Frame.Draw(context, new PulseOffice.UI.Controls.Shapes.FrameScene(area, rotation, Accent ?? Brushes.DodgerBlue, 1 / Scale, !_editor.ReadOnly()));
    }

    public DocumentPosition? PositionAt(Point point)
    {
        if (_arrangement.Pages.Count == 0) return null;
        var slot = _arrangement.Pages.FirstOrDefault(candidate => candidate.Contains(point.X, point.Y))
            ?? _arrangement.Pages.MinBy(candidate => Distance(candidate, point))!;
        var x = (point.X - slot.X) / Scale;
        var y = Math.Clamp((point.Y - slot.Y) / Scale, slot.CropTop / Scale, (slot.Height - slot.CropBottom) / Scale);
        return _services.Queries.HitTest(_editor.DisplayLayout, slot.Index, x, y);
    }

    private ITurnedFrame? TurnedOwner(PageBox page, LineBox line) =>
        page.TurnedFrames.FirstOrDefault(frame => frame.Lines.Contains(line));

    private DrawingContext.PushedState? Turn(DrawingContext context, PageBox page, LineBox line) =>
        TurnedOwner(page, line) is { } owner
            ? context.PushTransform(new ObjectTransform(owner.RotationDegrees).Around(new Rect(owner.Bounds.X, owner.Bounds.Y, owner.Bounds.Width, owner.Bounds.Height)))
            : null;

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

    private Dictionary<string, (int From, int To)> SelectionMap()
    {
        var map = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        if (_editor.Preedit is not null) return map;
        if (_editor.BlockRanges.Count > 0)
        {
            foreach (var block in _editor.BlockRanges) map[block.Start.ParagraphId] = (block.Start.Offset, block.End.Offset);
            return map;
        }
        if (!_editor.HasSelection) return map;
        var range = _editor.Selection;
        foreach (var paragraph in _services.Order.Between(_editor.Document, range.Start, range.End))
        {
            var from = paragraph.Id == range.Start.ParagraphId ? range.Start.Offset : 0;
            var to = paragraph.Id == range.End.ParagraphId ? range.End.Offset : paragraph.Length + 1;
            map[paragraph.Id] = (from, to);
        }
        return map;
    }

    private void PaintSelection(DrawingContext context, PageBox page, Dictionary<string, (int From, int To)> selection)
    {
        if (selection.Count == 0) return;
        var brush = new SolidColorBrush(Accent is ISolidColorBrush accent ? accent.Color : Colors.DodgerBlue, 0.28);
        foreach (var line in page.AllLines)
        {
            if (!selection.TryGetValue(line.ParagraphId, out var span)) continue;
            var composed = line.Line;
            var from = Math.Max(span.From, composed.Start);
            var to = Math.Min(span.To, composed.End);
            var tail = span.To > composed.End && composed.IsLast;
            if (to <= from && !tail) continue;
            var left = _services.Geometry.XOf(line, from);
            var right = to > from ? _services.Geometry.XOf(line, to) : left;
            if (tail) right += 4;
            using (Turn(context, page, line)) context.FillRectangle(brush, new Rect(left, line.Top, Math.Max(1, right - left), composed.CaretHeight));
        }
    }

    private bool Covers(PulseOffice.Documents.Commenting.CommentAnchor anchor, PulseOffice.Documents.Editing.DocumentPosition caret)
    {
        var order = _editor.Services.Order;
        return order.Contains(_editor.Document, anchor.Start.ParagraphId) && order.Contains(_editor.Document, anchor.End.ParagraphId)
            && order.Compare(_editor.Document, anchor.Start, caret) <= 0 && order.Compare(_editor.Document, caret, anchor.End) <= 0;
    }

    private sealed record ControlRange(int From, int To, bool Active, bool First, string? Title);

    private sealed record ControlRangeKey(DocumentLayout Layout, long Revision, DocumentPosition Caret, bool Shade);

    private (ControlRangeKey Key, Dictionary<string, List<ControlRange>> Ranges)? _controls;

    private Dictionary<string, List<ControlRange>> ControlRanges(DocumentLayout layout, bool shade)
    {
        var key = new ControlRangeKey(layout, _editor.Document.Revision, _editor.Caret, shade);
        if (_controls is { } cached && cached.Key == key) return cached.Ranges;
        var ranges = CollectControlRanges(shade);
        _controls = (key, ranges);
        return ranges;
    }

    private Dictionary<string, List<ControlRange>> CollectControlRanges(bool shade)
    {
        var ranges = new Dictionary<string, List<ControlRange>>(StringComparer.Ordinal);
        var catalog = _editor.Services.Controls.Catalog;
        var spans = catalog.All(_editor.Document);
        if (spans.Count == 0) return ranges;
        var active = _editor.ControlAt(_editor.Caret)?.Id;
        foreach (var span in spans)
        {
            var current = span.Id == active;
            if (!shade && !current) continue;
            foreach (var paragraph in _services.Order.Between(_editor.Document, span.Start, span.End))
            {
                var from = paragraph.Id == span.Start.ParagraphId ? span.Start.Offset : 0;
                var to = paragraph.Id == span.End.ParagraphId ? span.End.Offset : paragraph.Length;
                if (!ranges.TryGetValue(paragraph.Id, out var list)) ranges[paragraph.Id] = list = [];
                list.Add(new ControlRange(from, to, current, paragraph.Id == span.Start.ParagraphId, span.Control.Title));
            }
        }
        return ranges;
    }

    private void PaintControls(DrawingContext context, PageBox page, Dictionary<string, List<ControlRange>> ranges)
    {
        if (ranges.Count == 0) return;
        var accent = Accent is ISolidColorBrush solid ? solid.Color : Colors.DodgerBlue;
        var shade = new SolidColorBrush(accent, 0.08);
        var frame = new Pen(new SolidColorBrush(accent, 0.75), 0.75);
        var titled = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in page.AllLines)
        {
            if (!ranges.TryGetValue(line.ParagraphId, out var list)) continue;
            var composed = line.Line;
            foreach (var range in list)
            {
                var from = Math.Max(range.From, composed.Start);
                var to = Math.Min(range.To, composed.End);
                if (to < from || to == from && range.To > range.From) continue;
                var left = _services.Geometry.XOf(line, from) - 1;
                var right = Math.Max(left + 4, _services.Geometry.XOf(line, to) + 1);
                var box = new Rect(left, line.Top, right - left, composed.CaretHeight);
                context.FillRectangle(shade, box);
                if (!range.Active) continue;
                context.DrawRectangle(null, frame, box, 1.5, 1.5);
                if (!range.First || range.Title is not { Length: > 0 } title || !titled.Add(line.ParagraphId)) continue;
                var label = new FormattedText(title, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface.Default, 7.5, Brushes.White);
                var tab = new Rect(left, line.Top - label.Height - 2, label.Width + 6, label.Height + 2);
                context.FillRectangle(new SolidColorBrush(accent, 0.9), tab, 1.5f);
                context.DrawText(label, new Point(tab.X + 3, tab.Y + 1));
            }
        }
    }

    private void PaintCommentRanges(DrawingContext context, PageBox page, CommentSheet sheet)
    {
        if (sheet.Ranges.Count == 0) return;
        foreach (var line in page.AllLines)
        {
            if (!sheet.Ranges.TryGetValue(line.ParagraphId, out var ranges)) continue;
            var composed = line.Line;
            foreach (var range in ranges)
            {
                var from = Math.Max(range.From, composed.Start);
                var to = Math.Min(range.To, composed.End);
                if (to <= from) continue;
                var left = _services.Geometry.XOf(line, from);
                var right = _services.Geometry.XOf(line, to);
                context.FillRectangle(new SolidColorBrush(Color.FromUInt32(range.Color), range.Resolved ? 0.08 : 0.18), new Rect(left, line.Top, Math.Max(1, right - left), composed.CaretHeight));
            }
        }
    }

    public string? BalloonAt(Point point)
    {
        if (SlotAt(point) is not { } slot || !_balloons.TryGetValue(slot.Index, out var balloons)) return null;
        var local = new Point((point.X - slot.X) / Scale, (point.Y - slot.Y) / Scale);
        return balloons.FirstOrDefault(balloon => balloon.Bounds.Contains(local))?.Anchor.Comment.Id;
    }

    private void PaintWaves(DrawingContext context, PageBox page)
    {
        if (!_services.Proofing.Enabled) return;
        foreach (var group in page.AllLines.GroupBy(line => line.ParagraphId))
        {
            if (_editor.Preedit is not null && group.Key == _editor.PreeditAt.ParagraphId) continue;
            if (_editor.ContentOf(group.Key) is not { } paragraph) continue;
            foreach (var mark in _services.Proofing.Marks(_editor.Document, paragraph))
            {
                foreach (var line in group)
                {
                    var from = Math.Max(mark.Start, line.Line.Start);
                    var to = Math.Min(mark.Start + mark.Length, line.Line.End);
                    if (to <= from) continue;
                    var left = _services.Geometry.XOf(line, from);
                    var right = _services.Geometry.XOf(line, to);
                    using (Turn(context, page, line)) _services.Waves.PaintWave(context, new Rect(left, line.Top, right - left, line.Line.CaretHeight), 0.05, mark.Grammar);
                }
            }
        }
    }
}
