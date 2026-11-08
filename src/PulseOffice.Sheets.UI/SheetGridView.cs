using PulseOffice.Drawing.Shapes;
using PulseOffice.Sheets.Engine.Operations;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Charts;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Formatting;
using PulseOffice.UI.Controls.Charts;
using PulseOffice.Foundation.Units;
using PulseOffice.Drawing.Freeform;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.Document.Forms;
using PulseOffice.UI.Theming;
using PulseOffice.UI.Controls.Drawing;
using PulseOffice.Sheets.UI.GridParts;

namespace PulseOffice.Sheets.UI;

public sealed record FormControlInteractionEventArgs(PlacedObject Placed, FormControlProperties Properties);

public sealed class SheetGridView : PulseOffice.UI.Controls.Theming.ThemedSurface, PulseOffice.UI.Controls.Services.IDocumentSurface, IGridSurface
{
    public const double MinimumZoom = 0.25;
    public const double MaximumZoom = 4.0;

    private const double AccessibleWidth = 255;

    private readonly CalculationEngine _engine;
    private readonly ICellPresenter _presenter;
    private readonly SheetSelection _selection;
    private readonly IGridTheme _theme;
    private readonly SheetGridParts _parts;
    private readonly PulseOffice.UI.Controls.Shortcuts.TypingChordGuard _chords = new();

    public SheetGridView(
        CalculationEngine engine, ICellPresenter presenter,
        ConditionalFormattingEvaluator conditional, SheetSelection selection, int sheetIndex = 0,
        IFormControlBinding? formBinding = null,
        IFormControlInteraction? formInteraction = null,
        IFormControlRenderer? formRenderer = null,
        PulseOffice.Formulas.IFormulaParser? formulaParser = null,
        PulseOffice.UI.Controls.Formulas.IFormulaRenderer? formulaRenderer = null,
        IObjectManipulator? manipulator = null,
        IPathNormalizer? normalizer = null,
        IDrawingObjectRenderer? objectRenderer = null,
        IObjectOrdering? ordering = null,
        PulseOffice.UI.Controls.Objects.IPackagedObjectRenderer? packagedRenderer = null,
        IPathEditing? pathEditing = null,
        ISignatureLineRenderer? signatureRenderer = null,
        Engine.Operations.ISparklineData? sparklines = null,
        IAdjustHandleChrome? adjusting = null,
        IObjectFrameChrome? frame = null,
        IGroupChildLinks? groupLinks = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _chords.Attach(this);
        _theme = new ResourceGridTheme(this);

        var handles = manipulator ?? new ObjectManipulator();
        var dependencies = new SheetGridDependencies(
            _engine, _presenter, conditional ?? throw new ArgumentNullException(nameof(conditional)), _selection, sheetIndex,
            formBinding ?? new FormControlBinding(new PulseOffice.Sheets.Formatting.SystemSpreadsheetLocale()),
            formInteraction ?? new FormControlInteraction(),
            formRenderer ?? new FormControlRenderer(),
            formulaParser ?? new PulseOffice.Formulas.FormulaParser(),
            formulaRenderer ?? new PulseOffice.UI.Controls.Formulas.FormulaRenderer(),
            handles,
            normalizer ?? new PathNormalizer(),
            objectRenderer ?? new DrawingObjectRenderer(),
            ordering ?? new ObjectOrdering(),
            packagedRenderer ?? new PulseOffice.UI.Controls.Objects.PackagedObjectRenderer(),
            pathEditing ?? new PathEditing(),
            signatureRenderer ?? new SignatureLineRenderer(),
            sparklines ?? new Engine.Operations.SparklineData(),
            adjusting ?? new AdjustHandleChrome(),
            frame ?? new ObjectFrameChrome(handles, new FrameStyles().Filled),
            groupLinks ?? new GroupChildLinks());

        _parts = new SheetGridPartsFactory().Create(dependencies, this, this, _theme, () => Bounds.Size, _chords);

        Focusable = true;
        ClipToBounds = true;

        _selection.Changed += (_, _) => InvalidateVisual();
    }

    private GridViewport Viewport => _parts.Viewport;

    private GridDisplayOptions Options => _parts.Options;

    private GridNotifications Events => _parts.Notifications;

    public IChartDataExtractor ChartData { get => _parts.Services.ChartData; init => _parts.Services.ChartData = value; }

    public ChartRenderer ChartRenderer { get => _parts.Services.ChartRenderer; init => _parts.Services.ChartRenderer = value; }

    public ICellOverflow Overflow { get => _parts.Services.Overflow; init => _parts.Services.Overflow = value; }

