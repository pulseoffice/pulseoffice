using System.Text;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public sealed class JapaneseWidth
{
    private const char FullWidthStart = '！';
    private const char FullWidthEnd = '～';
    private const int FullWidthOffset = 0xFEE0;

    private const string HalfKatakana = "｡｢｣､･ｦｧｨｩｪｫｬｭｮｯｰｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾅﾆﾇﾈﾉﾊﾋﾌﾍﾎﾏﾐﾑﾒﾓﾔﾕﾖﾗﾘﾙﾚﾛﾜﾝﾞﾟ";
    private const string FullKatakana = "。「」、・ヲァィゥェォャュョッーアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワン゙゚";

    private readonly Dictionary<char, char> _toFull = new()
    {
        ['"'] = '”', ['\''] = '’', ['\\'] = '￥', ['`'] = '‘', [' '] = '　',
    };

    private readonly Dictionary<char, char> _toHalf;

    public JapaneseWidth()
    {
        for (var index = 0; index < HalfKatakana.Length; index++) _toFull[HalfKatakana[index]] = FullKatakana[index];
        _toHalf = _toFull.ToDictionary(pair => pair.Value, pair => pair.Key);
        _toHalf['゛'] = 'ﾞ';
        _toHalf['゜'] = 'ﾟ';
    }

    public string ToHalfWidth(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text.Normalize(NormalizationForm.FormD))
            builder.Append(_toHalf.TryGetValue(character, out var half) ? half
                : character is >= FullWidthStart and <= FullWidthEnd ? (char)(character - FullWidthOffset) : character);
        return builder.ToString();
    }

    public string ToFullWidth(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
            builder.Append(_toFull.TryGetValue(character, out var full) ? full
                : character is >= '!' and <= '~' ? (char)(character + FullWidthOffset) : character);
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
