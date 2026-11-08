namespace PulseOffice.Formats.Interchange.Text.Rtf;

public enum RtfTokenKind
{
    GroupStart,
    GroupEnd,
    ControlWord,
    ControlSymbol,
    Text,
    HexByte,
    Binary,
}

public readonly record struct RtfToken(RtfTokenKind Kind, string Word = "", int? Parameter = null, byte[]? Bytes = null, char Symbol = '\0');

public interface IRtfTokenizer
{
    IEnumerable<RtfToken> Tokenize(byte[] data);
}

public sealed class RtfTokenizer : IRtfTokenizer
{
    private const int MaximumWord = 32;
    private const int MaximumBinary = 64 * 1024 * 1024;

    public IEnumerable<RtfToken> Tokenize(byte[] data)
    {
        var position = 0;
        var text = new List<byte>();
        while (position < data.Length)
        {
            var current = data[position];
            if (current is (byte)'{' or (byte)'}' or (byte)'\\' && text.Count > 0)
            {
                yield return new RtfToken(RtfTokenKind.Text, Bytes: [.. text]);
                text.Clear();
            }

            switch (current)
            {
                case (byte)'{':
                    position++;
                    yield return new RtfToken(RtfTokenKind.GroupStart);
                    continue;
                case (byte)'}':
                    position++;
                    yield return new RtfToken(RtfTokenKind.GroupEnd);
                    continue;
                case (byte)'\\':
                    var token = Control(data, ref position);
                    if (token is { } found)
                    {
                        yield return found;
                        if (found is { Kind: RtfTokenKind.ControlWord, Word: "bin", Parameter: > 0 })
                        {
                            var length = Math.Min(found.Parameter.Value, Math.Min(MaximumBinary, data.Length - position));
                            yield return new RtfToken(RtfTokenKind.Binary, Bytes: data[position..(position + length)]);
                            position += length;
                        }
                    }
                    continue;
                case (byte)'\r' or (byte)'\n' or 0:
                    position++;
                    continue;
                default:
                    text.Add(current);
                    position++;
                    continue;
            }
        }

        if (text.Count > 0) yield return new RtfToken(RtfTokenKind.Text, Bytes: [.. text]);
    }

    private RtfToken? Control(byte[] data, ref int position)
    {
        position++;
        if (position >= data.Length) return null;
        var first = data[position];
        if (!IsLetter(first))
        {
            position++;
            if (first == (byte)'\'')
            {
                if (position + 1 < data.Length && Hex(data[position]) is { } high && Hex(data[position + 1]) is { } low)
                {
                    position += 2;
                    return new RtfToken(RtfTokenKind.HexByte, Bytes: [(byte)(high * 16 + low)]);
                }
                return null;
            }
            if (first is (byte)'\r' or (byte)'\n') return new RtfToken(RtfTokenKind.ControlWord, "par");
            return new RtfToken(RtfTokenKind.ControlSymbol, Symbol: (char)first);
        }

        var start = position;
        while (position < data.Length && IsLetter(data[position]) && position - start < MaximumWord) position++;
        while (position < data.Length && IsLetter(data[position])) position++;
        var word = System.Text.Encoding.ASCII.GetString(data, start, Math.Min(position - start, MaximumWord));
        int? parameter = null;
        var negative = position < data.Length && data[position] == (byte)'-';
        if (negative) position++;
        if (position < data.Length && data[position] is >= (byte)'0' and <= (byte)'9')
        {
            long value = 0;
            while (position < data.Length && data[position] is >= (byte)'0' and <= (byte)'9')
            {
                if (value < int.MaxValue) value = value * 10 + (data[position] - '0');
                position++;
            }
            parameter = (int)Math.Clamp(negative ? -value : value, int.MinValue, int.MaxValue);
        }
        if (position < data.Length && data[position] == (byte)' ') position++;
        return new RtfToken(RtfTokenKind.ControlWord, word, parameter);
    }

    private bool IsLetter(byte value) => value is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z';

    private int? Hex(byte value) => value switch
    {
        >= (byte)'0' and <= (byte)'9' => value - '0',
        >= (byte)'a' and <= (byte)'f' => value - 'a' + 10,
        >= (byte)'A' and <= (byte)'F' => value - 'A' + 10,
        _ => null,
    };
}
