using System.Globalization;
using System.Text;

namespace PulseOffice.Text.Rich;

public enum NumberSystem
{
    DecimalZero,
    Ordinal,
    OrdinalText,
    CardinalText,
    Hebrew,
    ArabicAbjad,
    ArabicAlpha,
    Hiragana,
    Katakana,
    KatakanaIroha,
    IdeographDigital,
    ChineseCounting,
    ThaiLetters,
    RussianLower,
    RussianUpper,
}

public interface INumberSystems
{
    string Render(NumberSystem system, int value);
}

public sealed class NumberSystems : INumberSystems
{
    private const int LongestRepetition = 30;
    private const int LargestSpelled = 999_999;
    private const int LargestCounted = 9_999;

    private readonly string[] _units = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
        "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"];

    private readonly string[] _tens = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    private readonly Dictionary<string, string> _ordinalWords = new(StringComparer.Ordinal)
    {
        ["one"] = "first", ["two"] = "second", ["three"] = "third", ["five"] = "fifth", ["eight"] = "eighth",
        ["nine"] = "ninth", ["twelve"] = "twelfth",
    };

    private readonly (int Value, string Letter)[] _hebrew =
    [
        (400, "ת"), (300, "ש"), (200, "ר"), (100, "ק"), (90, "צ"), (80, "פ"), (70, "ע"), (60, "ס"), (50, "נ"),
        (40, "מ"), (30, "ל"), (20, "כ"), (10, "י"), (9, "ט"), (8, "ח"), (7, "ז"), (6, "ו"), (5, "ה"), (4, "ד"),
        (3, "ג"), (2, "ב"), (1, "א"),
    ];

    private readonly IReadOnlyDictionary<NumberSystem, string> _alphabets = new Dictionary<NumberSystem, string>
    {
        [NumberSystem.ArabicAbjad] = "أبجدهوزحطيكلمنسعفصقرشتثخذضظغ",
        [NumberSystem.ArabicAlpha] = "أبتثجحخدذرزسشصضطظعغفقكلمنهوي",
        [NumberSystem.Hiragana] = "あいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをん",
        [NumberSystem.Katakana] = "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲン",
        [NumberSystem.KatakanaIroha] = "イロハニホヘトチリヌルヲワカヨタレソツネナラムウヰノオクヤマケフコエテアサキユメミシヱヒモセス",
        [NumberSystem.ThaiLetters] = "กขคงจฉชซฌญฎฏฐฑฒณดตถทธนบปผฝพฟภมยรลวศษสหฬอฮ",
        [NumberSystem.RussianLower] = "абвгдежзиклмнопрстуфхцчшщэюя",
        [NumberSystem.RussianUpper] = "АБВГДЕЖЗИКЛМНОПРСТУФХЦЧШЩЭЮЯ",
    };

    private readonly string _ideographs = "〇一二三四五六七八九";

    public string Render(NumberSystem system, int value) => system switch
    {
        NumberSystem.DecimalZero => value is >= 0 and < 10 ? "0" + value.ToString(CultureInfo.InvariantCulture) : value.ToString(CultureInfo.InvariantCulture),
        NumberSystem.Ordinal => value.ToString(CultureInfo.InvariantCulture) + OrdinalSuffix(value),
        NumberSystem.OrdinalText => Capitalized(OrdinalWords(value)),
        NumberSystem.CardinalText => Capitalized(Words(value)),
        NumberSystem.Hebrew => Hebrew(value),
        NumberSystem.IdeographDigital => Digital(value),
        NumberSystem.ChineseCounting => Counting(value),
        _ => Alphabetic(_alphabets[system], value),
    };

    private string OrdinalSuffix(int value)
    {
        var lastTwo = Math.Abs(value) % 100;
        if (lastTwo is >= 11 and <= 13) return "th";
        return (Math.Abs(value) % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
    }

    private string Words(int value)
    {
        if (value < 0 || value > LargestSpelled) return value.ToString(CultureInfo.InvariantCulture);
        if (value < 20) return _units[value];
        if (value < 100) return _tens[value / 10] + (value % 10 == 0 ? string.Empty : "-" + _units[value % 10]);
        if (value < 1000) return _units[value / 100] + " hundred" + (value % 100 == 0 ? string.Empty : " " + Words(value % 100));
        return Words(value / 1000) + " thousand" + (value % 1000 == 0 ? string.Empty : " " + Words(value % 1000));
    }

    private string OrdinalWords(int value)
    {
        var words = Words(value);
        if (value < 0 || value > LargestSpelled) return words + OrdinalSuffix(value);
        var cut = Math.Max(words.LastIndexOf(' '), words.LastIndexOf('-')) + 1;
        var last = words[cut..];
        var ordinal = _ordinalWords.TryGetValue(last, out var irregular) ? irregular
            : last.EndsWith('y') ? last[..^1] + "ieth"
            : last + "th";
        return words[..cut] + ordinal;
    }

    private string Capitalized(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private string Hebrew(int value)
    {
        if (value <= 0) return value.ToString(CultureInfo.InvariantCulture);
        var builder = new StringBuilder();
        var rest = value;
        while (rest > 0)
        {
            if (rest % 100 is 15 or 16 && rest < 100)
            {
                builder.Append('ט').Append(rest == 15 ? 'ו' : 'ז');
                break;
            }
            var (amount, letter) = _hebrew.First(entry => entry.Value <= rest);
            builder.Append(letter);
            rest -= amount;
        }
        return builder.ToString();
    }

    private string Digital(int value) =>
        value < 0 ? value.ToString(CultureInfo.InvariantCulture)
        : string.Concat(value.ToString(CultureInfo.InvariantCulture).Select(digit => _ideographs[digit - '0']));

    private string Counting(int value)
    {
        if (value <= 0 || value > LargestCounted) return Digital(value);
        var builder = new StringBuilder();
        var thousands = value / 1000;
        var hundreds = value / 100 % 10;
        var tens = value / 10 % 10;
        var units = value % 10;
        if (thousands > 0) builder.Append(_ideographs[thousands]).Append('千');
        if (hundreds > 0) builder.Append(_ideographs[hundreds]).Append('百');
        else if (thousands > 0 && (tens > 0 || units > 0)) builder.Append('零');
        if (tens > 0)
        {
            if (tens > 1 || value >= 100) builder.Append(_ideographs[tens]);
            builder.Append('十');
        }
        else if (value >= 100 && hundreds > 0 && units > 0) builder.Append('零');
        if (units > 0) builder.Append(_ideographs[units]);
        return builder.ToString();
    }

    private string Alphabetic(string alphabet, int value)
    {
        if (value <= 0) return string.Empty;
        var count = alphabet.Length;
        if ((value - 1) / count >= LongestRepetition) return value.ToString(CultureInfo.InvariantCulture);
        return new string(alphabet[(value - 1) % count], (value - 1) / count + 1);
    }
}
