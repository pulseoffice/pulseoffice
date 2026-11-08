using PulseOffice.Foundation.Zoom;

namespace PulseOffice.Documents.UI.Canvas;

public sealed record PageSlot(int Index, double X, double Y, double Width, double Height)
{
    public double CropTop { get; init; }

    public double CropBottom { get; init; }

    public double VisibleTop => Y + CropTop;

    public double VisibleBottom => Y + Height - CropBottom;

    public bool Contains(double x, double y) => x >= X && x <= X + Width && y >= VisibleTop && y <= VisibleBottom;

    public bool Intersects(double top, double bottom) => VisibleTop < bottom && VisibleBottom > top;
}

public sealed record DeskArrangement(IReadOnlyList<PageSlot> Pages, double Width, double Height, double Scale);

public enum DeskLayout { Single, Multiple, Book }

public interface IPageDesk
{
    double ScaleFor(ViewZoom zoom, IReadOnlyList<(double Width, double Height)> pages, double viewportWidth, double viewportHeight);

    DeskArrangement Arrange(IReadOnlyList<(double Width, double Height)> pages, double scale, double viewportWidth, DeskLayout layout = DeskLayout.Single,
        IReadOnlyList<(double Top, double Bottom)>? crops = null);
}

public sealed class PageDesk : IPageDesk
{
    public const double PointsToPixels = 96.0 / 72.0;
    public const double Margin = 32;
    public const double Gap = 24;

    public double ScaleFor(ViewZoom zoom, IReadOnlyList<(double Width, double Height)> pages, double viewportWidth, double viewportHeight)
    {
        if (zoom.Mode == ViewZoomMode.Fixed || pages.Count == 0) return zoom.Factor * PointsToPixels;
        var width = pages.Max(page => page.Width);
        var height = pages.Max(page => page.Height);
        var byWidth = (viewportWidth - Margin * 2) / Math.Max(1, width);
        var scale = zoom.Mode == ViewZoomMode.FitWidth ? byWidth : Math.Min(byWidth, (viewportHeight - Margin * 2) / Math.Max(1, height));
        return Math.Clamp(scale, ZoomLimits.Pages.Minimum * PointsToPixels, ZoomLimits.Pages.Maximum * PointsToPixels);
    }

    public DeskArrangement Arrange(IReadOnlyList<(double Width, double Height)> pages, double scale, double viewportWidth, DeskLayout layout = DeskLayout.Single,
        IReadOnlyList<(double Top, double Bottom)>? crops = null)
    {
        if (crops is not null && layout == DeskLayout.Single) return Cropped(pages, scale, viewportWidth, crops);
        var rows = Rows(pages, scale, viewportWidth, layout);
        var widest = rows.Count == 0 ? 0 : rows.Max(row => RowWidth(pages, row, scale, layout));
        var width = Math.Max(viewportWidth, widest + Margin * 2);
        var slots = new PageSlot[pages.Count];
        var y = Margin;
        foreach (var row in rows)
        {
            var height = row.Max(index => pages[index].Height * scale);
            if (layout == DeskLayout.Book) PlaceBook(slots, pages, row, scale, width / 2, y);
            else
            {
                var x = (width - RowWidth(pages, row, scale, layout)) / 2;
                foreach (var index in row)
                {
                    slots[index] = new PageSlot(index, x, y, pages[index].Width * scale, pages[index].Height * scale);
                    x += pages[index].Width * scale + Gap;
                }
            }
            y += height + Gap;
        }
        return new DeskArrangement(slots, width, pages.Count == 0 ? Margin * 2 : y - Gap + Margin, scale);
    }

    private DeskArrangement Cropped(IReadOnlyList<(double Width, double Height)> pages, double scale, double viewportWidth, IReadOnlyList<(double Top, double Bottom)> crops)
    {
        var widest = pages.Count == 0 ? 0 : pages.Max(page => page.Width) * scale;
        var width = Math.Max(viewportWidth, widest + Margin * 2);
        var slots = new PageSlot[pages.Count];
        var y = Margin;
        for (var index = 0; index < pages.Count; index++)
        {
            var (top, bottom) = index < crops.Count ? crops[index] : (0, 0);
            var pageWidth = pages[index].Width * scale;
            var pageHeight = pages[index].Height * scale;
            slots[index] = new PageSlot(index, (width - pageWidth) / 2, y - top * scale, pageWidth, pageHeight) { CropTop = top * scale, CropBottom = bottom * scale };
            y += pageHeight - (top + bottom) * scale + CroppedGap;
        }
        return new DeskArrangement(slots, width, pages.Count == 0 ? Margin * 2 : y - CroppedGap + Margin, scale);
    }

    private const double CroppedGap = 3;

    private void PlaceBook(PageSlot[] slots, IReadOnlyList<(double Width, double Height)> pages, IReadOnlyList<int> row, double scale, double spine, double y)
    {
        foreach (var index in row)
        {
            var width = pages[index].Width * scale;
            var right = index % 2 == 0;
            slots[index] = new PageSlot(index, right ? spine + Gap / 4 : spine - Gap / 4 - width, y, width, pages[index].Height * scale);
        }
    }

    private double RowWidth(IReadOnlyList<(double Width, double Height)> pages, IReadOnlyList<int> row, double scale, DeskLayout layout) =>
        layout == DeskLayout.Book
            ? 2 * row.Max(index => pages[index].Width * scale) + Gap / 2
            : row.Sum(index => pages[index].Width * scale) + Gap * (row.Count - 1);

    private List<IReadOnlyList<int>> Rows(IReadOnlyList<(double Width, double Height)> pages, double scale, double viewportWidth, DeskLayout layout)
    {
        var rows = new List<IReadOnlyList<int>>();
        if (pages.Count == 0) return rows;
        switch (layout)
        {
            case DeskLayout.Book:
                rows.Add([0]);
                for (var index = 1; index < pages.Count; index += 2)
                    rows.Add(index + 1 < pages.Count ? [index, index + 1] : [index]);
                break;
            case DeskLayout.Multiple:
                var room = viewportWidth - Margin * 2;
                var current = new List<int>();
                var used = 0.0;
                for (var index = 0; index < pages.Count; index++)
                {
                    var width = pages[index].Width * scale;
                    if (current.Count > 0 && used + Gap + width > room)
                    {
                        rows.Add(current);
                        current = [];
                        used = 0;
                    }
                    used += (current.Count > 0 ? Gap : 0) + width;
                    current.Add(index);
                }
                rows.Add(current);
                break;
            default:
                for (var index = 0; index < pages.Count; index++) rows.Add([index]);
                break;
        }
        return rows;
    }
}
