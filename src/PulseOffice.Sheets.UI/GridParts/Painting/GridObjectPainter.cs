using PulseOffice.Document.Forms;
using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Sheets.UI.GridParts.Gestures;
using PulseOffice.Sheets.UI.GridParts.Input;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.GridParts.Painting;

internal sealed class GridObjectPainter(
    GridViewport viewport,
    IGridTheme theme,
    GridObjectSelection objects,
    GridObjectRenderers renderers,
    GridRenderServices services,
    GridDisplayOptions options,
    IObjectOrdering ordering,
    IObjectManipulator manipulator,
    IAdjustHandleChrome adjusting,
    IObjectFrameChrome frame,
    FreeformDrawing drawing)
{
    public double AdjustSize => manipulator.HandleSize / 2 + 1;

    public void PaintObjects(DrawingContext context, GridGesture? gesture)
    {
        var sheet = viewport.Sheet;
        if (sheet is null || sheet.Objects.Count == 0) return;

        var drag = gesture as ObjectDragGesture;
        var adjust = gesture as ShapeAdjustGesture;

        foreach (var placed in ordering.Drawn(sheet))
        {
            var bounds = viewport.BoundsOf(placed);

            if (!bounds.Intersects(viewport.GridArea())) continue;

            PaintContent(context, Previewed(placed, gesture), bounds);

            if (!objects.Contains(placed)) continue;

            var area = drag?.Preview ?? ToBox(bounds);
            var degrees = drag?.RotationPreview ?? viewport.RotationOf(placed);
            frame.Draw(context, new FrameScene(area, degrees, theme.Accent));

            if (drag?.Preview is null && drag?.RotationPreview is null) PaintAdjustHandles(context, placed, bounds, adjust);
        }
    }

    private void PaintContent(DrawingContext context, PlacedObject placed, Rect bounds)
    {
        switch (placed.Content)
        {
            case Document.Embedding.FormulaObject formula:
                renderers.Formulas.Render(
                    context,
                    renderers.FormulaParser.Parse(formula.Expression),
                    bounds,
                    formula.FontSize,
                    theme.Brush(Palette.Keys.TextPrimary, Brushes.Black));
                return;

            case SignatureLineObject signature:
                renderers.Signatures.Render(context, signature, bounds, theme.FormPalette(), renderers.Culture);
                return;

            case Document.Embedding.PackagedObject packaged:
                renderers.Packaged.Render(context, packaged, bounds, theme.PackagedPalette());
                return;

            case FormControlObject form:
                renderers.Forms.Render(
                    context, form.ControlKind, form.Properties, renderers.ItemsOf(viewport, form), bounds, theme.FormPalette(), renderers.Culture);
                return;
        }

        if (renderers.Shapes.CanRender(placed.Content))
        {
            var content = options.LiveShape is { } live && live.Id == placed.Content.Id ? live : placed.Content;
            renderers.Shapes.Render(context, content, bounds, theme.Backdrop() with { Language = viewport.Engine.Workbook.TextLanguage });
            return;
        }

        if (placed.Content is ChartDefinition chart)
        {
            context.FillRectangle(Brushes.White, bounds);
            context.DrawRectangle(new Pen(Brushes.LightGray), bounds);
            services.ChartRenderer.Render(
                context, chart, services.ChartData.Extract(viewport.Engine.Workbook, chart, visibleOnly: true), bounds, renderers.Culture);
            return;
        }

        context.FillRectangle(theme.Brush(Palette.Keys.Surface, Brushes.White), bounds);
        context.DrawRectangle(new Pen(theme.Brush(Palette.Keys.BorderStrong, Brushes.Gray)), bounds);
    }

    private PlacedObject Previewed(PlacedObject placed, GridGesture? gesture) => gesture switch
    {
        PathEditGesture { Preview: { } path } edit when edit.Placed.Id == placed.Id && placed.Content is FreeformObject freeform =>
            placed with { Content = freeform with { Path = path } },
        FormControlScrollGesture { Preview: { } properties } scroll when scroll.Placed.Id == placed.Id && placed.Content is FormControlObject form =>
            placed with { Content = form with { Properties = properties } },
        _ => placed,
    };

    private void PaintAdjustHandles(DrawingContext context, PlacedObject placed, Rect bounds, ShapeAdjustGesture? adjust)
    {
        if (adjust is not null && ReferenceEquals(adjust.Placed, placed))
            adjusting.DrawPreview(context, adjust.Grip, new(bounds, viewport.RotationOf(placed)), AdjustSize, 1.2);
        else if (objects.Adjustable(placed) is { } shape)
            adjusting.Draw(context, shape, new(bounds, viewport.RotationOf(placed)), AdjustSize);
    }

    public void PaintFreeformOverlay(DrawingContext context, GridGesture? gesture)
    {
        var accent = theme.Accent;

        drawing.Sketch.Paint(context, ToPoint, accent);
        PaintPathNodes(context, accent, gesture);
    }

    private void PaintPathNodes(DrawingContext context, IBrush accent, GridGesture? gesture)
    {
        if (drawing.Tools is not { EditingPoints: true }) return;
        if (objects.Last is not { } last || Previewed(last, gesture) is not { Content: FreeformObject freeform } placed) return;

        var bounds = viewport.BoundsOf(placed);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        Point Map(ShapePoint point) =>
            new(bounds.X + (point.X * bounds.Width), bounds.Y + (point.Y * bounds.Height));

        var handlePen = new Pen(accent, 1);

        foreach (var figure in freeform.Path.Figures)
            foreach (var node in figure.Nodes)
            {
                var centre = Map(node.Point);

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

                if (node.Kind == PathNodeKind.Corner)
                    context.DrawRectangle(
                        Brushes.White, new Pen(accent, 1.5),
                        new Rect(centre.X - 4, centre.Y - 4, 8, 8));
                else
                    context.DrawEllipse(Brushes.White, new Pen(accent, 1.5), centre, 4.5, 4.5);
            }
    }

    private Point ToPoint(ShapePoint point) => new(point.X, point.Y);

    private ObjectBox ToBox(Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

}
