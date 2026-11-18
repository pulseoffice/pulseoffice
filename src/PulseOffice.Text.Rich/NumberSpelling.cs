using System.Globalization;

namespace PulseOffice.Text.Rich;

public interface INumberSpeller
{
    string Language { get; }

    string Cardinal(int value);

    string Ordinal(int value);

    string OrdinalSuffix(int value);
}

public interface INumberSpellers
{
    INumberSpeller For(string? language);
}

public sealed class NumberSpellers(IEnumerable<INumberSpeller> spellers) : INumberSpellers
{
    private readonly IReadOnlyDictionary<string, INumberSpeller> _byLanguage = spellers.ToDictionary(speller => speller.Language, StringComparer.OrdinalIgnoreCase);

    public NumberSpellers()
        : this([new EnglishNumberSpeller(), new ItalianNumberSpeller(), new GermanNumberSpeller(), new SpanishNumberSpeller(), new FrenchNumberSpeller()])
    {
    }

    public INumberSpeller For(string? language)
    {
        var primary = language is { Length: > 0 } ? language.Replace('_', '-').Split('-')[0] : string.Empty;
        return _byLanguage.TryGetValue(primary, out var speller) ? speller : _byLanguage["en"];
    }
}

public sealed class EnglishNumberSpeller : INumberSpeller
{
    private const int LargestSpelled = 999_999;

    private readonly string[] _units = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
        "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"];

    private readonly string[] _tens = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    private readonly Dictionary<string, string> _ordinalWords = new(StringComparer.Ordinal)
    {
        ["one"] = "first", ["two"] = "second", ["three"] = "third", ["five"] = "fifth", ["eight"] = "eighth",
        ["nine"] = "ninth", ["twelve"] = "twelfth",
    };

    public string Language => "en";

    public string OrdinalSuffix(int value)
    {
        var lastTwo = Math.Abs(value) % 100;
        if (lastTwo is >= 11 and <= 13) return "th";
        return (Math.Abs(value) % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
    }

    public string Cardinal(int value)
    {
        if (value < 0 || value > LargestSpelled) return value.ToString(CultureInfo.InvariantCulture);
        if (value < 20) return _units[value];
        if (value < 100) return _tens[value / 10] + (value % 10 == 0 ? string.Empty : "-" + _units[value % 10]);
        if (value < 1000) return _units[value / 100] + " hundred" + (value % 100 == 0 ? string.Empty : " " + Cardinal(value % 100));
        return Cardinal(value / 1000) + " thousand" + (value % 1000 == 0 ? string.Empty : " " + Cardinal(value % 1000));
    }

    public string Ordinal(int value)
    {
        var words = Cardinal(value);
        if (value < 0 || value > LargestSpelled) return words + OrdinalSuffix(value);
        var cut = Math.Max(words.LastIndexOf(' '), words.LastIndexOf('-')) + 1;
        var last = words[cut..];
        var ordinal = _ordinalWords.TryGetValue(last, out var irregular) ? irregular
            : last.EndsWith('y') ? last[..^1] + "ieth"
            : last + "th";
        return words[..cut] + ordinal;
    }
}

public sealed class ItalianNumberSpeller : INumberSpeller
{
    private const int LargestSpelled = 999_999;

    private readonly string[] _units = ["zero", "uno", "due", "tre", "quattro", "cinque", "sei", "sette", "otto", "nove", "dieci",
        "undici", "dodici", "tredici", "quattordici", "quindici", "sedici", "diciassette", "diciotto", "diciannove"];

    private readonly string[] _tens = ["", "", "venti", "trenta", "quaranta", "cinquanta", "sessanta", "settanta", "ottanta", "novanta"];

    private readonly string[] _firstOrdinals = ["", "primo", "secondo", "terzo", "quarto", "quinto", "sesto", "settimo", "ottavo", "nono", "decimo"];

    public string Language => "it";

    public string OrdinalSuffix(int value) => "º";

    public string Cardinal(int value)
    {
        if (value < 0 || value > LargestSpelled) return value.ToString(CultureInfo.InvariantCulture);
        if (value == 0) return _units[0];
        var thousands = value / 1000;
        var rest = value % 1000;
        var head = thousands switch { 0 => string.Empty, 1 => "mille", _ => BelowThousand(thousands) + "mila" };
        var words = head + (rest == 0 ? string.Empty : BelowThousand(rest));
        return value > 3 && value % 10 == 3 && value % 100 != 13 ? words[..^1] + "é" : words;
    }

    public string Ordinal(int value)
    {
        if (value is > 0 and <= 10) return _firstOrdinals[value];
        if (value < 0 || value > LargestSpelled) return value.ToString(CultureInfo.InvariantCulture) + OrdinalSuffix(value);
        var words = Cardinal(value);
        if (words.EndsWith("tré", StringComparison.Ordinal)) return words[..^1] + "eesimo";
        if (words.EndsWith("sei", StringComparison.Ordinal)) return words + "esimo";
        if (words.EndsWith("mila", StringComparison.Ordinal)) return words[..^4] + "millesimo";
        return words[..^1] + "esimo";
    }

