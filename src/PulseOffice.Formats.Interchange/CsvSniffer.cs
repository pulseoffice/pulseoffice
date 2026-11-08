using System.Globalization;
using System.Text;
using PulseOffice.Foundation.Text;

namespace PulseOffice.Formats.Interchange;

public interface ICsvSniffer
{
    CsvOptions Sniff(string sample, CsvOptions baseline);
}

public sealed class CsvSniffer(IDelimitedText delimited, ICsvNumberParser numbers) : ICsvSniffer
{
    public CsvSniffer(IDelimitedText delimited) : this(delimited, new CsvNumberParser())
    {
    }

    private static readonly CultureInfo[] Candidates =
    [
        CultureInfo.InvariantCulture,
        CultureInfo.GetCultureInfo("it-IT"),
    ];

    public CsvOptions Sniff(string sample, CsvOptions baseline)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(baseline);

        var separator = delimited.GuessSeparator(sample) ?? baseline.Delimiter.Separator;
        var format = baseline.Delimiter with { Separator = separator };

        var rows = Rows(sample, format);
        var culture = BestCulture(rows, baseline.Culture);

        return baseline with { Delimiter = format, Culture = culture };
    }

    private List<IReadOnlyList<string>> Rows(string sample, DelimitedFormat format)
    {
        using var reader = new StringReader(sample);
        return [.. delimited.Read(reader, format).Take(20)];
    }

    private CultureInfo BestCulture(List<IReadOnlyList<string>> rows, CultureInfo fallback)
    {
        var fields = rows
            .SelectMany(row => row)
            .Select(field => field.Trim())
            .Where(field => field.Length > 0 && field.Any(char.IsAsciiDigit))
            .ToList();

        if (fields.Count == 0) return fallback;

        var scores = Candidates
            .Select(culture => (Culture: culture, Score: Score(fields, culture)))
            .OrderByDescending(pair => pair.Score)
            .ToList();

        if (scores[0].Score == 0) return fallback;
        if (scores.Count > 1 && scores[0].Score == scores[1].Score) return fallback;

        return scores[0].Culture;
    }

    private int Score(List<string> fields, CultureInfo culture)
    {
        var separators = new StringBuilder(culture.NumberFormat.NumberDecimalSeparator)
            .Append(culture.NumberFormat.NumberGroupSeparator)
            .ToString();

        var score = 0;

        foreach (var field in fields)
        {
            if (!numbers.TryParse(field, culture, out _)) continue;

            score += field.IndexOfAny([.. separators]) >= 0 ? 2 : 1;
        }

        return score;
    }
}
