using PulseOffice.Foundation.Units;
using Avalonia;
using Avalonia.Input;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.UI.Controls.Drawing;

namespace PulseOffice.Sheets.UI.GridParts.Input;

internal sealed class FreeformDrawing(
    GridViewport viewport,
    IGridSurface surface,
    GridNotifications notifications,
    FreeformSketch sketch,
    IPathNormalizer normalizer)
{
    public IDrawingToolState? Tools
    {
        get;
        set
        {
            if (ReferenceEquals(field, value)) return;

            if (field is not null) field.Changed -= ToolsChanged;

            field = value;

            if (field is not null) field.Changed += ToolsChanged;
        }
    }

    public FreeformSketch Sketch => sketch;

    public bool Drawing => Tools is { Tool: not DrawingTool.None };

    private void ToolsChanged(object? sender, EventArgs e)
    {
        sketch.Cancel();

        surface.Invalidate();
    }

    public bool Begin(Point point, PointerPressedEventArgs e)
    {
        if (Tools is not { } tools || tools.Tool == DrawingTool.None) return false;

        e.Handled = true;
        if (!sketch.Begin(tools.Tool, new ShapePoint(point.X, point.Y), e.ClickCount, tools.Filled, out var done)) return false;
        if (tools.Tool == DrawingTool.Freehand) surface.Capture(e.Pointer);
        if (done is not null || !sketch.Active) Deliver(done, tools);
        surface.Invalidate();
        return true;
    }

    public void Continue(Point point)
    {
        if (Tools is not { } tools) return;
        sketch.Move(tools.Tool, new ShapePoint(point.X, point.Y), pressed: true);
        surface.Invalidate();
    }

    public void Complete(bool closed)
    {
        if (Tools is not { } tools) return;
        Deliver(sketch.Complete(tools.Tool, tools.Filled, closed), tools);
    }

    public void Release()
    {
        if (Tools is { Tool: DrawingTool.Freehand } tools && sketch.Active) Deliver(sketch.Release(tools.Tool, tools.Filled), tools);
    }

    public void Cancel()
    {
        sketch.Cancel();

        Tools?.Finish();
        surface.Invalidate();
    }

    private void Deliver(SketchResult? result, IDrawingToolState tools)
    {
        if (result is { } sketched)
        {
            var normalized = normalizer.ToUnitBox(sketched.Path);
            var box = normalized.Bounds;

            var address = viewport.AnchorAt(box.X, box.Y);
            var cell = viewport.CellRect(address);

            notifications.Freeform(new FreeformCompletedEventArgs(
                normalized.Path,
                address,
                GridGeometry.ToTwips(box.X - cell.X),
                GridGeometry.ToTwips(box.Y - cell.Y),
                new DocumentSize(GridGeometry.ToTwips(box.Width), GridGeometry.ToTwips(box.Height)),
                tools.Filled));
        }

        tools.Finish();
        surface.Invalidate();
    }
}
