namespace PulseOffice.Document.Pages;

public interface IPageSketchLayout
{
    PageSketchGeometry Fit(PageSketch sketch, double availableWidth, double availableHeight, double inset);
}

public sealed class PageSketchLayout : IPageSketchLayout
{
    public PageSketchGeometry Fit(PageSketch sketch, double availableWidth, double availableHeight, double inset)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        var (width, height) = OrientedSize(sketch);
        var room = (Width: Math.Max(0, availableWidth - 2 * inset), Height: Math.Max(0, availableHeight - 2 * inset));
        var scale = width <= 0 || height <= 0 ? 0 : Math.Min(room.Width / width, room.Height / height);
        var page = new SketchRect(
            (availableWidth - width * scale) / 2,
            (availableHeight - height * scale) / 2,
            width * scale,
            height * scale);

        var margins = sketch.Margins;
        var content = Clamp(new SketchRect(
            page.X + margins.Left * scale,
            page.Y + margins.Top * scale,
            page.Width - (margins.Left + margins.Right) * scale,
            page.Height - (margins.Top + margins.Bottom) * scale));

        var top = content.Y;
        var bottom = content.Bottom;
        SketchRect? header = null;
        SketchRect? footer = null;
        if (sketch.Header is { Height: > 0 } headerBand)
        {
            header = new SketchRect(content.X, top, content.Width, Math.Min(content.Height, headerBand.Height * scale));
            top = Math.Min(bottom, top + (headerBand.Height + headerBand.Spacing) * scale);
        }
        if (sketch.Footer is { Height: > 0 } footerBand)
        {
            var bandHeight = Math.Min(Math.Max(0, bottom - top), footerBand.Height * scale);
            footer = new SketchRect(content.X, bottom - bandHeight, content.Width, bandHeight);
            bottom = Math.Max(top, bottom - (footerBand.Height + footerBand.Spacing) * scale);
        }

        var body = Clamp(new SketchRect(content.X, top, content.Width, bottom - top));
        return new PageSketchGeometry(scale, page, content, header, footer, body, Split(body, Math.Max(1, sketch.Columns), sketch.ColumnGap * scale));
    }

    private (double Width, double Height) OrientedSize(PageSketch sketch)
    {
        var shortSide = Math.Min(sketch.Width, sketch.Height);
        var longSide = Math.Max(sketch.Width, sketch.Height);
        return sketch.Orientation == PageSketchOrientation.Landscape ? (longSide, shortSide) : (shortSide, longSide);
    }

    private IReadOnlyList<SketchRect> Split(SketchRect body, int columns, double gap)
    {
        gap = Math.Min(gap, columns > 1 ? body.Width / (columns - 1) / 2 : 0);
        var width = (body.Width - gap * (columns - 1)) / columns;
        return [.. Enumerable.Range(0, columns).Select(column => new SketchRect(body.X + column * (width + gap), body.Y, width, body.Height))];
    }

    private SketchRect Clamp(SketchRect rect) => new(rect.X, rect.Y, Math.Max(0, rect.Width), Math.Max(0, rect.Height));
}
