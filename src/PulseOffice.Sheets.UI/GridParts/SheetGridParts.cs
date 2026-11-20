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
    IGroupChildLinks GroupLinks,
    PulseOffice.UI.Controls.Links.ILinkTips LinkTips,
    IObjectNudgeKeys Nudges);

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
    GridNotePainter Notes,
    GridPointerInput Pointer,
    GridKeyboardInput Keyboard,
    PulseOffice.UI.Controls.Links.ILinkTips LinkTips);

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
        var viewport = new GridViewport(dependencies.Engine, metrics, dependencies.SheetIndex, bounds, new Outline.OutlineGutter());
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
        var icons = new ConditionalIcons();
        var cells = new GridCellPainter(
            viewport, dependencies.Presenter, dependencies.Conditional, icons, options, services, theme,
            decorations, new CellValuePainter(viewport, services, options, theme));
        var overlay = new GridOverlayPainter(viewport, selection, options, theme);
        var objectPainter = new GridObjectPainter(
            viewport, theme, objects, renderers, services, options, dependencies.Ordering,
            dependencies.Manipulator, dependencies.Adjusting, dependencies.Frame, drawing);

        var gestures = new GridGestureFactory(
            viewport, selection, surface, notifications, objects, dependencies.Manipulator,
            dependencies.Adjusting, dependencies.PathEditing, dependencies.FormInteraction,
            new ObjectSnapping());

        var notes = new GridNotePainter(viewport, theme, options);
        var pointer = new GridPointerInput(
            viewport, selection, options, surface, notifications, objects, renderers, gestures, drawing, overlay,
            decorations, dependencies.Ordering, dependencies.Manipulator, dependencies.Adjusting, dependencies.Frame,
            dependencies.PathEditing, dependencies.FormInteraction, dependencies.GroupLinks,
            new GridCellLinkTarget(viewport, dependencies.Presenter, services, options, new CellLinkHitArea(), dependencies.Conditional, icons),
            notes, new CommentBoxDrag(), dependencies.LinkTips);

        var keyboard = new GridKeyboardInput(
            viewport, selection, options, surface, notifications, objects, services, drawing, chords,
            dependencies.Nudges);

        return new SheetGridParts(
            viewport, notifications, services, options, objects, drawing,
            new GridHeaderPainter(viewport, selection, new OutlineGutterPainter(viewport)), cells, overlay, objectPainter, notes, pointer, keyboard, dependencies.LinkTips);
    }
}
