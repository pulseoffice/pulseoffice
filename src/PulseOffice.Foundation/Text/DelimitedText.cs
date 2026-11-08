using System.Text;

namespace PulseOffice.Foundation.Text;

public sealed record DelimitedFormat
{
    public char Separator { get; init; } = ';';

    public char? Quote { get; init; } = '"';

    public bool MergeSeparators { get; init; }

    public string OtherSeparators { get; init; } = string.Empty;

    public bool IsSeparator(char character) => character == Separator || OtherSeparators.Contains(character, StringComparison.Ordinal);

    public static readonly DelimitedFormat Comma = new() { Separator = ',' };
    public static readonly DelimitedFormat Semicolon = new();
    public static readonly DelimitedFormat Tab = new() { Separator = '\t' };
}

public interface IDelimitedText
{
    IReadOnlyList<string> SplitLine(string line, DelimitedFormat format);

    IEnumerable<IReadOnlyList<string>> Read(TextReader reader, DelimitedFormat format);

    string JoinLine(IEnumerable<string> fields, DelimitedFormat format);

    char? GuessSeparator(string sample);
}

public sealed class DelimitedText : IDelimitedText
{
    private static readonly char[] Candidates = [';', ',', '\t', '|'];

    public IReadOnlyList<string> SplitLine(string line, DelimitedFormat format)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(format);

        using var reader = new StringReader(line);
        return Read(reader, format).FirstOrDefault() ?? [string.Empty];
    }

    public IEnumerable<IReadOnlyList<string>> Read(TextReader reader, DelimitedFormat format)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(format);

        return new DelimitedScanner(reader, format).Rows();
    }

    public string JoinLine(IEnumerable<string> fields, DelimitedFormat format)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(format);

        return string.Join(format.Separator, fields.Select(field => Escape(field, format)));
    }

    public char? GuessSeparator(string sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        var lines = sample.Split('\n').Where(text => text.Trim().Length > 0).Take(SampledLines).ToList();
        if (lines.Count > 1 && !sample.EndsWith('\n')) lines.RemoveAt(lines.Count - 1);
        if (lines.Count == 0) return null;

        var best = Candidates
            .Select(candidate => Score(lines, candidate))
            .Where(score => score.Lines > 0)
            .OrderByDescending(score => score.Lines)
            .ThenByDescending(score => score.Count)
            .ToList();

        return best.Count == 0 ? null : best[0].Candidate;
    }

    private const int SampledLines = 20;

    private (char Candidate, int Lines, int Count) Score(IReadOnlyList<string> lines, char candidate)
    {
        var counts = lines.Select(line => Outside(line, candidate)).Where(count => count > 0).ToList();
        if (counts.Count == 0) return (candidate, 0, 0);
        var usual = counts.GroupBy(count => count).OrderByDescending(group => group.Count()).ThenByDescending(group => group.Key).First();
        return (candidate, usual.Count(), usual.Key);
    }

    private static int Outside(string line, char candidate)
    {
        var count = 0;
        var quoted = false;

        var fieldStart = true;

        foreach (var character in line)
        {
            if (character == '"' && (quoted || fieldStart)) quoted = !quoted;
            else if (!quoted && character == candidate)
            {
                count++;
                fieldStart = true;
                continue;
            }

            fieldStart = false;
        }

        return count;
    }

    private static string Escape(string field, DelimitedFormat format)
    {
        if (format.Quote is not { } quote) return field;

        var needs = field.Contains(format.Separator, StringComparison.Ordinal)
            || field.Contains(quote)
            || field.Contains('\n', StringComparison.Ordinal)
            || field.Contains('\r', StringComparison.Ordinal);

        if (!needs) return field;

        return quote + field.Replace(quote.ToString(), new string(quote, 2), StringComparison.Ordinal) + quote;
    }
}

internal sealed class LookaheadReader(TextReader reader)
{
    private const int Chunk = 64 * 1024;

    private char[] _buffer = new char[Chunk];
    private int _start;
    private int _end;
    private bool _exhausted;

    public bool Has(int offset) => Fill(offset);

    public char this[int offset] => Fill(offset) ? _buffer[_start + offset] : '\0';

    public void Advance(int count) => _start = Math.Min(_start + count, _end);

    private bool Fill(int offset)
    {
        while (_start + offset >= _end)
        {
            if (_exhausted) return false;
            if (_start > 0)
            {
                Array.Copy(_buffer, _start, _buffer, 0, _end - _start);
                _end -= _start;
                _start = 0;
            }
            if (_end == _buffer.Length) Array.Resize(ref _buffer, _buffer.Length * 2);
            var read = reader.Read(_buffer, _end, _buffer.Length - _end);
            if (read == 0) _exhausted = true;
            _end += read;
        }

        return true;
    }
}

internal sealed class DelimitedScanner(TextReader reader, DelimitedFormat format)
{
    private readonly LookaheadReader _text = new(reader);

    public IEnumerable<IReadOnlyList<string>> Rows()
    {
        while (_text.Has(0))
        {
            if (IsLineEnd(0))
            {
                SkipLineEnd();
                yield return [string.Empty];
                continue;
            }

            yield return Row();
        }
    }

    private IReadOnlyList<string> Row()
    {
        var fields = new List<string>();
        while (true)
        {
            var field = Field();
            if (!(format.MergeSeparators && field.Length == 0 && fields.Count > 0 && _text.Has(0) && format.IsSeparator(_text[0])))
                fields.Add(field);

            if (!_text.Has(0)) return fields;
            if (IsLineEnd(0))
            {
                SkipLineEnd();
                return fields;
            }

            _text.Advance(1);
        }
    }

    private string Field()
    {
        if (format.Quote is { } quote && QuotedStart(quote) is { } start)
        {
            _text.Advance(start + 1);
            return Quoted(quote);
        }

        var value = new StringBuilder();
        while (_text.Has(0) && !format.IsSeparator(_text[0]) && !IsLineEnd(0))
        {
            value.Append(_text[0]);
            _text.Advance(1);
        }
        return value.ToString();
    }

    private int? QuotedStart(char quote)
    {
        var index = 0;
        if (!format.IsSeparator(' '))
            while (_text.Has(index) && _text[index] == ' ') index++;
        return _text.Has(index) && _text[index] == quote ? index : null;
    }

    private string Quoted(char quote)
    {
        var value = new StringBuilder();
        while (_text.Has(0))
        {
            var character = _text[0];
            if (character == quote)
            {
                if (_text.Has(1) && _text[1] == quote)
                {
                    value.Append(quote);
                    _text.Advance(2);
                    continue;
                }

                var after = 1;
                if (!format.IsSeparator(' '))
                    while (_text.Has(after) && _text[after] == ' ') after++;
                if (!_text.Has(after) || format.IsSeparator(_text[after]) || IsLineEnd(after))
                {
                    _text.Advance(after);
                    return value.ToString();
                }

                value.Append(quote);
                _text.Advance(1);
                continue;
            }

            if (character == '\r')
            {
                value.Append('\n');
                _text.Advance(_text.Has(1) && _text[1] == '\n' ? 2 : 1);
                continue;
            }

            value.Append(character);
            _text.Advance(1);
        }

        return value.ToString();
    }

    private bool IsLineEnd(int offset) => _text[offset] is '\r' or '\n';

    private void SkipLineEnd()
    {
        _text.Advance(_text[0] == '\r' && _text.Has(1) && _text[1] == '\n' ? 2 : 1);
    }
}
