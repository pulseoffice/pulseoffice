using System.Globalization;
using PulseOffice.Formats.Interchange;
using PulseOffice.Formats.Interchange.Text.Plain;
using PulseOffice.Foundation.Text;

namespace PulseOffice.Sheets.UI.Dialogs;

public interface ICsvUnattendedOptions
{
    CsvOptions Apply(CsvOptions detected, IReadOnlyDictionary<string, string> options);
}

public sealed class CsvUnattendedOptions(ICodePages codePages) : ICsvUnattendedOptions
{
    private readonly Dictionary<string, char> _separators = new(StringComparer.OrdinalIgnoreCase)
    {
        ["comma"] = ',',
        ["semicolon"] = ';',
        ["tab"] = '\t',
        ["space"] = ' ',
    };

    private readonly Dictionary<string, int> _encodings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["utf-8"] = 65001,
        ["utf8"] = 65001,
        ["windows-1252"] = 1252,
        ["cp1252"] = 1252,
        ["iso-8859-15"] = 28605,
        ["utf-16"] = 1200,
    };

    public CsvOptions Apply(CsvOptions detected, IReadOnlyDictionary<string, string> options)
    {
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(options);
        var result = detected;
        if (options.TryGetValue("csv_separator", out var separator)) result = result with { Delimiter = Delimiter(detected.Delimiter, separator) };
        if (options.TryGetValue("csv_encoding", out var encoding)) result = result with { Encoding = codePages.Get(CodePage(encoding)) };
        if (options.TryGetValue("csv_language", out var language)) result = result with { Culture = Culture(language) };
        if (options.TryGetValue("csv_skip_rows", out var skip)) result = result with { SkipRows = SkipRows(skip) };
        return result;
    }

    private DelimitedFormat Delimiter(DelimitedFormat detected, string value) =>
        _separators.TryGetValue(value, out var named) ? detected with { Separator = named, OtherSeparators = string.Empty }
        : value.Length == 1 ? detected with { Separator = value[0], OtherSeparators = string.Empty }
        : throw new ArgumentException($"Unknown separator '{value}'.");

    private int CodePage(string value) =>
        _encodings.TryGetValue(value, out var codePage) ? codePage : throw new ArgumentException($"Unknown encoding '{value}'.");

    private static CultureInfo Culture(string value)
    {
        try
        {
            return CultureInfo.GetCultureInfo(value);
        }
        catch (CultureNotFoundException)
        {
            throw new ArgumentException($"Unknown language '{value}'.");
        }
    }

    private static int SkipRows(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rows) && rows is >= 0 and <= 1000
            ? rows
            : throw new ArgumentException($"Invalid number of rows to skip '{value}'.");
}
