using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Runtime.CompilerServices;
using PulseOffice.Document.Embedding;

namespace PulseOffice.UI.Controls.Objects;

public sealed record PackagedPalette(IBrush Surface, IBrush Border, IBrush Text, IBrush Accent);

public interface IPackagedObjectRenderer
{
    void Render(DrawingContext context, PackagedObject packaged, Rect bounds, PackagedPalette palette);
}

public sealed class PackagedObjectRenderer : IPackagedObjectRenderer
{
    private readonly ConditionalWeakTable<PackagedObject, Bitmap?> _replacements = [];

    public void Render(
        DrawingContext context, PackagedObject packaged, Rect bounds, PackagedPalette palette)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(packaged);
        ArgumentNullException.ThrowIfNull(palette);

        if (Replacement(packaged) is { } picture)
        {
            context.DrawImage(picture, new Rect(picture.Size), bounds);
            return;
        }

        context.DrawRectangle(palette.Surface, new Pen(palette.Border), new RoundedRect(bounds, 6));

        if (packaged.Packaged == PackagedObjectKind.Media) Play(context, bounds, palette);
        else Sheet(context, bounds, palette);

        var label = new FormattedText(
            packaged.FileName,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            11,
            palette.Text)
        {
            MaxTextWidth = Math.Max(10, bounds.Width - 12),
            MaxTextHeight = 32,
            TextAlignment = TextAlignment.Center,
        };

        context.DrawText(label, new Point(bounds.X + 6, bounds.Bottom - label.Height - 6));
    }

    private Bitmap? Replacement(PackagedObject packaged)
    {
        if (!packaged.HasReplacement) return null;
        return _replacements.GetValue(packaged, Decode);
    }

    private Bitmap? Decode(PackagedObject packaged)
    {
        try
        {
            using var stream = new MemoryStream(packaged.Replacement!.Value.ToArray());
            return new Bitmap(stream);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    private static void Play(DrawingContext context, Rect bounds, PackagedPalette palette)
    {
        var radius = Math.Min(bounds.Width, bounds.Height) * 0.22;
        var centre = new Point(bounds.Center.X, bounds.Y + (bounds.Height * 0.42));

        context.DrawEllipse(palette.Accent, null, centre, radius, radius);

        var geometry = new StreamGeometry();

        using (var sink = geometry.Open())
        {
            sink.BeginFigure(new Point(centre.X - (radius * 0.3), centre.Y - (radius * 0.45)), true);
            sink.LineTo(new Point(centre.X + (radius * 0.5), centre.Y));
            sink.LineTo(new Point(centre.X - (radius * 0.3), centre.Y + (radius * 0.45)));
            sink.EndFigure(true);
        }

        context.DrawGeometry(Brushes.White, null, geometry);
    }

    private static void Sheet(DrawingContext context, Rect bounds, PackagedPalette palette)
    {
        var width = Math.Min(bounds.Width * 0.3, bounds.Height * 0.4);
        var height = width * 1.3;

        var page = new Rect(
            bounds.Center.X - (width / 2),
            bounds.Y + (bounds.Height * 0.42) - (height / 2),
            width,
            height);

        context.DrawRectangle(Brushes.White, new Pen(palette.Accent, 1.5), page, 2, 2);

        var fold = new StreamGeometry();

        using (var sink = fold.Open())
        {
            sink.BeginFigure(new Point(page.Right - (width * 0.32), page.Y), true);
            sink.LineTo(new Point(page.Right, page.Y + (height * 0.25)));
            sink.LineTo(new Point(page.Right - (width * 0.32), page.Y + (height * 0.25)));
            sink.EndFigure(true);
        }

        context.DrawGeometry(palette.Accent, null, fold);
    }
}