    private string BelowThousand(int value)
    {
        var hundreds = value / 100;
        var rest = value % 100;
        var head = hundreds switch { 0 => string.Empty, 1 => "cento", _ => _units[hundreds] + "cento" };
        if (head.Length > 0 && (rest == 8 || rest / 10 == 8)) head = head[..^1];
        return head + (rest == 0 ? string.Empty : BelowHundred(rest));
    }

    private string BelowHundred(int value)
    {
        if (value < 20) return _units[value];
        var tens = _tens[value / 10];
        var unit = value % 10;
        if (unit == 0) return tens;
        if (unit is 1 or 8) tens = tens[..^1];
        return tens + _units[unit];
    }
}

public sealed class GermanNumberSpeller : INumberSpeller
{
    private const int LargestSpelled = 999_999;

    private readonly string[] _units = ["null", "eins", "zwei", "drei", "vier", "fünf", "sechs", "sieben", "acht", "neun", "zehn",
        "elf", "zwölf", "dreizehn", "vierzehn", "fünfzehn", "sechzehn", "siebzehn", "achtzehn", "neunzehn"];

    private readonly string[] _tens = ["", "", "zwanzig", "dreißig", "vierzig", "fünfzig", "sechzig", "siebzig", "achtzig", "neunzig"];

    private readonly Dictionary<int, string> _irregularOrdinals = new() { [1] = "erste", [3] = "dritte", [7] = "siebte", [8] = "achte" };

    public string Language => "de";

    public string OrdinalSuffix(int value) => ".";

    public string Cardinal(int value)
    {
        if (value < 0 || value > LargestSpelled) return value.ToString(CultureInfo.InvariantCulture);
        if (value == 0) return _units[0];
        var thousands = value / 1000;
        var rest = value % 1000;
        var head = thousands == 0 ? string.Empty : Prefix(BelowThousand(thousands)) + "tausend";
        return head + (rest == 0 ? string.Empty : BelowThousand(rest));
    }

    public string Ordinal(int value)
    {
        if (value <= 0 || value > LargestSpelled) return value.ToString(CultureInfo.InvariantCulture) + OrdinalSuffix(value);
        var tail = value % 100;
        if (tail is 0 or >= 20) return Cardinal(value) + "ste";
        var head = value - tail == 0 ? string.Empty : Cardinal(value - tail);
        return head + (_irregularOrdinals.TryGetValue(tail, out var irregular) ? irregular : _units[tail] + "te");
    }

    private string Prefix(string words) => words.EndsWith("eins", StringComparison.Ordinal) ? words[..^1] : words;

    private string BelowThousand(int value)
    {
        var hundreds = value / 100;
        var rest = value % 100;
        var head = hundreds == 0 ? string.Empty : Prefix(_units[hundreds]) + "hundert";
        return head + (rest == 0 ? string.Empty : BelowHundred(rest));
    }

    private string BelowHundred(int value)
    {
        if (value < 20) return _units[value];
        var unit = value % 10;
        return (unit == 0 ? string.Empty : Prefix(_units[unit]) + "und") + _tens[value / 10];
    }
}

public sealed class SpanishNumberSpeller : INumberSpeller
{
    private const int LargestSpelled = 999_999;

    private readonly string[] _units = ["cero", "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve", "diez",
        "once", "doce", "trece", "catorce", "quince", "dieciséis", "diecisiete", "dieciocho", "diecinueve", "veinte",
        "veintiuno", "veintidós", "veintitrés", "veinticuatro", "veinticinco", "veintiséis", "veintisiete", "veintiocho", "veintinueve"];

    private readonly string[] _tens = ["", "", "veinte", "treinta", "cuarenta", "cincuenta", "sesenta", "setenta", "ochenta", "noventa"];

    private readonly string[] _hundreds = ["", "ciento", "doscientos", "trescientos", "cuatrocientos", "quinientos", "seiscientos",
        "setecientos", "ochocientos", "novecientos"];

    private readonly string[] _ordinalUnits = ["", "primero", "segundo", "tercero", "cuarto", "quinto", "sexto", "séptimo", "octavo", "noveno"];

    private readonly string[] _ordinalTens = ["", "décimo", "vigésimo", "trigésimo", "cuadragésimo", "quincuagésimo", "sexagésimo",
        "septuagésimo", "octogésimo", "nonagésimo"];

    private readonly string[] _ordinalHundreds = ["", "centésimo", "ducentésimo", "tricentésimo", "cuadringentésimo", "quingentésimo",
        "sexcentésimo", "septingentésimo", "octingentésimo", "noningentésimo"];

    public string Language => "es";

    public string OrdinalSuffix(int value) => "º";

