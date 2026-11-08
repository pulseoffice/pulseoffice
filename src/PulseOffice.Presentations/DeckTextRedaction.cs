using PulseOffice.Text.Redaction;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations;

public interface IDeckTextRedaction
{
    RichText MaskAll(RichText text);

    RichText Redact(RichText text, IReadOnlyList<RedactionPattern> patterns);

    string RedactNotes(string notes, IReadOnlyList<RedactionPattern> patterns);
}

public sealed class DeckTextRedaction(ITextRedactor redactor) : IDeckTextRedaction
{
    public RichText MaskAll(RichText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Rewrite(text, paragraph => paragraph.Text.Length == 0 ? paragraph.Text : redactor.Mask);
    }

    public RichText Redact(RichText text, IReadOnlyList<RedactionPattern> patterns)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Rewrite(text, paragraph => redactor.Redact(paragraph.Text, patterns));
    }

    public string RedactNotes(string notes, IReadOnlyList<RedactionPattern> patterns) =>
        redactor.Redact(notes ?? string.Empty, patterns);

    private RichText Rewrite(RichText text, Func<TextParagraph, string> replacement) =>
        text with
        {
            Paragraphs = [.. text.Paragraphs.Select(paragraph =>
            {
                var after = replacement(paragraph);
                return after == paragraph.Text
                    ? paragraph
                    : paragraph.WithRuns([paragraph.EffectiveRuns()[0] with { Text = after, Field = TextFieldKind.None }]);
            })],
        };
}
