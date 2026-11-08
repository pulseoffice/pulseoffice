using System.Text;

namespace PulseOffice.Text.Scripts;

public interface IWidthConversion
{
    string ToFullWidth(string text);

    string ToHalfWidth(string text);
}

public sealed class WidthConversion : IWidthConversion
{
    private const char AsciiFirst = '!';
    private const char AsciiLast = '~';
    private const char FullWidthFirst = '！';
    private const int AsciiToFullWidth = FullWidthFirst - AsciiFirst;

    private static readonly Dictionary<char, string> HalfKanaToFull = Build();

    private static readonly Dictionary<char, string> FullKanaToHalf =
        HalfKanaToFull
            .GroupBy(pair => pair.Value[0])
            .ToDictionary(group => group.Key, group => group.First().Key.ToString());

    private static readonly Dictionary<string, char> VoicedHalfToFull = Voiced();

    public string ToFullWidth(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var result = new StringBuilder(text.Length);

        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];

            if (index + 1 < text.Length
                && VoicedHalfToFull.TryGetValue(text.Substring(index, 2), out var combined))
            {
                result.Append(combined);
                index++;
                continue;
            }

            if (current >= AsciiFirst && current <= AsciiLast)
            {
                result.Append((char)(current + AsciiToFullWidth));
                continue;
            }

            if (current == ' ')
            {
                result.Append('　');
                continue;
            }

            result.Append(HalfKanaToFull.TryGetValue(current, out var full) ? full : current.ToString());
        }

        return result.ToString();
    }

    public string ToHalfWidth(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var result = new StringBuilder(text.Length);

        foreach (var current in text)
        {
            if (current >= FullWidthFirst && current <= '～')
            {
                result.Append((char)(current - AsciiToFullWidth));
                continue;
            }

            if (current == '　')
            {
                result.Append(' ');
                continue;
            }

            if (Decompose(current) is { } decomposed)
            {
                result.Append(decomposed);
                continue;
            }

            result.Append(FullKanaToHalf.TryGetValue(current, out var half) ? half : current.ToString());
        }

        return result.ToString();
    }

    private static string? Decompose(char full)
    {
        foreach (var (half, combined) in VoicedHalfToFull)
            if (combined == full)
                return half;

        return null;
    }

    private static Dictionary<char, string> Build()
    {
        const string half = "｡｢｣､･ｦｧｨｩｪｫｬｭｮｯｰｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾅﾆﾇﾈﾉﾊﾋﾌﾍﾎﾏﾐﾑﾒﾓﾔﾕﾖﾗﾘﾙﾚﾛﾜﾝﾞﾟ";
        const string full = "。「」、・ヲァィゥェォャュョッーアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワン゛゜";

        var map = new Dictionary<char, string>(half.Length);

        for (var index = 0; index < half.Length; index++)
            map[half[index]] = full[index].ToString();

        return map;
    }

    private static Dictionary<string, char> Voiced()
    {
        const string bases = "ｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾊﾋﾌﾍﾎｳ";
        const string voiced = "ガギグゲゴザジズゼゾダヂヅデドバビブベボヴ";
        const string plosiveBases = "ﾊﾋﾌﾍﾎ";
        const string plosive = "パピプペポ";

        var map = new Dictionary<string, char>(StringComparer.Ordinal);

        for (var index = 0; index < bases.Length; index++)
            map[bases[index] + "ﾞ"] = voiced[index];

        for (var index = 0; index < plosiveBases.Length; index++)
            map[plosiveBases[index] + "ﾟ"] = plosive[index];

        return map;
    }
}
