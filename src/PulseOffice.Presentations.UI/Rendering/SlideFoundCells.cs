using Avalonia;
using Avalonia.Media;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Spelling;

namespace PulseOffice.Presentations.UI.Rendering;

public interface ISlideFoundCells
{
    void Paint(DrawingContext context, Slide slide, Func<SlideElement, Rect> bounds, double scale,
        Func<string, IReadOnlyList<WordSpan>> found, IBrush fill);
}

public sealed class SlideFoundCells(ISlideTextSources sources, TableGeometry geometry) : ISlideFoundCells
{
    public SlideFoundCells() : this(new SlideTextSources(), new TableGeometry())
    {
    }

    public void Paint(DrawingContext context, Slide slide, Func<SlideElement, Rect> bounds, double scale,
        Func<string, IReadOnlyList<WordSpan>> found, IBrush fill)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(slide);
        foreach (var element in slide.Elements)
        {
            if (element is not ObjectElement { Content: DataTableObject table }) continue;
            var box = bounds(element);
            var points = scale * Twip.PerPoint;
            foreach (var cell in sources.Of(element))
            {
                if (!cell.IsCell || !cell.Text.Paragraphs.Any(paragraph => found(paragraph.Text).Count > 0)) continue;
                var area = geometry.Area(table, new TableCellRange(cell.Row, cell.Column, cell.Row, cell.Column));
                context.FillRectangle(fill, new Rect(box.X + area.X * box.Width, box.Y + area.Y * points, area.Width * box.Width, area.Height * points));
            }
        }
    }
}
