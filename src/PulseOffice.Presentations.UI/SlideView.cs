using PulseOffice.Foundation.Zoom;
using PulseOffice.Document.Tables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.UI.Theming;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public sealed record ElementTransform(SlideElement Element, DocumentRect Bounds, double Rotation);

public sealed record GroupTransformRequest(SlideGroup Group, DocumentRect Bounds, double Rotation);

public sealed record FreeformReady(FreeformPath Path, DocumentRect Bounds, bool Filled);

public sealed class SlideView : PulseOffice.UI.Controls.Theming.ThemedSurface, PulseOffice.UI.Controls.Services.IDocumentSurface
{
    private readonly ISlideRenderer _renderer;
    private readonly IObjectManipulator _manipulator;
    private readonly PulseOffice.UI.Controls.Shapes.IAdjustHandleChrome _adjusting;
    private readonly PulseOffice.UI.Controls.Shapes.IObjectFrameChrome _frame;
    private readonly IBrush _frameAccent = new SolidColorBrush(Color.FromArgb(230, 37, 99, 235));
    private readonly IBrush _secondaryAccent = new SolidColorBrush(Color.FromArgb(150, 37, 99, 235));
    private readonly IBrush _shadow = new SolidColorBrush(Colors.Black, 0.12);
    private readonly IBrush _cellFill = new SolidColorBrush(Color.FromArgb(50, 37, 99, 235));
    private readonly IPen _cellEdge = new Pen(new SolidColorBrush(Color.FromArgb(220, 37, 99, 235)), 1.5);
    private readonly IPen _helpline = new Pen(new SolidColorBrush(Color.FromArgb(150, 37, 99, 235)), 1, new DashStyle([4, 3], 0));
    private readonly IBrush _commentFill = new SolidColorBrush(Color.FromArgb(235, 250, 204, 21));
    private readonly IPen _commentEdge = new Pen(new SolidColorBrush(Color.FromArgb(255, 161, 98, 7)), 1);
    private readonly IBrush _commentInk = new SolidColorBrush(Color.FromArgb(255, 92, 60, 4));
    private readonly IPen _gridLine = new Pen(new SolidColorBrush(Color.FromArgb(40, 120, 130, 150)), 1);
    private readonly IPen _guideLine = new Pen(new SolidColorBrush(Color.FromArgb(160, 220, 60, 90)), 1);
    private readonly IBrush _drawingInk = new SolidColorBrush(Color.FromArgb(230, 37, 99, 235));
    private readonly IPen _drawingLine = new Pen(new SolidColorBrush(Color.FromArgb(230, 37, 99, 235)), 1.5);
    private const double MinimumGridPixels = 4;

    private Rect _target;
    private double _scale = 1;

    private CanvasGesture? _gesture;

    private readonly List<SlideElement> _selection = [];

    private readonly ISlideGroupUnits _units;

    private readonly ISlideGroupEditing _groupEditing;

    public string? OpenSlideGroupId { get; set; }

    private readonly List<ShapePoint> _drawingPoints = [];
    private ShapePoint? _drawingCursor;
    private readonly IPathAuthoring _authoring;
    private readonly IPathNormalizer _normalizer;
    private IDeckDrawingState? _drawing;

    public Func<string, IReadOnlyList<PulseOffice.Text.Spelling.WordSpan>>? Proofing
    {
        get;
        set
        {
            field = value;
            InvalidateVisual();
        }
    }

    public SlideElement? LiveText
    {
        get;
        set
        {
            field = value;
            InvalidateVisual();
        }
    }

    public SlideView(ISlideRenderer renderer, PulseOffice.UI.Controls.Objects.IAnimatedImageProbe? animatedImages = null, IPathAuthoring? authoring = null, IObjectManipulator? manipulator = null, IPathNormalizer? normalizer = null, IPathEditing? pathEditing = null, PulseOffice.UI.Controls.Objects.IBitmapPixels? pixels = null, ISlideGroupUnits? units = null, ISlideGroupEditing? groupEditing = null, PulseOffice.UI.Controls.Shapes.IAdjustHandleChrome? adjusting = null, IObjectSnapping? snapping = null, PulseOffice.UI.Controls.Shapes.IObjectFrameChrome? frame = null)
    {
        _snapping = snapping ?? new ObjectSnapping();
        _adjusting = adjusting ?? new PulseOffice.UI.Controls.Shapes.AdjustHandleChrome();
        _units = units ?? new SlideGroupUnits();
        _groupEditing = groupEditing ?? new SlideGroupEditing(_units);
        _pixels = pixels ?? new PulseOffice.UI.Controls.Objects.BitmapPixels();
        _pathEditing = pathEditing ?? new PathEditing();
        _normalizer = normalizer ?? new PathNormalizer();
        _manipulator = manipulator ?? new ObjectManipulator();
        _frame = frame ?? new PulseOffice.UI.Controls.Shapes.ObjectFrameChrome(_manipulator, new PulseOffice.UI.Controls.Shapes.FrameStyles().Dashed);
        _authoring = authoring ?? new PathAuthoring();
        _animatedImages = animatedImages ?? new PulseOffice.UI.Controls.Objects.AnimatedImageProbe();
        _renderer = renderer;
    }

    public PulseOffice.UI.Controls.Text.Rich.ComposedTextFrame Compose(PulseOffice.UI.Controls.Text.Rich.ITextFrameComposer composer, TextElement element)
    {
        ArgumentNullException.ThrowIfNull(composer);
        ArgumentNullException.ThrowIfNull(element);
        return composer.Compose(element.Text, BoundsOf(element), EffectiveTheme, Fields, _scale);
    }

    public Slide? Slide { get; private set; }

    private SlideSize _size = SlideSize.Widescreen;

