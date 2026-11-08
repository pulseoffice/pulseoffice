using System.Globalization;
using System.Text;
using PulseOffice.Foundation.Text;
using PulseOffice.Sheets;

namespace PulseOffice.Formats.Interchange;

public sealed record CsvOptions
{
    public DelimitedFormat Delimiter { get; init; } = DelimitedFormat.Comma;

    public Encoding Encoding { get; init; } = new UTF8Encoding(false);

    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    public bool DetectTypes { get; init; } = true;

    public string SheetName { get; init; } = Workbook.CanonicalSheetPrefix + "1";

    public int SkipRows { get; init; }

    public string LineEnding { get; init; } = "\r\n";

    public bool AutoDetect { get; init; } = true;

    public bool GuardFormulas { get; init; }

    public static CsvOptions Default { get; } = new();
}