    public ICellTextPlacement TextPlacement { get => _parts.Services.TextPlacement; init => _parts.Services.TextPlacement = value; }

    public ISparklinePainter SparklinePainter { get => _parts.Services.SparklinePainter; init => _parts.Services.SparklinePainter = value; }

    public ICellTextComposer TextComposer { get => _parts.Services.TextComposer; init => _parts.Services.TextComposer = value; }

    public ICellNavigator Navigator { get => _parts.Services.Navigator; init => _parts.Services.Navigator = value; }

    public string PresentedText(CellAddress address)
    {
        var sheet = _engine.Workbook.Sheets[Viewport.SheetIndex];
        var cell = sheet[address] with { StyleId = sheet.StyleIdAt(address) };
        if (cell.Value.IsEmpty && !cell.HasFormula) return string.Empty;
        return _presenter.Present(cell, _engine.Workbook.Styles, _engine.Workbook.NumberFormats, AccessibleWidth).Text;
    }

    public double Zoom
    {
        get => Viewport.Zoom;
        set
        {
            var clamped = Math.Clamp(value, MinimumZoom, MaximumZoom);
            if (Math.Abs(clamped - Viewport.Zoom) < 0.001) return;

            Viewport.Zoom = clamped;
            ZoomChanged?.Invoke(this, clamped);
            InvalidateVisual();
        }
    }

    public event EventHandler<double>? ZoomChanged;

    public GridGeometry Geometry => Viewport.Geometry;

    public Rect ActiveCellBounds() => Viewport.ToScreen(Viewport.MergedRect(_selection.Active));

    public int SheetIndex
    {
        get => Viewport.SheetIndex;
        set
        {
            Viewport.SheetIndex = value;
            Viewport.ApplyFrozenPanes();
            InvalidateVisual();
        }
    }

    public bool ShowHeaders
    {
        get => Viewport.ShowHeaders;
        set { Viewport.ShowHeaders = value; InvalidateVisual(); }
    }

    public bool ShowFormulas
    {
        get => Options.ShowFormulas;
        set { Options.ShowFormulas = value; InvalidateVisual(); }
    }

    public bool ShowGridLines
    {
        get => Options.ShowGridLines;
        set { Options.ShowGridLines = value; InvalidateVisual(); }
    }

    public bool HighlightValues
    {
        get => Options.HighlightValues;
        set { Options.HighlightValues = value; InvalidateVisual(); }
    }

    public bool HighlightRowAndColumn
    {
        get => Options.HighlightRowAndColumn;
        set { Options.HighlightRowAndColumn = value; InvalidateVisual(); }
    }

    public bool ShowHiddenMarkers
    {
        get => Options.ShowHiddenMarkers;
        set { Options.ShowHiddenMarkers = value; InvalidateVisual(); }
    }

    public bool ShowPageBreaks
    {
        get => Options.ShowPageBreaks;
        set { Options.ShowPageBreaks = value; InvalidateVisual(); }
    }

    public bool SnapToGrid { get; set; }

    public ISheetSpelling? Spelling
    {
        get => Options.Spelling;
        set
        {
            if (ReferenceEquals(Options.Spelling, value)) return;

            if (Options.Spelling is { } previous) previous.Changed -= SpellingChanged;

            Options.Spelling = value;

            if (value is not null) value.Changed += SpellingChanged;
        }
    }

    private void SpellingChanged(object? sender, EventArgs e) => InvalidateVisual();

    public Func<CellAddress, bool> ChoicesAvailable
    {
        get => Options.ChoicesAvailable;
        set => Options.ChoicesAvailable = value;
    }

    public Rect ChoiceButtonBounds() => _parts.Overlay.ChoiceButtonBounds();

    public bool PickingReference
    {
        get => Options.PickingReference;
        set
        {
            if (Options.PickingReference == value) return;
            Options.PickingReference = value;
            _parts.Pointer.AbandonForReferencePick();
            InvalidateVisual();
        }
    }

    public bool DesignMode
    {
        get => Options.DesignMode;
        set
        {
            if (Options.DesignMode == value) return;

            Options.DesignMode = value;
            if (value) _parts.Pointer.AbandonControlScroll();
            InvalidateVisual();
        }
    }

    public PulseOffice.Drawing.Shapes.ShapeObject? LiveShape
    {
        get => Options.LiveShape;
        set { Options.LiveShape = value; InvalidateVisual(); }
    }

    public IDrawingToolState? Tools
    {
        get => _parts.Drawing.Tools;
        set => _parts.Drawing.Tools = value;
    }

