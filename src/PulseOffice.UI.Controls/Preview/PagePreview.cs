using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Document.Pages;

namespace PulseOffice.UI.Controls.Preview;

public sealed class PagePreview : Control
{
    private const double Inset = 8;

    private readonly IPageSketchLayout _layout;
    private readonly IBrush _shadow = new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0));
    private readonly IBrush _ink = new SolidColorBrush(Color.FromArgb(0x55, 0x4B, 0x55, 0x63));
    private readonly IBrush _band = new SolidColorBrush(Color.FromArgb(0x16, 0x25, 0x63, 0xEB));
    private readonly IBrush _bandText = new SolidColorBrush(Color.FromRgb(0x37, 0x41, 0x51));
    private readonly IPen _guide = new Pen(new SolidColorBrush(Color.FromArgb(0xB0, 0x60, 0x8B, 0xD8)), 0.8, new DashStyle([3, 2], 0));
    private readonly IPen _cell = new Pen(new SolidColorBrush(Color.FromArgb(0x40, 0x4B, 0x55, 0x63)), 0.5);
    private PageSketch? _sketch;

    public PagePreview(IPageSketchLayout layout)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
    }

    public PageSketch? Sketch
    {
        get => _sketch;
        set
        {
            _sketch = value;
            InvalidateVisual();
        }
    }

    public PageSketchGeometry? Geometry => _sketch is null || Bounds.Width <= 0 ? null : _layout.Fit(_sketch, Bounds.Width, Bounds.Height, Inset);

    public override void Render(DrawingContext context)
    {
        if (Geometry is not { } geometry || _sketch is null || geometry.Page.IsEmpty) return;

        var page = Rect(geometry.Page);
        context.FillRectangle(_shadow, page.Translate(new Vector(1.5, 2)));
        context.FillRectangle(new SolidColorBrush(Color.FromUInt32(_sketch.Paper)), page);

        using (context.PushClip(page))
        {
            if (geometry.Header is { } header) Band(context, Rect(header), _sketch.Header!);
            if (geometry.Footer is { } footer) Band(context, Rect(footer), _sketch.Footer!);
            foreach (var column in geometry.Columns) Body(context, Rect(column), geometry.Scale);
            if (!_sketch.Margins.IsEmpty && !geometry.Content.IsEmpty) context.DrawRectangle(null, _guide, Rect(geometry.Content));
            if (_sketch.Watermark is { Length: > 0 } watermark) Watermark(context, page, watermark);
        }
    }

    private Rect Rect(SketchRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    private void Band(DrawingContext context, Rect area, PageSketchBand band)
    {
        if (area.Width <= 0 || area.Height <= 0) return;
        context.FillRectangle(_band, area);
        if (!band.HasText)
        {
            var bar = Math.Max(1, area.Height * 0.3);
            context.FillRectangle(_ink, new Rect(area.X + area.Width * 0.35, area.Center.Y - bar / 2, area.Width * 0.3, bar));
            return;
        }

        var size = Math.Clamp(area.Height * 0.6, 4, 11);
        Text(context, band.Left, area, size, TextAlignment.Left);
        Text(context, band.Center, area, size, TextAlignment.Center);
        Text(context, band.Right, area, size, TextAlignment.Right);
    }

    private void Text(DrawingContext context, string? text, Rect area, double size, TextAlignment alignment)
    {
        if (string.IsNullOrEmpty(text)) return;
        var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface.Default, size, _bandText)
        {
            MaxTextWidth = Math.Max(1, area.Width / 3),
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
        };
        var x = alignment switch
        {
            TextAlignment.Center => area.Center.X - formatted.Width / 2,
            TextAlignment.Right => area.Right - formatted.Width - 1,
            _ => area.X + 1,
        };
        context.DrawText(formatted, new Point(x, area.Center.Y - formatted.Height / 2));
    }

    private void Body(DrawingContext context, Rect area, double scale)
    {
        if (area.Width <= 0 || area.Height <= 0 || _sketch is null) return;
        switch (_sketch.Body)
        {
            case PageSketchBody.TextLines:
                var pitch = Math.Max(3, 14 * scale);
                var thickness = Math.Max(1, pitch * 0.35);
                var line = 0;
                for (var y = area.Y + pitch / 2; y + thickness <= area.Bottom; y += pitch, line++)
                {
                    var width = line % 7 == 6 ? area.Width * 0.55 : area.Width;
                    context.FillRectangle(_ink, new Rect(area.X, y, width, thickness));
                }
                break;
            case PageSketchBody.Cells:
                var cellHeight = Math.Max(3, 16 * scale);
                var cellWidth = Math.Max(6, 64 * scale);
                for (var y = area.Y; y <= area.Bottom + 0.01; y += cellHeight) context.DrawLine(_cell, new Point(area.X, y), new Point(area.Right, y));
                for (var x = area.X; x <= area.Right + 0.01; x += cellWidth) context.DrawLine(_cell, new Point(x, area.Y), new Point(x, area.Bottom));
                break;
        }
    }

    private void Watermark(DrawingContext context, Rect page, string watermark)
    {
        var formatted = new FormattedText(watermark, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface.Default, Math.Max(6, page.Width / 8), _ink);
        using (context.PushTransform(Matrix.CreateTranslation(-formatted.Width / 2, -formatted.Height / 2) * Matrix.CreateRotation(-Math.PI / 5) * Matrix.CreateTranslation(page.Center.X, page.Center.Y)))
            context.DrawText(formatted, default);
    }
}
