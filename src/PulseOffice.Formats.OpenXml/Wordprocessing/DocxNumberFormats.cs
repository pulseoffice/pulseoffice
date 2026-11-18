using PulseOffice.Documents.Styles;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxNumberFormats
{
    private readonly IReadOnlyDictionary<NumberFormat, W.NumberFormatValues> _formats = new Dictionary<NumberFormat, W.NumberFormatValues>
    {
        [NumberFormat.None] = W.NumberFormatValues.None,
        [NumberFormat.Bullet] = W.NumberFormatValues.Bullet,
        [NumberFormat.Arabic] = W.NumberFormatValues.Decimal,
        [NumberFormat.LowerLetter] = W.NumberFormatValues.LowerLetter,
        [NumberFormat.UpperLetter] = W.NumberFormatValues.UpperLetter,
        [NumberFormat.LowerRoman] = W.NumberFormatValues.LowerRoman,
        [NumberFormat.UpperRoman] = W.NumberFormatValues.UpperRoman,
        [NumberFormat.DecimalZero] = W.NumberFormatValues.DecimalZero,
        [NumberFormat.Ordinal] = W.NumberFormatValues.Ordinal,
        [NumberFormat.OrdinalText] = W.NumberFormatValues.OrdinalText,
        [NumberFormat.CardinalText] = W.NumberFormatValues.CardinalText,
        [NumberFormat.Hebrew] = W.NumberFormatValues.Hebrew1,
        [NumberFormat.ArabicAbjad] = W.NumberFormatValues.ArabicAbjad,
        [NumberFormat.ArabicAlpha] = W.NumberFormatValues.ArabicAlpha,
        [NumberFormat.Katakana] = W.NumberFormatValues.AiueoFullWidth,
        [NumberFormat.KatakanaIroha] = W.NumberFormatValues.IrohaFullWidth,
        [NumberFormat.IdeographDigital] = W.NumberFormatValues.IdeographDigital,
        [NumberFormat.ChineseCounting] = W.NumberFormatValues.ChineseCounting,
        [NumberFormat.ThaiLetters] = W.NumberFormatValues.ThaiLetters,
        [NumberFormat.RussianLower] = W.NumberFormatValues.RussianLower,
        [NumberFormat.RussianUpper] = W.NumberFormatValues.RussianUpper,
    };

    public bool TryWord(NumberFormat format, out W.NumberFormatValues value) => _formats.TryGetValue(format, out value);

    public bool TryWordPage(NumberFormat format, out W.NumberFormatValues value) =>
        TryWord(format == NumberFormat.Hiragana ? NumberFormat.Katakana : format, out value);

    public NumberFormat? Model(W.NumberFormatValues value)
    {
        foreach (var (format, known) in _formats)
            if (known == value) return format;
        return null;
    }
}
