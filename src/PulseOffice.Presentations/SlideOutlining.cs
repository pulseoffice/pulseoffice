using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations;

public sealed record OutlineSlide(string Title, IReadOnlyList<TextParagraph> Body);

public interface ISlideOutlining
{
    OutlineSlide? Summary(IEnumerable<Slide> slides);

    IReadOnlyList<OutlineSlide> Expand(Slide slide);
}

public sealed class SlideOutlining : ISlideOutlining
{
    public OutlineSlide? Summary(IEnumerable<Slide> slides)
    {
        ArgumentNullException.ThrowIfNull(slides);
        var titles = slides
            .Where(slide => !slide.Hidden)
            .Select(slide => slide.Placeholder(PlaceholderRole.Title)?.Text.PlainText.Trim())
            .Where(title => !string.IsNullOrEmpty(title))
            .Select(title => new TextParagraph(title!) { Bullet = true })
            .ToList();

        return titles.Count == 0 ? null : new OutlineSlide(string.Empty, titles);
    }

    public IReadOnlyList<OutlineSlide> Expand(Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        if (slide.Placeholder(PlaceholderRole.Body) is not { } body) return [];

        var result = new List<OutlineSlide>();
        string? title = null;
        var children = new List<TextParagraph>();

        foreach (var paragraph in body.Text.Paragraphs.Where(p => p.Text.Trim().Length > 0))
        {
            if (paragraph.BulletLevel == 0)
            {
                Flush();
                title = paragraph.Text.Trim();
            }
            else if (title is not null)
            {
                children.Add(paragraph with { BulletLevel = paragraph.BulletLevel - 1 });
            }
        }

        Flush();
        return result;

        void Flush()
        {
            if (title is not null) result.Add(new OutlineSlide(title, [.. children]));
            children.Clear();
        }
    }
}

public sealed record AlbumPage(IReadOnlyList<PulseOffice.Foundation.Units.DocumentRect> Frames);

public sealed class PhotoAlbum
{
    public IReadOnlyList<AlbumPage> Pages(SlideSize size, int photos, int perSlide)
    {
        perSlide = Math.Clamp(perSlide, 1, 4);
        var columns = perSlide == 1 ? 1 : 2;
        var rows = (perSlide + columns - 1) / columns;
        var margin = PulseOffice.Foundation.Units.Twip.FromCentimeters(1).Value;
        var top = PulseOffice.Foundation.Units.Twip.FromCentimeters(2.2).Value;
        var cellWidth = Math.Max(1, (size.Width.Value - margin * (columns + 1)) / columns);
        var cellHeight = Math.Max(1, (size.Height.Value - top - margin * (rows + 1)) / rows);

        var pages = new List<AlbumPage>();
        for (var start = 0; start < photos; start += perSlide)
        {
            var frames = new List<PulseOffice.Foundation.Units.DocumentRect>();
            for (var slot = 0; slot < perSlide && start + slot < photos; slot++)
            {
                var column = slot % columns;
                var row = slot / columns;
                frames.Add(new PulseOffice.Foundation.Units.DocumentRect(
                    new(margin + column * (cellWidth + margin)), new(top + margin + row * (cellHeight + margin)),
                    new(cellWidth), new(cellHeight)));
            }
            pages.Add(new AlbumPage(frames));
        }
        return pages;
    }
}
