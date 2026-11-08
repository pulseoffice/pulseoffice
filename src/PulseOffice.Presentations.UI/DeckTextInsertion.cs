using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public interface IDeckTextInsertion
{
    bool CanInsert(DeckTarget target);

    void Insert(DeckTarget target, TextRun run, double freeFontSize);

    void Insert(DeckTarget target, IReadOnlyList<TextParagraph> paragraphs, double freeFontSize);
}

public sealed class DeckTextInsertion(IRichTextInsertion rich) : IDeckTextInsertion
{
    public bool CanInsert(DeckTarget target) => target.CurrentSlide() is not null;

    public void Insert(DeckTarget target, TextRun run, double freeFontSize)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(run);
        if (target.CurrentSlide() is not { } slide) return;

        if (run.Field == TextFieldKind.None && target.TypeAtCaret(run.Text)) return;

        if (target.Selected() is TextElement selected)
        {
            target.Actions.AppendRun(slide, selected.Id, run);
            return;
        }

        var width = Twip.FromCentimeters(6);
        var height = Twip.FromCentimeters(1.6);

        target.Actions.InsertElement(slide, new TextElement
        {
            Id = Guid.NewGuid().ToString("N"),
            Bounds = new DocumentRect(
                new Twip((target.Deck.Size.Width.Value - width.Value) / 2),
                new Twip((target.Deck.Size.Height.Value - height.Value) / 2),
                width, height),
            ZOrder = slide.Elements.Count,
            Text = new RichText(PlaceholderRole.Free,
                [new TextParagraph(run.Text) { FontSize = freeFontSize, Align = TextAlign.Center, Runs = [run] }]),
        });
    }

    public void Insert(DeckTarget target, IReadOnlyList<TextParagraph> paragraphs, double freeFontSize)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(paragraphs);
        if (paragraphs.Count == 0 || target.CurrentSlide() is not { } slide) return;

        if (target.IsTyping() && target.InsertParagraphsAtCaret(paragraphs)) return;

        if (target.Selected() is TextElement selected)
        {
            target.Actions.EditText(slide, selected.Id, rich.Insert(selected.Text, null, paragraphs));
            return;
        }

        var width = Twip.FromCentimeters(12);
        var height = Twip.FromCentimeters(Math.Min(14, 1.0 + 0.8 * paragraphs.Count));

        target.Actions.InsertElement(slide, new TextElement
        {
            Id = Guid.NewGuid().ToString("N"),
            Bounds = new DocumentRect(
                new Twip((target.Deck.Size.Width.Value - width.Value) / 2),
                new Twip((target.Deck.Size.Height.Value - height.Value) / 2),
                width, height),
            ZOrder = slide.Elements.Count,
            Text = new RichText(PlaceholderRole.Free, [.. paragraphs.Select(paragraph => paragraph with { FontSize = freeFontSize })]),
        });
    }
}