    public string Cardinal(int value)
    {
        if (value < 0 || value > LargestSpelled) return value.ToString(CultureInfo.InvariantCulture);
        if (value == 0) return _units[0];
        var thousands = value / 1000;
        var rest = value % 1000;
        var head = thousands switch { 0 => string.Empty, 1 => "mil", _ => Shortened(BelowThousand(thousands)) + " mil" };
        return Joined(head, rest == 0 ? string.Empty : BelowThousand(rest));
    }

    public string Ordinal(int value)
    {
        if (value <= 0 || value > LargestSpelled) return value.ToString(CultureInfo.InvariantCulture) + OrdinalSuffix(value);
        var thousands = value / 1000;
        var rest = value % 1000;
        var head = thousands switch { 0 => string.Empty, 1 => "milésimo", _ => Shortened(BelowThousand(thousands)) + " milésimo" };
        return Joined(head, rest == 0 ? string.Empty : OrdinalBelowThousand(rest));
    }

    private string OrdinalBelowThousand(int value)
    {
        var tail = value % 100;
        var head = _ordinalHundreds[value / 100];
        var tailWords = tail switch
        {
            0 => string.Empty,
            11 => "undécimo",
            12 => "duodécimo",
            < 10 => _ordinalUnits[tail],
            _ => Joined(_ordinalTens[tail / 10], _ordinalUnits[tail % 10]),
        };
        return Joined(head, tailWords);
    }

    private string Shortened(string words) => words.EndsWith("veintiuno", StringComparison.Ordinal) ? words[..^3] + "ún"
        : words.EndsWith("uno", StringComparison.Ordinal) ? words[..^1] : words;

    private string Joined(string head, string tail) => head.Length == 0 ? tail : tail.Length == 0 ? head : head + " " + tail;

    private string BelowThousand(int value)
    {
        var hundreds = value / 100;
        var rest = value % 100;
        if (value == 100) return "cien";
        return Joined(_hundreds[hundreds], rest == 0 ? string.Empty : BelowHundred(rest));
    }

    private string BelowHundred(int value)
    {
        if (value < 30) return _units[value];
        var unit = value % 10;
        return unit == 0 ? _tens[value / 10] : _tens[value / 10] + " y " + _units[unit];
    }
}

public sealed class FrenchNumberSpeller : INumberSpeller
{
    private const int LargestSpelled = 999_999;

    private readonly string[] _units = ["zéro", "un", "deux", "trois", "quatre", "cinq", "six", "sept", "huit", "neuf", "dix",
        "onze", "douze", "treize", "quatorze", "quinze", "seize", "dix-sept", "dix-huit", "dix-neuf"];

    private readonly string[] _tens = ["", "", "vingt", "trente", "quarante", "cinquante", "soixante", "soixante", "quatre-vingt", "quatre-vingt"];

    public string Language => "fr";

    public string OrdinalSuffix(int value) => value == 1 ? "er" : "e";

    public string Cardinal(int value)
    {
        if (value < 0 || value > LargestSpelled) return value.ToString(CultureInfo.InvariantCulture);
        if (value == 0) return _units[0];
        var thousands = value / 1000;
        var rest = value % 1000;
        var head = thousands switch { 0 => string.Empty, 1 => "mille", _ => Singular(BelowThousand(thousands)) + " mille" };
        return head.Length == 0 ? BelowThousand(rest) : rest == 0 ? head : head + " " + BelowThousand(rest);
    }

    public string Ordinal(int value)
    {
        if (value == 1) return "premier";
        if (value <= 0 || value > LargestSpelled) return value.ToString(CultureInfo.InvariantCulture) + OrdinalSuffix(value);
        var words = Cardinal(value);
        var cut = Math.Max(words.LastIndexOf(' '), words.LastIndexOf('-')) + 1;
        var last = words[cut..];
        var stem = last switch
        {
            "cinq" => "cinqu",
            "neuf" => "neuv",
            "vingts" or "cents" => last[..^1],
            _ when last.EndsWith('e') => last[..^1],
            _ => last,
        };
        return words[..cut] + stem + "ième";
    }

    private string Singular(string words) => words.EndsWith("cents", StringComparison.Ordinal) || words.EndsWith("vingts", StringComparison.Ordinal) ? words[..^1] : words;

    private string BelowThousand(int value)
    {
        var hundreds = value / 100;
        var rest = value % 100;
        var head = hundreds switch { 0 => string.Empty, 1 => "cent", _ => _units[hundreds] + " cent" + (rest == 0 ? "s" : string.Empty) };
        if (rest == 0) return head;
        return head.Length == 0 ? BelowHundred(rest) : head + " " + BelowHundred(rest);
    }

    private string BelowHundred(int value)
    {
        if (value < 20) return _units[value];
        var tens = value / 10;
        var unit = tens is 7 or 9 ? value % 10 + 10 : value % 10;
        var stem = _tens[tens];
        if (unit == 0) return tens == 8 ? stem + "s" : stem;
        if (unit is 1 or 11 && tens < 8) return stem + " et " + _units[unit];
        return stem + "-" + _units[unit];
    }
}