    public void Refresh() => InvalidateVisual();

    public void ApplyRowFilter(IReadOnlySet<int>? hiddenRows)
    {
        if (Viewport.Sheet is { } sheet) ApplyRowFilter(sheet, hiddenRows);
    }

    public void ApplyRowFilter(Worksheet sheet, IReadOnlySet<int>? hiddenRows)
    {
        Viewport.ApplyRowFilter(sheet, hiddenRows);
        InvalidateVisual();
    }

    public void FreezePanes(int rows, int columns)
    {
        Viewport.FreezePanes(rows, columns);
        InvalidateVisual();
    }

    public void SplitPanes(int rows, int columns)
    {
        Viewport.SplitPanes(rows, columns);
        InvalidateVisual();
    }

    public void EnsureVisible(CellAddress address)
    {
        Viewport.EnsureVisible(address);
        InvalidateVisual();
    }

    public Rect ScreenBoundsOf(PlacedObject placed)
    {
        ArgumentNullException.ThrowIfNull(placed);
        return Viewport.ToScreen(Viewport.BoundsOf(placed));
    }

    public ShapeSurroundings ShapeTextBackdrop => _theme.Backdrop();

    public PlacedObject? SelectedObject => _parts.Objects.Last;

    public IReadOnlyList<PlacedObject> SelectedObjects => _parts.Objects.Items;

    public void SelectObject(PlacedObject? placed) => _parts.Objects.Select(placed);

    public void ToggleObject(PlacedObject placed) => _parts.Objects.Toggle(placed);

    public void SelectObjects(IEnumerable<PlacedObject> objects) => _parts.Objects.SelectMany(objects);

    public override void Render(DrawingContext context)
    {
        Viewport.ApplyFrozenPanes();
        using var scale = context.PushTransform(Matrix.CreateScale(Viewport.Zoom, Viewport.Zoom));

        var foreground = Themed(Palette.Keys.TextPrimary, Brushes.Black);
        var lineBrush = Themed(Palette.Keys.Border, Brushes.LightGray);
        var headerBrush = Themed(Palette.Keys.SurfaceSunken, Brushes.WhiteSmoke);
        var surfaceBrush = Themed(Palette.Keys.Surface, Brushes.White);
        var accent = _theme.Accent;
        var surface = Viewport.Surface;
        var geometry = Viewport.Geometry;

        context.FillRectangle(surfaceBrush, new Rect(surface));

        var gridWidth = surface.Width - geometry.RowHeaderWidth;
        var gridHeight = surface.Height - geometry.ColumnHeaderHeight;
        if (gridWidth <= 0 || gridHeight <= 0) return;

        var visible = geometry.VisibleCells(Viewport.Scroll.X, Viewport.Scroll.Y, gridWidth, gridHeight);

        _parts.Headers.Paint(context, visible, headerBrush, foreground, lineBrush, accent);

        using (context.PushClip(Viewport.ScrollableArea()))
            _parts.Cells.Paint(context, visible, foreground, lineBrush);

        _parts.Cells.PaintFrozenPanes(context, visible, foreground, lineBrush);

        using (context.PushClip(Viewport.GridArea()))
        {
            _parts.Overlay.PaintViewAids(context, visible, lineBrush, foreground);
            _parts.ObjectsPainter.PaintObjects(context, _parts.Pointer.Gesture);
            _parts.ObjectsPainter.PaintFreeformOverlay(context, _parts.Pointer.Gesture);
            _parts.Overlay.PaintTraceArrows(context);
        }

        _parts.Overlay.PaintSelection(context, visible, accent, _parts.Pointer.Gesture);
        _parts.Overlay.PaintChoiceButton(context);
    }

    protected override void OnDoubleTapped(TappedEventArgs e)
    {
        base.OnDoubleTapped(e);
        _parts.Keyboard.DoubleTapped(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        _parts.Pointer.Press(Local(e), e, e.GetCurrentPoint(this).Properties.IsRightButtonPressed);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _parts.Pointer.Move(Local(e), e.KeyModifiers);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _parts.Pointer.Release();
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var zoomModifier = PulseOffice.UI.Controls.Shortcuts.ShortcutGesture.Primary;

        if (e.KeyModifiers.HasFlag(zoomModifier))
        {
            Zoom = Math.Round(Zoom + (e.Delta.Y > 0 ? 0.1 : -0.1), 2);
            e.Handled = true;
            return;
        }

        if (!(Geometry.PanesAreSplit && Viewport.ScrollPane(Local(e), e.Delta))) Viewport.ScrollBy(e.Delta);

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!_parts.Keyboard.KeyDown(e)) base.OnKeyDown(e);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        _parts.Keyboard.TextInput(e);
    }

