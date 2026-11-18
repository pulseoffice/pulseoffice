using Avalonia;
using Avalonia.Media;
using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.Text.Rich;

public sealed record UprightGlyph(FormattedText Glyph, int Start, int End);

public interface IUprightGlyphs
{
    Matrix Stand(Rect cell);

    Point Origin(Rect cell, Size glyph);

    IReadOnlyList<UprightGlyph> Lift(FormattedText text, int offset, int length, IReadOnlyList<TextCluster> clusters, Func<int, int, FormattedText> glyph);

    void Paint(DrawingContext context, FormattedText text, Point origin, int offset, IReadOnlyList<UprightGlyph> glyphs);
}

public sealed class UprightGlyphs : IUprightGlyphs
{
    public Matrix Stand(Rect cell)
    {
        var centre = cell.Center;
        return Matrix.CreateTranslation(-centre.X, -centre.Y)
            * Matrix.CreateRotation(-Math.PI / 2)
            * Matrix.CreateTranslation(centre.X, centre.Y);
    }

    public Point Origin(Rect cell, Size glyph) => new(cell.Center.X - glyph.Width / 2, cell.Center.Y - glyph.Height / 2);

    public IReadOnlyList<UprightGlyph> Lift(FormattedText text, int offset, int length, IReadOnlyList<TextCluster> clusters, Func<int, int, FormattedText> glyph)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(clusters);
        ArgumentNullException.ThrowIfNull(glyph);
        var lifted = new List<UprightGlyph>();
        foreach (var cluster in clusters)
        {
            if (cluster.Start < offset || cluster.End > offset + length) continue;
            lifted.Add(new UprightGlyph(glyph(cluster.Start, cluster.End), cluster.Start, cluster.End));
            text.SetForegroundBrush(Brushes.Transparent, cluster.Start - offset, cluster.End - cluster.Start);
        }
        return lifted;
    }

    public void Paint(DrawingContext context, FormattedText text, Point origin, int offset, IReadOnlyList<UprightGlyph> glyphs)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(glyphs);
        foreach (var glyph in glyphs)
        {
            var area = text.BuildHighlightGeometry(origin, glyph.Start - offset, glyph.End - glyph.Start);
            if (area is null || area.Bounds.Width <= 0) continue;
            var cell = area.Bounds;
            using (context.PushTransform(Stand(cell)))
                context.DrawText(glyph.Glyph, Origin(cell, new Size(glyph.Glyph.WidthIncludingTrailingWhitespace, glyph.Glyph.Height)));
        }
    }
}