    public SlideSize Size
    {
        get => _size;
        set
        {
            if (_size == value) return;
            _size = value;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    public Theme DeckTheme { get; set; } = new() { Name = "Light" };

    public Presentation? Deck { get; set; }

    private SlideMaster EffectiveMaster => Deck is { } deck && Slide is { } slide ? deck.MasterOf(slide) : Master;

    private Theme EffectiveTheme => Deck is { } deck && Slide is { } slide ? deck.ThemeOf(slide) : DeckTheme;

    public SlideMaster Master { get; set; } = new()
    {
        Id = "master1",
        Name = "Light",
        Theme = new Theme { Name = "Light" },
    };

    public double Padding { get; init; } = 12;

    public bool ShowsPrompts { get; init; } = true;

    public bool Editable { get; set; }

    public Func<string, ElementVisual>? PlaybackVisual { get; set; }

    public SlideChrome Chrome { get; set; }

    public TextFields Fields { get; set; }

    public GridSettings? Grid { get; set; }

    public event EventHandler<SlideElement?>? SelectionChanged;

    public event EventHandler<(ObjectElement Element, PulseOffice.Document.Forms.FormControlProperties Changed)>? ControlPressed;

    public bool DesignMode { get; set; } = true;

    public event EventHandler<(ObjectElement Element, int Row, int Column, bool Extend)>? TableCellPicked;

    public (string ElementId, PulseOffice.Document.Tables.TableCellRange Range)? TableSelection
    {
        get;
        set
        {
            field = value;
            InvalidateVisual();
        }
    }

    private readonly PulseOffice.Document.Embedding.TableGeometry _tables = new();

    public bool ShowComments { get; set; } = true;

    public SlideOutputQuality Quality { get; set; } = SlideOutputQuality.Color;

    public PulseOffice.Document.Forms.IFormControlInteraction? Forms { get; set; }

    public event EventHandler<ElementTransform>? ElementChangeRequested;

    public event EventHandler<(IReadOnlyList<string> Ids, int Dx, int Dy)>? ElementsMoveRequested;

    public event EventHandler<GroupTransformRequest>? GroupTransformRequested;

    public event EventHandler<FreeformReady>? FreeformCompleted;

    public event EventHandler<(ObjectElement Element, FreeformPath Path)>? FreeformPathEdited;

    public event EventHandler<(ObjectElement Element, ShapeObject Shape)>? ShapeAdjusted;

    public event EventHandler<(string FromId, ConnectorSite FromSite, string ToId, ConnectorSite ToSite)>? GlueConnectRequested;

    private static readonly ConnectorSite[] GlueSites = [ConnectorSite.Top, ConnectorSite.Right, ConnectorSite.Bottom, ConnectorSite.Left];

    private readonly IPathEditing _pathEditing;

    private readonly PulseOffice.UI.Controls.Shapes.OutlineGeometry _outlines = new();

    public IDeckDrawingState? Drawing
    {
        get => _drawing;
        set
        {
            if (_drawing is not null) _drawing.Changed -= OnDrawingChanged;
            _drawing = value;
            if (_drawing is not null && TopLevel.GetTopLevel(this) is not null) _drawing.Changed += OnDrawingChanged;
        }
    }

    private void OnDrawingChanged(object? sender, EventArgs e)
    {
        _drawingPoints.Clear();
        _drawingCursor = null;
        InvalidateVisual();
    }

    private bool IsDrawing => _drawing is { Pen: not DeckPen.None };

    public void Show(Slide? slide)
    {
        Slide = slide;
        InvalidateVisual();
        UpdateAnimationClock();
    }

    private Avalonia.Threading.DispatcherTimer? _clock;

    public bool PlaysAnimatedImages => _clock is not null;
    private readonly PulseOffice.UI.Controls.Objects.IAnimatedImageProbe _animatedImages;

    private void UpdateAnimationClock()
    {
        var animated = Slide is not null
            && _animatedImages.AnyAnimated(Slide.Elements.OfType<ObjectElement>().Select(element => element.Content));
        if (animated && _clock is null && VisualRoot is not null)
        {
            _clock = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _clock.Tick += (_, _) => InvalidateVisual();
            _clock.Start();
        }
        else if (!animated && _clock is not null)
        {
            _clock.Stop();
            _clock = null;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_drawing is not null)
        {
            _drawing.Changed -= OnDrawingChanged;
            _drawing.Changed += OnDrawingChanged;
        }
        UpdateAnimationClock();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_drawing is not null) _drawing.Changed -= OnDrawingChanged;
        _clock?.Stop();
        _clock = null;
        _filtered?.Dispose();
        _filtered = null;
    }

    private SlideElement? _primary;

    public SlideElement? SelectedElement
    {
        get => _primary;
        set
        {
            _primary = value;
            _selection.Clear();
            if (value is not null) _selection.Add(value);
        }
    }

    public IReadOnlyList<SlideElement> Selection => _selection;

    public void EnsureSelected(SlideElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (_selection.Any(selected => selected.Id == element.Id)) return;

        SelectedElement = element;
        SelectionChanged?.Invoke(this, element);
        InvalidateVisual();
    }

    public void SetSelection(IReadOnlyList<SlideElement> elements, SlideElement? primary)
    {
        _selection.Clear();
        _selection.AddRange(elements);
        _primary = primary ?? _selection.LastOrDefault();
        InvalidateVisual();
    }

    public Rect Target => _target;

    public double Scale => _scale;

    public const double TwipsPerPixel = 15;

    private readonly IObjectSnapping _snapping;

    private ViewZoom _zoom = ViewZoom.Whole;

    public ViewZoom Zoom
    {
        get => _zoom;
        set
        {
            if (_zoom == value) return;
            _zoom = value;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    public double EffectiveZoom => _scale * TwipsPerPixel;

    private double ChromeExtent => 2 * Padding;

    protected override Size MeasureOverride(Size availableSize)
    {
        var aspect = Size.AspectRatio;
        switch (_zoom.Mode)
        {
            case ViewZoomMode.Fixed:
                return new Size(
                    Size.Width.Value / TwipsPerPixel * _zoom.Factor + ChromeExtent,
                    Size.Height.Value / TwipsPerPixel * _zoom.Factor + ChromeExtent);
            case ViewZoomMode.FitWidth when double.IsFinite(availableSize.Width):
                var width = Math.Max(0, availableSize.Width - ChromeExtent);
                return new Size(availableSize.Width, width / aspect + ChromeExtent);
            default:
                return new Size(
                    double.IsFinite(availableSize.Width) ? availableSize.Width : 0,
                    double.IsFinite(availableSize.Height) ? availableSize.Height : 0);
        }
    }

    public SlideElement? ElementAt(Point point)
    {
        if (Slide is null || _scale <= 0) return null;

        foreach (var element in Slide.InZOrder().Where(element => !element.Hidden).Reverse())
            if (BoundsOf(element).Contains(point)) return element;

        return null;
    }

    private bool PressControl(Point point, PointerPressedEventArgs e)
    {
        if (DesignMode || Forms is not { } forms || IsDrawing || e.KeyModifiers.HasFlag(KeyModifiers.Alt)) return false;
        if (ElementAt(point) is not ObjectElement { Content: PulseOffice.Document.Forms.FormControlObject form } element) return false;
        if (!forms.Responds(form.ControlKind)) return false;

        var bounds = BoundsOf(element);
        if (bounds.Width <= 0 || bounds.Height <= 0) return false;

        e.Handled = true;
        var changed = forms.Press(form.ControlKind, form.Properties,
            (point.X - bounds.X) / bounds.Width, (point.Y - bounds.Y) / bounds.Height,
            form.Properties.Items.Count, bounds.Width >= bounds.Height, bounds.Height);
        if (changed is not null && changed != form.Properties) ControlPressed?.Invoke(this, (element, changed));
        return true;
    }

    public Rect BoundsOf(SlideElement element) =>
        new(_target.X + element.Bounds.Left.Value * _scale,
            _target.Y + element.Bounds.Top.Value * _scale,
            element.Bounds.Width.Value * _scale,
            element.Bounds.Height.Value * _scale);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Slide is null || _scale <= 0) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var point = e.GetPosition(this);
        if (PressControl(point, e)) return;
        if (!Editable) return;

        if (IsDrawing)
        {
            BeginDrawing(point, e);
            return;
        }

        if (BeginPointEdit(point, e)) return;
        if (PressGlue(point, e)) return;

        if (AdjustableShape() is { } adjustable
            && _adjusting.Grip(adjustable.Shape, AdjustFrameOf(adjustable.Element), point, _manipulator.HandleSize / 2 + 2) is { } grip)
        {
            _gesture = new AdjustGesture(this, adjustable.Element, grip);
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (Manipulated() is { } frame)
        {
            var handle = _frame.HandleAt(new PulseOffice.UI.Controls.Shapes.FrameScene(frame.Box, frame.Rotation, _frameAccent), point);
            if (handle is not ObjectHandle.None and not ObjectHandle.Body)
            {
                _gesture = new TransformGesture(this, handle, point, frame.Box, frame.Rotation, frame.Group);
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }
        }

        if (Grid is { ShowGuides: true } && GuideAt(point) is { } guideIndex)
        {
            _gesture = new GuideGesture(this, guideIndex);
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        var picked = ElementAt(point);

        if (picked is ObjectElement { Content: PulseOffice.Document.Embedding.DataTableObject table } tableElement
            && SelectedElement?.Id == tableElement.Id)
        {
            var bounds = BoundsOf(tableElement);
            var scale = _scale * PulseOffice.Foundation.Units.Twip.PerPoint;
            if (_tables.CellAt(table, (point.X - bounds.X) / bounds.Width, (point.Y - bounds.Y) / scale) is { } cell)
            {
                TableCellPicked?.Invoke(this, (tableElement, cell.Row, cell.Column, e.KeyModifiers.HasFlag(KeyModifiers.Shift)));
                e.Handled = true;
                return;
            }
        }

        var unit = picked is null ? [] : _units.Unit(Slide, picked, OpenSlideGroupId);

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && picked is not null)
        {
            if (_selection.Any(x => x.Id == picked.Id))
            {
                _selection.RemoveAll(x => unit.Any(member => member.Id == x.Id));
                _primary = _selection.LastOrDefault();
            }
            else
            {
                _selection.AddRange(unit.Where(member => _selection.All(x => x.Id != member.Id)));
                _primary = picked;
            }
            SelectionChanged?.Invoke(this, _primary);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (picked is not null && _selection.Any(x => x.Id == picked.Id))
        {
            _primary = picked;
            SelectionChanged?.Invoke(this, picked);
            InvalidateVisual();
        }
        else if (picked is not null && unit.Count > 1)
        {
            SetSelection(unit, picked);
            SelectionChanged?.Invoke(this, picked);
        }
        else if (!ReferenceEquals(picked, SelectedElement))
        {
            SelectedElement = picked;
            SelectionChanged?.Invoke(this, picked);
            InvalidateVisual();
        }

        if (picked is not null)
        {
            _gesture = SelectedGroupFrame() is { } moving && _selection.Any(x => x.Id == picked.Id)
                ? new TransformGesture(this, ObjectHandle.Body, point, moving.Box, moving.Rotation, moving.Group)
                : new TransformGesture(this, ObjectHandle.Body, point, ToBox(BoundsOf(picked)), picked.RotationDegrees, null);
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (IsDrawing)
        {
            if (_drawingPoints.Count > 0) ContinueDrawing(e.GetPosition(this));
            return;
        }

        _gesture?.Move(e.GetPosition(this), e.KeyModifiers);
    }

    private (List<double> Verticals, List<double> Horizontals) SnapLines(GridSettings grid)
    {
        var verticals = new List<double>();
        var horizontals = new List<double>();

        if (grid.SnapToGrid && grid.Spacing.Value > 0)
        {
            var step = GridStep(grid);
            for (var x = 0.0; x <= Size.Width.Value; x += step)
                verticals.Add(_target.X + x * _scale);
            for (var y = 0.0; y <= Size.Height.Value; y += step)
                horizontals.Add(_target.Y + y * _scale);
        }

        if (grid.SnapToGuides)
        {
            foreach (var guide in grid.Guides)
                if (guide.Horizontal) horizontals.Add(_target.Y + guide.Position * _scale);
                else verticals.Add(_target.X + guide.Position * _scale);

            verticals.Add(_target.X + Size.Width.Value / 2.0 * _scale);
            horizontals.Add(_target.Y + Size.Height.Value / 2.0 * _scale);
        }

        if (grid.SnapToPageMargins)
        {
            verticals.Add(_target.X);
            verticals.Add(_target.Right);
            horizontals.Add(_target.Y);
            horizontals.Add(_target.Bottom);
        }

        if ((grid.SnapToObjectFrames || grid.SnapToObjectPoints) && Slide is { } slide)
        {
            foreach (var element in slide.Elements.Where(element => _selection.All(selected => selected.Id != element.Id)))
            {
                var bounds = BoundsOf(element);
                if (grid.SnapToObjectFrames)
                {
                    verticals.Add(bounds.Left);
                    verticals.Add(bounds.Right);
                    horizontals.Add(bounds.Top);
                    horizontals.Add(bounds.Bottom);
                }
                if (grid.SnapToObjectPoints)
                {
                    verticals.Add(bounds.Center.X);
                    horizontals.Add(bounds.Center.Y);
                }
            }
        }

        return (verticals, horizontals);
    }

    private double GridStep(GridSettings grid) =>
        Math.Max(grid.Spacing.Value, _scale > 0 ? MinimumGridPixels / _scale : grid.Spacing.Value);

    public bool BeginGuide(bool horizontal, IPointer pointer)
    {
        if (!Editable || Grid is not { ShowGuides: true } grid) return false;
        grid.AddGuide(new SlideGuide(horizontal, 0));
        _gesture = new GuideGesture(this, grid.Guides.Count - 1);
        pointer.Capture(this);
        InvalidateVisual();
        return true;
    }

    private int? GuideAt(Point point)
    {
        if (Grid is not { } grid) return null;

        const double tolerance = 5;
        for (var i = 0; i < grid.Guides.Count; i++)
        {
            var guide = grid.Guides[i];
            var hit = guide.Horizontal
                ? Math.Abs(point.Y - (_target.Y + guide.Position * _scale)) <= tolerance
                : Math.Abs(point.X - (_target.X + guide.Position * _scale)) <= tolerance;
            if (hit) return i;
        }

        return null;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (IsDrawing)
        {
            if (_drawing!.Pen == DeckPen.Freehand && _drawingPoints.Count > 0) CompleteDrawing(closed: false);
            e.Pointer.Capture(null);
            return;
        }

        if (_gesture is not { } gesture) return;

        _gesture = null;
        gesture.Release();
        e.Pointer.Capture(null);
        InvalidateVisual();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (IsDrawing && _drawing!.Pen == DeckPen.Freehand && _drawingPoints.Count > 0)
        {
            CompleteDrawing(closed: false);
            return;
        }
        if (_gesture is null) return;
        _gesture = null;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var available = new Rect(Bounds.Size).Deflate(Padding);
        if (available.Width <= 0 || available.Height <= 0 || Slide is null) return;

        _target = TargetFor(available);
        _scale = Size.Width.Value == 0 ? 1 : _target.Width / Size.Width.Value;

        context.DrawRectangle(_shadow, null, _target.Translate(new Vector(0, 3)), 4, 4);

        if (Quality == SlideOutputQuality.Color) _renderer.Render(context, Slide, Size, _target, EffectiveTheme, EffectiveMaster, Palette(), PlaybackVisual, Chrome, Fields);
        else RenderFiltered(context);

        if (Editable && Grid is { } grid)
        {
            DrawGridAndGuides(context, grid, behind: false);
        }

        if (SelectedGroupFrame() is { } groupFrame) DrawFrame(context, groupFrame.Box, groupFrame.Rotation, groupFrame.Group);
        else
        {
            if (_selection.Count > 1) DrawSecondarySelection(context);

            if (SelectedElement is { } selected && Slide.Find(selected.Id) is not null)
                DrawSelection(context, LiveText is { } live && live.Id == selected.Id ? live : selected);
            if (Editable) DrawAdjustHandles(context);
        }

        if (Editable && TableSelection is { } cells && Slide.Find(cells.ElementId) is ObjectElement { Content: PulseOffice.Document.Embedding.DataTableObject selectedTable } cellOwner)
        {
            var bounds = BoundsOf(cellOwner);
            var area = _tables.Area(selectedTable, cells.Range);
            var scale = _scale * PulseOffice.Foundation.Units.Twip.PerPoint;
            var rect = new Rect(bounds.X + area.X * bounds.Width, bounds.Y + area.Y * scale, area.Width * bounds.Width, area.Height * scale);
            context.FillRectangle(_cellFill, rect);
            context.DrawRectangle(_cellEdge, rect);
        }

        if (Editable && Grid is { HelplinesWhileMoving: true } && _gesture is TransformGesture { Preview: { } moving })
            DrawHelplines(context, moving.Normalized());

        if (IsDrawing) DrawDrawingOverlay(context);

        if (Editable) DrawPathNodes(context);
        if (Editable) DrawGluePoints(context);

        if (Editable && ShowComments && Slide.Comments.Count > 0) DrawComments(context);
    }

    private void RenderFiltered(DrawingContext context)
    {
        if (Slide is null) return;
        var width = Math.Max(1, (int)Math.Ceiling(_target.Width));
        var height = Math.Max(1, (int)Math.Ceiling(_target.Height));
        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(width, height));
        using (var inner = bitmap.CreateDrawingContext())
            _renderer.Render(inner, Slide, Size, new Rect(0, 0, width, height), EffectiveTheme, EffectiveMaster,
                Palette() with { Underlay = null }, PlaybackVisual, Chrome, Fields);

        PulseOffice.Drawing.Assets.Filters.IPixelFilter filter = Quality == SlideOutputQuality.Grayscale
            ? new PulseOffice.Drawing.Assets.Filters.GrayscaleFilter()
            : new PulseOffice.Drawing.Assets.Filters.ThresholdFilter();
        var previous = _filtered;
        _filtered = _pixels.Edit(bitmap, filter.Apply);
        context.DrawImage(_filtered, _target);
        previous?.Dispose();
    }

    private Avalonia.Media.Imaging.WriteableBitmap? _filtered;

    private readonly PulseOffice.UI.Controls.Objects.IBitmapPixels _pixels;

    private void DrawHelplines(DrawingContext context, ObjectBox box)
    {
        using var clip = context.PushClip(_target);
        context.DrawLine(_helpline, new Point(box.X, _target.Y), new Point(box.X, _target.Bottom));
        context.DrawLine(_helpline, new Point(box.Right, _target.Y), new Point(box.Right, _target.Bottom));
        context.DrawLine(_helpline, new Point(_target.X, box.Y), new Point(_target.Right, box.Y));
        context.DrawLine(_helpline, new Point(_target.X, box.Bottom), new Point(_target.Right, box.Bottom));
    }

    private void DrawComments(DrawingContext context)
    {
        if (Slide is null) return;

        foreach (var comment in Slide.Comments)
        {
            var x = _target.X + comment.X.Value * _scale;
            var y = _target.Y + comment.Y.Value * _scale;
            var rect = new Rect(x, y, 18, 18);
            context.DrawRectangle(_commentFill, _commentEdge, rect, 4, 4);

            var initial = string.IsNullOrEmpty(comment.Author) ? "?" : comment.Author[..1].ToUpperInvariant();
            var text = new FormattedText(initial, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, new Typeface(FontFamily.Default, weight: FontWeight.Bold), 11, _commentInk);
            context.DrawText(text, new Point(x + (18 - text.Width) / 2, y + (18 - text.Height) / 2));
        }
    }

    private SlideGroup? SelectedGroup => _selection.Count > 1 && Slide is { } slide ? _units.Matching(slide, _selection) : null;

    private sealed record ManipulatedFrame(ObjectBox Box, double Rotation, SlideGroup? Group);

    private ManipulatedFrame? SelectedGroupFrame()
    {
        if (Slide is not { } slide || SelectedGroup is not { } group || _groupEditing.Frame(slide, group) is not { } frame) return null;
        var box = BoundsOf(frame.Bounds);
        return new ManipulatedFrame(ToBox(box), frame.RotationDegrees, group);
    }

    private ManipulatedFrame? Manipulated()
    {
        if (SelectedGroupFrame() is { } group) return group;
        if (SelectedGroup is null && SelectedElement is { } current && Slide?.Find(current.Id) is not null)
            return new ManipulatedFrame(ToBox(BoundsOf(current)), current.RotationDegrees, null);
        return null;
    }

    private Rect BoundsOf(DocumentRect bounds) =>
        new(_target.X + bounds.Left.Value * _scale, _target.Y + bounds.Top.Value * _scale, bounds.Width.Value * _scale, bounds.Height.Value * _scale);

    private void DrawSecondarySelection(DrawingContext context)
    {

        var (dx, dy) = _gesture is TransformGesture { Handle: ObjectHandle.Body, Preview: { } preview } moving
            ? (preview.X - moving.Start.X, preview.Y - moving.Start.Y)
            : (0.0, 0.0);

        foreach (var element in _selection)
        {
            if (_primary is not null && element.Id == _primary.Id) continue;
            if (Slide?.Find(element.Id) is null) continue;

            var box = BoundsOf(element).Translate(new Vector(dx, dy));
            _frame.DrawSecondary(context, new PulseOffice.UI.Controls.Shapes.FrameScene(ToBox(box), element.RotationDegrees, _secondaryAccent));
        }
    }

    private void DrawGridAndGuides(DrawingContext context, GridSettings grid, bool behind)
    {
        using var clip = context.PushClip(_target);

        if (grid.ShowGrid && grid.Spacing.Value > 0 && grid.GridInFront != behind)
        {
            var step = GridStep(grid);
            for (var x = step; x < Size.Width.Value; x += step)
            {
                var px = _target.X + x * _scale;
                context.DrawLine(_gridLine, new Point(px, _target.Y), new Point(px, _target.Bottom));
            }
            for (var y = step; y < Size.Height.Value; y += step)
            {
                var py = _target.Y + y * _scale;
                context.DrawLine(_gridLine, new Point(_target.X, py), new Point(_target.Right, py));
            }
        }

        if (grid.ShowGuides && grid.GuidesInFront != behind)
        {
            var pen = _guideLine;
            foreach (var guide in grid.Guides)
            {
                if (guide.Horizontal)
                {
                    var py = _target.Y + guide.Position * _scale;
                    context.DrawLine(pen, new Point(_target.X, py), new Point(_target.Right, py));
                }
                else
                {
                    var px = _target.X + guide.Position * _scale;
                    context.DrawLine(pen, new Point(px, _target.Y), new Point(px, _target.Bottom));
                }
            }
        }
    }

    private void DrawSelection(DrawingContext context, SlideElement selected) =>
        DrawFrame(context, ToBox(BoundsOf(selected)), selected.RotationDegrees, null);

    private void DrawFrame(DrawingContext context, ObjectBox resting, double restingRotation, SlideGroup? group)
    {
        var drag = _gesture as TransformGesture;
        if (drag is not null && !ReferenceEquals(drag.Group, group) && drag.Group?.Id != group?.Id) drag = null;
        var box = drag?.Preview is { } preview ? preview.Normalized() : resting;
        var rotation = drag is null ? restingRotation : drag.RotationPreview ?? drag.RotationStart;

        _frame.Draw(context, new PulseOffice.UI.Controls.Shapes.FrameScene(box, rotation, _frameAccent, 1, Editable));
    }

    private (ObjectElement Element, ShapeObject Shape)? AdjustableShape() =>
        Editable && SelectedGroup is null && SelectedElement is ObjectElement { Content: ShapeObject shape } element && Slide?.Find(element.Id) is not null
            ? (element, shape)
            : null;

    private PulseOffice.UI.Controls.Shapes.AdjustFrame AdjustFrameOf(ObjectElement element) => new(BoundsOf(element), element.RotationDegrees);

    private void DrawAdjustHandles(DrawingContext context)
    {
        var size = _manipulator.HandleSize / 2 + 1;
        if (_gesture is AdjustGesture adjusting)
        {
            _adjusting.DrawPreview(context, adjusting.Grip, AdjustFrameOf(adjusting.Element), size, 1.2);
            return;
        }
        if (_gesture is TransformGesture || AdjustableShape() is not { } adjustable) return;
        _adjusting.Draw(context, adjustable.Shape, AdjustFrameOf(adjustable.Element), size);
    }

    private void BeginDrawing(Point point, PointerPressedEventArgs e)
    {
        if (_drawing is not { } tools || tools.Pen == DeckPen.None) return;
        e.Handled = true;

        var candidate = new ShapePoint(point.X, point.Y);
        if (tools.Pen == DeckPen.PolygonConstrained && _drawingPoints.Count > 0)
            candidate = _authoring.Constrain(_drawingPoints[^1], candidate);

        if (tools.Pen == DeckPen.Freehand)
        {
            _drawingPoints.Clear();
            _drawingPoints.Add(candidate);
            e.Pointer.Capture(this);
            return;
        }

        if (e.ClickCount > 1 || (_drawingPoints.Count > 1 && Near(candidate, _drawingPoints[0])))
        {
            CompleteDrawing(closed: _drawingPoints.Count > 2 && Near(candidate, _drawingPoints[0]));
            return;
        }

        _drawingPoints.Add(candidate);
        InvalidateVisual();
    }

    private void ContinueDrawing(Point point)
    {
        if (_drawing is not { } tools) return;
        var candidate = new ShapePoint(point.X, point.Y);

        if (tools.Pen == DeckPen.Freehand)
        {
            if (_drawingPoints.Count == 0) return;
            _drawingPoints.Add(candidate);
            InvalidateVisual();
            return;
        }

        _drawingCursor = tools.Pen == DeckPen.PolygonConstrained && _drawingPoints.Count > 0
            ? _authoring.Constrain(_drawingPoints[^1], candidate)
            : candidate;
        InvalidateVisual();
    }

    private void CompleteDrawing(bool closed)
    {
        if (_drawing is not { } tools) return;

        var points = _drawingPoints.ToList();
        _drawingPoints.Clear();
        _drawingCursor = null;

        if (points.Count < 2) { tools.Finish(); InvalidateVisual(); return; }

        var path = tools.Pen switch
        {
            DeckPen.Freehand => _authoring.Freehand(points, 2.0, closed, tools.Filled),
            DeckPen.Curve => _authoring.Curve(points, closed, tools.Filled),
            _ => _authoring.Polygon(points, closed, tools.Filled),
        };

        if (path.Figures.Count > 0 && _scale > 0)
        {
            var box = _normalizer.ToUnitBox(path).Bounds;
            var bounds = new DocumentRect(
                new Twip((int)Math.Round((box.X - _target.X) / _scale)),
                new Twip((int)Math.Round((box.Y - _target.Y) / _scale)),
                new Twip((int)Math.Round(box.Width / _scale)),
                new Twip((int)Math.Round(box.Height / _scale)));
            FreeformCompleted?.Invoke(this, new FreeformReady(_normalizer.ToUnitBox(path).Path, bounds, tools.Filled));
        }

        tools.Finish();
        InvalidateVisual();
    }

    private bool Near(ShapePoint a, ShapePoint b) =>
        Math.Abs(a.X - b.X) <= 6 && Math.Abs(a.Y - b.Y) <= 6;

    private void DrawDrawingOverlay(DrawingContext context)
    {
        if (_drawingPoints.Count == 0) return;

        var all = new List<ShapePoint>(_drawingPoints);
        if (_drawingCursor is { } cursor) all.Add(cursor);
        for (var i = 1; i < all.Count; i++)
            context.DrawLine(_drawingLine, new Point(all[i - 1].X, all[i - 1].Y), new Point(all[i].X, all[i].Y));

        foreach (var point in _drawingPoints)
            context.DrawEllipse(_drawingInk, null, new Point(point.X, point.Y), 2.5, 2.5);
    }

    private ObjectBox ToBox(Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    private DocumentRect ToRect(ObjectBox box) => new(
        new Twip((int)Math.Round((box.X - _target.X) / _scale)),
        new Twip((int)Math.Round((box.Y - _target.Y) / _scale)),
        new Twip((int)Math.Round(box.Width / _scale)),
        new Twip((int)Math.Round(box.Height / _scale)));

    private SlidePalette Palette()
    {
        Color Resolve(string key, Color fallback) =>
            (this.FindResource(key) as ISolidColorBrush)?.Color ?? fallback;

        return new SlidePalette(
            ShowsPrompts ? Resolve(PulseOffice.UI.Theming.Palette.Keys.TextDisabled, Color.FromRgb(160, 168, 180)) : Colors.Transparent,
            Resolve(PulseOffice.UI.Theming.Palette.Keys.Border, Color.FromRgb(210, 214, 220)),
            Proofing,
            Editable && Grid is { } grid && (!grid.GridInFront || !grid.GuidesInFront)
                ? context => DrawGridAndGuides(context, grid, behind: true)
                : null,
            Quality,
            LiveText);
    }

    private Rect TargetFor(Rect available)
    {
        var aspect = Size.AspectRatio;
        if (_zoom.Mode == ViewZoomMode.FitWhole) return FitLetterboxed(available, aspect);

        var width = _zoom.Mode == ViewZoomMode.Fixed
            ? Size.Width.Value / TwipsPerPixel * _zoom.Factor
            : available.Width;
        var height = width / aspect;
        var x = available.X + Math.Max(0, (available.Width - width) / 2);
        var y = available.Y + Math.Max(0, (available.Height - height) / 2);
        return new Rect(x, y, width, height);
    }

    private Rect FitLetterboxed(Rect available, double aspect)
    {
        var width = available.Width;
        var height = width / aspect;

        if (height > available.Height)
        {
            height = available.Height;
            width = height * aspect;
        }

        var x = available.X + (available.Width - width) / 2;
        var y = available.Y + (available.Height - height) / 2;

        return new Rect(x, y, width, height);
    }

    private bool BeginPointEdit(Point point, PointerPressedEventArgs e)
    {
        if (_drawing is not { EditingPoints: true } drawing) return false;
        if (SelectedElement is not ObjectElement { Content: FreeformObject freeform } element) return false;

        var bounds = BoundsOf(element);
        if (bounds.Width <= 0 || bounds.Height <= 0) return false;

        var handle = _pathEditing.HandleAt(freeform.Path, LocalPoint(bounds, point), 8 / Math.Max(bounds.Width, bounds.Height));
        drawing.Handle = handle;
        InvalidateVisual();
        if (!handle.Exists) return false;

        _gesture = new PointGesture(this, element, freeform.Path, handle);
        e.Pointer.Capture(this);
        e.Handled = true;
        return true;
    }

    private ShapePoint LocalPoint(Rect bounds, Point point) =>
        new((point.X - bounds.X) / bounds.Width, (point.Y - bounds.Y) / bounds.Height);

    private void DrawPathNodes(DrawingContext context)
    {
        if (_drawing is not { EditingPoints: true } drawing) return;
        if (SelectedElement is not ObjectElement { Content: FreeformObject freeform } element) return;

        var path = _gesture is PointGesture { Preview: { } preview } ? preview : freeform.Path;
        var bounds = BoundsOf(element);
        var accent = this.FindResource("PulseAccent") as IBrush ?? Brushes.SteelBlue;
        var handlePen = new Pen(accent, 1);
        var nodePen = new Pen(accent, 1.5);

        Point Map(ShapePoint point) => new(bounds.X + point.X * bounds.Width, bounds.Y + point.Y * bounds.Height);

        if (_gesture is PointGesture) context.DrawGeometry(null, new Pen(accent, 1) { DashStyle = DashStyle.Dash },
            _outlines.Build(path.ToOutline(), Map, filled: false));

        for (var f = 0; f < path.Figures.Count; f++)
            for (var n = 0; n < path.Figures[f].Nodes.Count; n++)
            {
                var node = path.Figures[f].Nodes[n];
                var centre = Map(node.Point);
                var chosen = drawing.Handle.Figure == f && drawing.Handle.Node == n;

                if (node.Before is { } before)
                {
                    context.DrawLine(handlePen, centre, Map(before));
                    context.DrawEllipse(accent, null, Map(before), 3, 3);
                }

                if (node.After is { } after)
                {
                    context.DrawLine(handlePen, centre, Map(after));
                    context.DrawEllipse(accent, null, Map(after), 3, 3);
                }

                var fill = chosen ? accent : Brushes.White;
                if (node.Kind == PathNodeKind.Corner) context.DrawRectangle(fill, nodePen, new Rect(centre.X - 4, centre.Y - 4, 8, 8));
                else context.DrawEllipse(fill, nodePen, centre, 4.5, 4.5);
            }
    }

    private IEnumerable<(ObjectElement Element, ConnectorSite Site, Point At)> GlueTargets()
    {
        if (Slide is null) yield break;
        foreach (var element in Slide.Elements.OfType<ObjectElement>())
        {
            if (element.Content is ConnectorObject) continue;
            var b = BoundsOf(element);
            foreach (var site in GlueSites)
                yield return (element, site, site switch
                {
                    ConnectorSite.Top => new Point(b.Center.X, b.Top),
                    ConnectorSite.Right => new Point(b.Right, b.Center.Y),
                    ConnectorSite.Bottom => new Point(b.Center.X, b.Bottom),
                    _ => new Point(b.Left, b.Center.Y),
                });
        }
    }

    private bool PressGlue(Point point, PointerPressedEventArgs e)
    {
        if (_drawing is not { GluePoints: true } drawing) return false;
        e.Handled = true;

        var hit = GlueTargets().Where(t => Point.Distance(t.At, point) <= 8)
            .OrderBy(t => Point.Distance(t.At, point)).Select(t => ((ObjectElement, ConnectorSite)?)(t.Element, t.Site)).FirstOrDefault();

        if (hit is not { } found)
        {
            drawing.GlueStart = null;
        }
        else if (drawing.GlueStart is { } start && start.ObjectId != found.Item1.Id)
        {
            drawing.GlueStart = null;
            GlueConnectRequested?.Invoke(this, (start.ObjectId, start.Site, found.Item1.Id, found.Item2));
        }
        else
        {
            drawing.GlueStart = (found.Item1.Id, found.Item2);
        }

        InvalidateVisual();
        return true;
    }

    private void DrawGluePoints(DrawingContext context)
    {
        if (_drawing is not { GluePoints: true } drawing) return;
        var accent = this.FindResource("PulseAccent") as IBrush ?? Brushes.SteelBlue;
        var pen = new Pen(accent, 1.5);

        foreach (var (element, site, at) in GlueTargets())
        {
            var chosen = drawing.GlueStart is { } start && start.ObjectId == element.Id && start.Site == site;
            context.DrawEllipse(chosen ? accent : Brushes.White, pen, at, 4.5, 4.5);
            context.DrawLine(pen, new Point(at.X - 2.5, at.Y - 2.5), new Point(at.X + 2.5, at.Y + 2.5));
            context.DrawLine(pen, new Point(at.X - 2.5, at.Y + 2.5), new Point(at.X + 2.5, at.Y - 2.5));
        }
    }

    private sealed class PointGesture(SlideView view, ObjectElement element, FreeformPath start, PathHandle handle) : CanvasGesture(view)
    {
        public FreeformPath? Preview { get; private set; }

        public override void Move(Point point, KeyModifiers modifiers)
        {
            var bounds = View.BoundsOf(element);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            Preview = View._pathEditing.Move(start, handle, View.LocalPoint(bounds, point));
            View.InvalidateVisual();
        }

        public override void Release()
        {
            if (Preview is { } path) View.FreeformPathEdited?.Invoke(View, (element, path));
        }
    }

    private sealed class AdjustGesture(SlideView view, ObjectElement element, PulseOffice.UI.Controls.Shapes.AdjustHandleGrip grip) : CanvasGesture(view)
    {
        public ObjectElement Element => element;

        public PulseOffice.UI.Controls.Shapes.AdjustHandleGrip Grip { get; private set; } = grip;

        public override void Move(Point point, KeyModifiers modifiers)
        {
            Grip = View._adjusting.Drag(Grip, View.AdjustFrameOf(element), point);
            View.InvalidateVisual();
        }

        public override void Release()
        {
            if (Grip.Changed) View.ShapeAdjusted?.Invoke(View, (element, Grip.Preview));
        }
    }

    private abstract class CanvasGesture(SlideView view)
    {
        protected SlideView View => view;

        public abstract void Move(Point point, KeyModifiers modifiers);

        public abstract void Release();
    }

    private sealed class GuideGesture(SlideView view, int index) : CanvasGesture(view)
    {
        public override void Move(Point point, KeyModifiers modifiers)
        {
            if (View.Grid is not { } grid || index >= grid.Guides.Count) return;

            var guide = grid.Guides[index];
            var position = guide.Horizontal
                ? (int)Math.Round((point.Y - View._target.Y) / View._scale)
                : (int)Math.Round((point.X - View._target.X) / View._scale);

            grid.MoveGuide(index, Math.Clamp(position, 0, guide.Horizontal ? View.Size.Height.Value : View.Size.Width.Value));
            View.InvalidateVisual();
        }

        public override void Release()
        {
        }
    }

    private sealed class TransformGesture(SlideView view, ObjectHandle handle, Point origin, ObjectBox start, double rotation, SlideGroup? group)
        : CanvasGesture(view)
    {
        private const double Tolerance = 8;

        private readonly ObjectTransformSession _session = view._manipulator.Begin(handle, start, rotation);

        public SlideGroup? Group => group;

        public ObjectHandle Handle => _session.Handle;

        public ObjectBox Start => _session.Start;

        public double RotationStart => _session.StartRotation;

        public ObjectBox Preview => _session.Preview;

        public double? RotationPreview => _session.RotationPreview;

        public override void Move(Point point, KeyModifiers modifiers)
        {
            _session.Track(point.X - origin.X, point.Y - origin.Y, point.X, point.Y, modifiers.HasFlag(KeyModifiers.Shift));

            if (!modifiers.HasFlag(KeyModifiers.Alt)) Snap();
            View.InvalidateVisual();
        }

        public override void Release()
        {
            if (View.SelectedElement is not { } selected) return;

            _session.Settle(_session.Preview.Normalized());
            var box = _session.Preview;
            var angle = _session.Rotation;
            var moved = _session.Moved(0.5);
            var rotated = _session.Rotated(0.01);

            if (_session.Moving && View._selection.Count > 1 && moved)
            {
                var dx = (int)Math.Round((box.X - Start.X) / View._scale);
                var dy = (int)Math.Round((box.Y - Start.Y) / View._scale);
                View.ElementsMoveRequested?.Invoke(View, ([.. View._selection.Select(s => s.Id)], dx, dy));
            }
            else if (group is not null && (moved || rotated))
            {
                View.GroupTransformRequested?.Invoke(View, new GroupTransformRequest(group, View.ToRect(box), angle));
            }
            else if (moved || rotated)
            {
                View.ElementChangeRequested?.Invoke(View, new ElementTransform(selected, View.ToRect(box), angle));
            }
        }

        private void Snap()
        {
            if (View.Grid is not { } grid) return;
            var (verticals, horizontals) = View.SnapLines(grid);
            _session.Settle(View._snapping.Snap(_session, new SnapLines(verticals, horizontals), Tolerance));
        }
    }
}