    private Point Local(PointerEventArgs e) => Viewport.ToLocal(e.GetPosition(this));

    void IGridSurface.Invalidate() => InvalidateVisual();

    void IGridSurface.Capture(IPointer pointer) => pointer.Capture(this);

    void IGridSurface.ShowCursor(Cursor cursor) => Cursor = cursor;

    void IGridSurface.ShowTip(string? text)
    {
        if (Equals(ToolTip.GetTip(this), text)) return;

        ToolTip.SetTip(this, text);
        ToolTip.SetIsOpen(this, text is not null);
    }

    public event EventHandler<CellAddress>? EditRequested { add => Events.EditRequested += value; remove => Events.EditRequested -= value; }

    public event EventHandler<string>? TypingStarted { add => Events.TypingStarted += value; remove => Events.TypingStarted -= value; }

    public event EventHandler<CellAddress>? ChoicesRequested { add => Events.ChoicesRequested += value; remove => Events.ChoicesRequested -= value; }

    public event EventHandler<FillRequest>? FillRequested { add => Events.FillRequested += value; remove => Events.FillRequested -= value; }

    public event EventHandler<int>? FilterRequested { add => Events.FilterRequested += value; remove => Events.FilterRequested -= value; }

    public event EventHandler? ReferenceCommitRequested { add => Events.ReferenceCommitRequested += value; remove => Events.ReferenceCommitRequested -= value; }

    public event EventHandler? ReferenceCancelRequested { add => Events.ReferenceCancelRequested += value; remove => Events.ReferenceCancelRequested -= value; }

    public event EventHandler<string>? ContextMenuRequested { add => Events.ContextMenuRequested += value; remove => Events.ContextMenuRequested -= value; }

    public event EventHandler<PlacedObject>? ObjectActivated { add => Events.ObjectActivated += value; remove => Events.ObjectActivated -= value; }

    public event EventHandler<string>? ObjectLinkFollowed { add => Events.ObjectLinkFollowed += value; remove => Events.ObjectLinkFollowed -= value; }

    public event EventHandler<ShapeTextRequest>? ShapeTextRequested { add => Events.ShapeTextRequested += value; remove => Events.ShapeTextRequested -= value; }

    public event EventHandler<PlacedObject?>? SelectedObjectChanged { add => Events.SelectedObjectChanged += value; remove => Events.SelectedObjectChanged -= value; }

    public event EventHandler<FormControlInteractionEventArgs>? FormControlChanged { add => Events.FormControlChanged += value; remove => Events.FormControlChanged -= value; }

    public event EventHandler<FreeformCompletedEventArgs>? FreeformCompleted { add => Events.FreeformCompleted += value; remove => Events.FreeformCompleted -= value; }

    public event EventHandler<FreeformEditedEventArgs>? FreeformEdited { add => Events.FreeformEdited += value; remove => Events.FreeformEdited -= value; }

    public event EventHandler<ShapeAdjustedEventArgs>? ShapeAdjusted { add => Events.ShapeAdjusted += value; remove => Events.ShapeAdjusted -= value; }

    public event EventHandler<IReadOnlyList<ObjectChange>>? ObjectChangeRequested { add => Events.ObjectChangeRequested += value; remove => Events.ObjectChangeRequested -= value; }

    public event EventHandler<CellRange>? ClearRequested { add => Events.ClearRequested += value; remove => Events.ClearRequested -= value; }

    public event EventHandler<IReadOnlyList<PlacedObject>>? ObjectsDeleteRequested { add => Events.ObjectsDeleteRequested += value; remove => Events.ObjectsDeleteRequested -= value; }

    public event EventHandler<ResizeRequest>? ResizeRequested { add => Events.ResizeRequested += value; remove => Events.ResizeRequested -= value; }

    public sealed record FillRequest(CellRange Source, CellRange Target);

    public sealed record ObjectChange(
        PlacedObject Object, CellAddress Anchor, Twip OffsetX, Twip OffsetY,
        DocumentSize Size, double? RotationDegrees);

    public sealed record ResizeRequest(bool IsColumn, int Index, Twip? Before, Twip After);

    public enum GridPart { Corner, RowHeader, ColumnHeader, Cell }

    public readonly record struct GridHit(GridPart Part, int Row, int Column)
    {
        public CellAddress? Address => Part == GridPart.Cell ? new CellAddress(Row, Column) : null;
    }
}
