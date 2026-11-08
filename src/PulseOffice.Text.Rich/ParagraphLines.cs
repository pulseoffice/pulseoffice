namespace PulseOffice.Text.Rich;

public readonly record struct BrokenLine(int Start, int End, int Next, bool Last);

public readonly record struct SpreadPiece(int Start, int End, double X);

public interface IParagraphLineBreaker
{
    IReadOnlyList<BrokenLine> Break(string content, double firstWidth, double otherWidth, Func<int, int, double> width);
}

public interface ILineJustifier
{
    IReadOnlyList<SpreadPiece> Spread(string content, BrokenLine line, double available, Func<int, int, double> width);
}

public sealed class ParagraphLineBreaker(IFirstLineBreaker breaker) : IParagraphLineBreaker
{
    public ParagraphLineBreaker()
        : this(new FirstLineBreaker())
    {
    }

    public IReadOnlyList<BrokenLine> Break(string content, double firstWidth, double otherWidth, Func<int, int, double> width)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(width);

        if (content.Length == 0) return [new BrokenLine(0, 0, 0, true)];

        var lines = new List<BrokenLine>();
        var start = 0;
        while (start < content.Length)
        {
            var available = lines.Count == 0 ? firstWidth : otherWidth;
            var from = start;
            var split = breaker.Break(content, from, available, end => width(from, from + end));
            var end = from + split.End;
            var next = Math.Max(from + split.Next, end == from ? from + 1 : end);
            var hard = end < content.Length && IsHardBreak(content[end]);
            lines.Add(new BrokenLine(from, end, next, next >= content.Length || hard));
            start = next;
        }
        return lines;
    }

    private bool IsHardBreak(char character) => character is '\n' or '\r' or '\v' or '\u2028';
}

public sealed class LineJustifier : ILineJustifier
{
    public IReadOnlyList<SpreadPiece> Spread(string content, BrokenLine line, double available, Func<int, int, double> width)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(width);

        var starts = new List<int> { line.Start };
        for (var index = line.Start + 1; index < line.End; index++)
            if (char.IsWhiteSpace(content[index - 1]) && !char.IsWhiteSpace(content[index])) starts.Add(index);

        var natural = width(line.Start, line.End);
        var extra = line.Last || starts.Count < 2 || natural >= available ? 0 : (available - natural) / (starts.Count - 1);

        var pieces = new List<SpreadPiece>(starts.Count);
        for (var index = 0; index < starts.Count; index++)
        {
            var end = index + 1 < starts.Count ? starts[index + 1] : line.End;
            var x = index == 0 ? 0 : width(line.Start, starts[index]) + extra * index;
            pieces.Add(new SpreadPiece(starts[index], end, x));
        }
        return pieces;
    }
}

public readonly record struct TextPosition(int Paragraph, int Offset);

public interface IPlainTextPositions
{
    TextPosition Locate(string text, int index);

    int Index(string text, TextPosition position);

    int ParagraphLength(string text, int paragraph);
}

public sealed class PlainTextPositions : IPlainTextPositions
{
    public TextPosition Locate(string text, int index)
    {
        ArgumentNullException.ThrowIfNull(text);
        index = Math.Clamp(index, 0, text.Length);

        var paragraph = 0;
        var start = 0;
        var position = 0;
        while (position < index)
        {
            var separator = Separator(text, position);
            if (separator == 0)
            {
                position++;
                continue;
            }
            if (position + separator > index) return new TextPosition(paragraph, position - start);
            position += separator;
            paragraph++;
            start = position;
        }
        return new TextPosition(paragraph, index - start);
    }

    public int Index(string text, TextPosition position)
    {
        ArgumentNullException.ThrowIfNull(text);

        var paragraph = 0;
        var start = 0;
        var cursor = 0;
        while (paragraph < position.Paragraph && cursor < text.Length)
        {
            var separator = Separator(text, cursor);
            if (separator == 0)
            {
                cursor++;
                continue;
            }
            cursor += separator;
            paragraph++;
            start = cursor;
        }

        var length = ParagraphLengthFrom(text, start);
        var index = start + Math.Clamp(position.Offset, 0, length);
        return index > start && index < text.Length
            && char.IsHighSurrogate(text[index - 1]) && char.IsLowSurrogate(text[index]) ? index - 1 : index;
    }

    public int ParagraphLength(string text, int paragraph)
    {
        ArgumentNullException.ThrowIfNull(text);
        return ParagraphLengthFrom(text, Index(text, new TextPosition(paragraph, 0)));
    }

    private int ParagraphLengthFrom(string text, int start)
    {
        var end = start;
        while (end < text.Length && Separator(text, end) == 0) end++;
        return end - start;
    }

    private int Separator(string text, int position) => text[position] switch
    {
        '\r' when position + 1 < text.Length && text[position + 1] == '\n' => 2,
        '\r' or '\n' => 1,
        _ => 0,
    };
}
