using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Styling;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxColumnMetrics
{
    private const double PointsPerPixel = 0.75;
    private const double PixelsPerPoint = 4.0 / 3.0;

    private readonly double _pointsPerCharacter;

    public XlsxColumnMetrics(string family, double size, XlsxDigitWidths digits)
    {
        ArgumentNullException.ThrowIfNull(digits);
        var points = size > 0 && double.IsFinite(size) ? size : CellFont.Default.Size;
        _pointsPerCharacter = points * digits.EmOf(family) * PixelsPerPoint * PointsPerPixel;
    }

    public double ToCharacters(Twip width) => Math.Round(width.ToPoints() / _pointsPerCharacter, 4);

    public Twip FromCharacters(double characters) => Twip.FromPoints(characters * _pointsPerCharacter);
}

internal sealed class XlsxColumnMetricsFactory
{
    private readonly XlsxDigitWidths _digits = new();

    public XlsxColumnMetrics For(CellFont font) => new(font.Family, font.Size, _digits);

    public XlsxColumnMetrics For(Stylesheet? stylesheet)
    {
        var font = stylesheet?.Fonts?.Elements<Font>().FirstOrDefault();
        var family = font?.GetFirstChild<FontName>()?.Val?.Value ?? "Calibri";
        var size = font?.GetFirstChild<FontSize>()?.Val?.Value ?? 11;
        return new XlsxColumnMetrics(family, size, _digits);
    }
}

internal sealed class XlsxDigitWidths
{
    private const double Fallback = 0.55;

    private readonly Dictionary<string, double> _ems = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Calibri"] = 0.507, ["Carlito"] = 0.507,
        ["Arial"] = 0.556, ["Liberation Sans"] = 0.556, ["Helvetica"] = 0.556, ["Arimo"] = 0.556,
        ["Times New Roman"] = 0.5, ["Liberation Serif"] = 0.5, ["Tinos"] = 0.5,
        ["Courier New"] = 0.6, ["Liberation Mono"] = 0.6, ["Cousine"] = 0.6,
        ["Cambria"] = 0.555, ["Caladea"] = 0.555,
        ["Verdana"] = 0.636, ["DejaVu Sans"] = 0.636,
        ["Aptos"] = 0.54,
    };

    public double EmOf(string family) => _ems.TryGetValue(family, out var em) ? em : Fallback;
}
