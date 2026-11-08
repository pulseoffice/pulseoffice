using System.Text;

namespace PulseOffice.Text.Scripts;

public interface IKanaConversion
{
    string ToHiragana(string text);

    string ToKatakana(string text);
}

public sealed class KanaConversion : IKanaConversion
{
    private const char HiraganaFirst = 'ぁ';
    private const char HiraganaLast = 'ゖ';
    private const char KatakanaFirst = 'ァ';
    private const char KatakanaLast = 'ヶ';
    private const int Distance = KatakanaFirst - HiraganaFirst;

    private const char HiraganaIterationFirst = '\u309D';
    private const char HiraganaIterationLast = '\u309E';
    private const char KatakanaIterationFirst = '\u30FD';
    private const char KatakanaIterationLast = '\u30FE';

    public string ToHiragana(string text) =>
        Map(text, current => (current >= KatakanaFirst && current <= KatakanaLast)
            || (current >= KatakanaIterationFirst && current <= KatakanaIterationLast)
            ? (char)(current - Distance)
            : current);

    public string ToKatakana(string text) =>
        Map(text, current => (current >= HiraganaFirst && current <= HiraganaLast)
            || (current >= HiraganaIterationFirst && current <= HiraganaIterationLast)
            ? (char)(current + Distance)
            : current);

    private static string Map(string text, Func<char, char> convert)
    {
        ArgumentNullException.ThrowIfNull(text);

        var result = new StringBuilder(text.Length);

        foreach (var current in text) result.Append(convert(current));

        return result.ToString();
    }
}
