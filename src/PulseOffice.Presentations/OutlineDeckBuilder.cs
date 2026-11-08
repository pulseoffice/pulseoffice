using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations;

public sealed record OutlineLine(int Level, string Text);

public interface IOutlineDeckBuilder
{
    void Fill(Presentation deck, IReadOnlyList<OutlineLine> lines);
}

public sealed class OutlineDeckBuilder(ISlideFactory slides) : IOutlineDeckBuilder
{
    public void Fill(Presentation deck, IReadOnlyList<OutlineLine> lines)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(lines);
        var top = lines.Count == 0 ? 1 : lines.Min(line => line.Level);
        Slide? current = null;
        var body = new List<TextParagraph>();
        foreach (var line in lines.Where(line => line.Text.Trim().Length > 0))
        {
            if (line.Level == top || current is null)
            {
                Flush(current, body);
                current = slides.Create(deck.NextId(), deck.Master, SlideLayout.TitleAndContent);
                deck.AddExisting(current);
                Set(current, PlaceholderRole.Title, [new TextParagraph(line.Text.Trim())]);
                body = [];
                continue;
            }
            body.Add(new TextParagraph(line.Text.Trim()) { BulletLevel = Math.Clamp(line.Level - top - 1, 0, 8) });
        }
        Flush(current, body);
    }

    private void Flush(Slide? slide, List<TextParagraph> body)
    {
        if (slide is not null && body.Count > 0) Set(slide, PlaceholderRole.Body, body);
    }

    private void Set(Slide slide, PlaceholderRole role, IReadOnlyList<TextParagraph> paragraphs)
    {
        if (slide.Placeholder(role) is not { } placeholder) return;
        slide.Replace(placeholder with { Text = placeholder.Text with { Paragraphs = [.. paragraphs] } });
    }
}
