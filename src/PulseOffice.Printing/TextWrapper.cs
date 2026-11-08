using PulseOffice.Foundation.Units;

namespace PulseOffice.Printing;

public interface ITextWrapper
{
    IReadOnlyList<string> Wrap(string text, Twip width, Func<string, Twip> measure);
}

public sealed class TextWrapper : ITextWrapper
{
    public IReadOnlyList<string> Wrap(string text, Twip width, Func<string, Twip> measure)
    {
        ArgumentNullException.ThrowIfNull(measure);

        var lines = new List<string>();
        if (string.IsNullOrEmpty(text)) return lines;

        foreach (var paragraph in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n', '\r'))
            WrapParagraph(paragraph, width, measure, lines);

        return lines;
    }

    private void WrapParagraph(string paragraph, Twip width, Func<string, Twip> measure, List<string> lines)
    {
        var current = string.Empty;

        foreach (var word in paragraph.Split(' '))
        {
            var candidate = current.Length == 0 ? word : current + " " + word;

            if (current.Length == 0 || measure(candidate).Value <= width.Value)
            {
                current = candidate;
                continue;
            }

            current = Emit(current, width, measure, lines);
            lines.Add(current);
            current = word;
        }

        current = Emit(current, width, measure, lines);
        lines.Add(current);
    }

    private string Emit(string text, Twip width, Func<string, Twip> measure, List<string> lines)
    {
        while (text.Length > 1 && measure(text).Value > width.Value)
        {
            var cut = Fitting(text, width, measure);
            if (cut >= text.Length) break;
            lines.Add(text[..cut]);
            text = text[cut..];
        }

        return text;
    }

    private int Fitting(string text, Twip width, Func<string, Twip> measure)
    {
        var cut = 1;

        while (cut < text.Length && measure(text[..(cut + 1)]).Value <= width.Value) cut++;

        if (cut < text.Length && char.IsLowSurrogate(text[cut])) cut = cut > 1 ? cut - 1 : Math.Min(2, text.Length);

        return cut;
    }
}
