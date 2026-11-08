using System.Text;
using PulseOffice.Text.Spelling;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations;

public interface ISlideTextHyphenation
{
    RichText Hyphenate(RichText text, IHyphenator hyphenator);
}

public sealed class SlideTextHyphenation(IWordTokenizer tokenizer) : ISlideTextHyphenation
{
    private const char SoftHyphen = '­';
    private const char FieldBoundary = '￼';

    public RichText Hyphenate(RichText text, IHyphenator hyphenator) => text with
    {
        Paragraphs = [.. text.Paragraphs.Select(paragraph => Hyphenate(paragraph, hyphenator))],
    };

    private TextParagraph Hyphenate(TextParagraph paragraph, IHyphenator hyphenator)
    {
        var runs = paragraph.Runs.Count > 0 ? paragraph.Runs : [new TextRun(paragraph.Text)];
        var cleaned = runs
            .Select(run => run.Field == TextFieldKind.None ? run with { Text = run.Text.Replace(SoftHyphen.ToString(), string.Empty, StringComparison.Ordinal) } : run)
            .ToList();

        var joined = string.Concat(cleaned.Select(run => run.Field == TextFieldKind.None ? run.Text : new string(FieldBoundary, run.Text.Length)));
        var breaks = new HashSet<int>();
        foreach (var span in tokenizer.Words(joined))
        {
            if (span.Text.Contains(FieldBoundary)) continue;
            foreach (var position in hyphenator.Positions(span.Text))
                if (position > 0 && position < span.Text.Length) breaks.Add(span.Start + position);
        }

        var result = new List<TextRun>(cleaned.Count);
        var offset = 0;
        foreach (var run in cleaned)
        {
            var length = run.Text.Length;
            if (run.Field != TextFieldKind.None || length == 0)
            {
                result.Add(run);
                offset += length;
                continue;
            }

            var builder = new StringBuilder(length + 4);
            for (var i = 0; i < length; i++)
            {
                if (breaks.Contains(offset + i)) builder.Append(SoftHyphen);
                builder.Append(run.Text[i]);
            }
            result.Add(run with { Text = builder.ToString() });
            offset += length;
        }

        return paragraph.Runs.Count > 0
            ? paragraph.WithRuns(result)
            : paragraph with { Text = result[0].Text };
    }
}
