using PulseOffice.Drawing.Shapes;
using PulseOffice.Document.Forms;
using PulseOffice.Sheets.Engine.Operations;
using Avalonia;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.UI.GridParts.Gestures;
using PulseOffice.Sheets.UI.GridParts.Input;
using PulseOffice.Sheets.UI.GridParts.Painting;
using PulseOffice.UI.Controls.Drawing;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.GridParts;

internal sealed record SheetGridDependencies(
    CalculationEngine Engine,
    ICellPresenter Presenter,
    ConditionalFormattingEvaluator Conditional,
    SheetSelection Selection,
    int SheetIndex,
    IFormControlBinding FormBinding,
    IFormControlInteraction FormInteraction,
    IFormControlRenderer FormRenderer,
    PulseOffice.Formulas.IFormulaParser FormulaParser,
    PulseOffice.UI.Controls.Formulas.IFormulaRenderer FormulaRenderer,
    IObjectManipulator Manipulator,
    IPathNormalizer Normalizer,
    IDrawingObjectRenderer ObjectRenderer,
    IObjectOrdering Ordering,
    PulseOffice.UI.Controls.Objects.IPackagedObjectRenderer PackagedRenderer,
    IPathEditing PathEditing,
    ISignatureLineRenderer SignatureRenderer,
    Engine.Operations.ISparklineData Sparklines,
    IAdjustHandleChrome Adjusting,
    IObjectFrameChrome Frame,
    IGroupChildLinks GroupLinks);

internal sealed record SheetGridParts(
    GridViewport Viewport,
    GridNotifications Notifications,
    GridRenderServices Services,
    GridDisplayOptions Options,
    GridObjectSelection Objects,
    FreeformDrawing Drawing,
    GridHeaderPainter Headers,
    GridCellPainter Cells,
    GridOverlayPainter Overlay,
    GridObjectPainter ObjectsPainter,
    GridPointerInput Pointer,
    GridKeyboardInput Keyboard);

internal sealed class SheetGridPartsFactory
{
    public SheetGridParts Create(
        SheetGridDependencies dependencies,
        object sender,
        IGridSurface surface,
        IGridTheme theme,
        Func<Size> bounds,
        PulseOffice.UI.Controls.Shortcuts.TypingChordGuard chords)
    {
        var metrics = DesignTokens.For(InterfaceDensity.Compact);
        var viewport = new GridViewport(dependencies.Engine, metrics, dependencies.SheetIndex, bounds);
        var notifications = new GridNotifications(sender);
        var services = new GridRenderServices();
        var options = new GridDisplayOptions();
        var selection = dependencies.Selection;
        var objects = new GridObjectSelection(notifications, surface);
        var drawing = new FreeformDrawing(
            viewport, surface, notifications, new FreeformSketch(new PathAuthoring()), dependencies.Normalizer);

        var renderers = new GridObjectRenderers(
            dependencies.ObjectRenderer, dependencies.FormRenderer, dependencies.SignatureRenderer,
            dependencies.FormulaRenderer, dependencies.PackagedRenderer, dependencies.FormulaParser,
            dependencies.FormBinding);

        var decorations = new CellDecorationPainter(theme, services, dependencies.Sparklines);
        var cells = new GridCellPainter(
            viewport, dependencies.Presenter, dependencies.Conditional, new ConditionalIcons(), options, services, theme,
            decorations, new CellValuePainter(viewport, services, options));
        var overlay = new GridOverlayPainter(viewport, selection, options, theme);
        var objectPainter = new GridObjectPainter(
            viewport, theme, objects, renderers, services, options, dependencies.Ordering,
            dependencies.Manipulator, dependencies.Adjusting, dependencies.Frame, drawing);

        var gestures = new GridGestureFactory(
            viewport, selection, surface, notifications, objects, dependencies.Manipulator,
            dependencies.Adjusting, dependencies.PathEditing, dependencies.FormInteraction,
            new ObjectSnapping());

        var pointer = new GridPointerInput(
            viewport, selection, options, surface, notifications, objects, renderers, gestures, drawing, overlay,
            decorations, dependencies.Ordering, dependencies.Manipulator, dependencies.Adjusting, dependencies.Frame,
            dependencies.PathEditing, dependencies.FormInteraction, dependencies.GroupLinks);

        var keyboard = new GridKeyboardInput(
            viewport, selection, options, surface, notifications, objects, services, drawing, chords,
            new PulseOffice.UI.Controls.Shapes.ObjectNudgeKeys());

        return new SheetGridParts(
            viewport, notifications, services, options, objects, drawing,
            new GridHeaderPainter(viewport, selection), cells, overlay, objectPainter, pointer, keyboard);
    }
}
