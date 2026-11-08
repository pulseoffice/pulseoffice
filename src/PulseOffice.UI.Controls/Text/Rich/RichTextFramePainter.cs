using Avalonia;
using Avalonia.Media;
using PulseOffice.Text.Rich;
using PulseOffice.Text.Spelling;

namespace PulseOffice.UI.Controls.Text.Rich;

public sealed record RichTextScene(
    DrawingContext Context, Rect Bounds, double Scale, ITextTheme Theme, TextFields Fields,
    Func<string, IReadOnlyList<WordSpan>>? Proofing = null);

public interface IRichTextFramePainter
{
    void Paint(RichTextScene scene, RichText text);
}

public sealed class RichTextFramePainter(ITextFrameComposer composer, IBulletPictureBitmaps pictures) : IRichTextFramePainter
{
    public RichTextFramePainter(ITextFrameComposer composer) : this(composer, new BulletPictureBitmaps())
    {
    }

    private readonly SpellingWaves _waves = new();
    private readonly RunText _runText = new();

    public void Paint(RichTextScene scene, RichText text)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(text);

        var composed = composer.Compose(text, scene.Bounds, scene.Theme, scene.Fields, scene.Scale);
        for (var index = 0; index < composed.Lines.Count; index++)
        {
            var line = composed.Lines[index];
            if (line.Label is { } label) scene.Context.DrawText(label, new Point(composed.Area.Inner.X + line.LabelX, composed.Tops[index]));
            if (line.Picture is { } picture) PaintPicture(scene, picture, picture.Box.Translate(new Vector(composed.Area.Inner.X, composed.Tops[index])), line);
            var words = scene.Proofing is { } proofing && line.Paragraph.EffectiveRuns().All(run => run.Field == TextFieldKind.None)
                ? proofing(line.Paragraph.Text)
                : null;
            foreach (var segment in line.Segments)
            {
                var origin = composed.Origin(index, segment);
                PaintHighlights(scene, line, segment, origin);
                scene.Context.DrawText(segment.Text, origin);
                if (words is not null) _waves.Paint(scene.Context, segment.Text, origin, line.Offset - segment.Start, Within(words, line.Offset, segment), scene.Scale);
            }
        }
    }

    private void PaintPicture(RichTextScene scene, TypesetBulletPicture picture, Rect box, TypesetLine line)
    {
        if (box.Width <= 0 || box.Height <= 0) return;
        if (pictures.For(picture.Picture.Data) is { } bitmap)
        {
            scene.Context.DrawImage(bitmap, new Rect(bitmap.Size), box);
            return;
        }
        var ink = Color.FromUInt32(line.Paragraph.Color ?? scene.Theme.Resolve(line.Paragraph.ColorRole ?? ColorRole.Text));
        scene.Context.DrawRectangle(null, new Pen(new SolidColorBrush(ink), Math.Max(0.5, scene.Scale * 10)), box);
    }

    private void PaintHighlights(RichTextScene scene, TypesetLine line, TypesetSegment segment, Point origin)
    {
        var position = line.Offset;
        foreach (var run in line.Paragraph.EffectiveRuns())
        {
            var length = _runText.Shown(run, scene.Fields).Length;
            var from = Math.Max(position, segment.Start);
            var to = Math.Min(position + length, segment.End);
            position += length;
            if (run.Highlight is not { } colour || to <= from) continue;

            var area = segment.Text.BuildHighlightGeometry(origin, from - segment.Start, to - from);
            if (area is not null) scene.Context.DrawGeometry(new SolidColorBrush(Color.FromUInt32(colour)), null, area);
        }
    }

    private IReadOnlyList<WordSpan> Within(IReadOnlyList<WordSpan> words, int offset, TypesetSegment segment) =>
        [.. words.Where(word => offset + word.Start >= segment.Start && offset + word.Start + word.Length <= segment.End)];
}
