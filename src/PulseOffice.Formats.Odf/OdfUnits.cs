using PulseOffice.Foundation.Colors;
using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfUnits
{

    public string Cm(Twip value) => (value.Value / PulseOffice.Foundation.Units.Twip.PerCentimeter).ToString("0.###", CultureInfo.InvariantCulture) + "cm";

    public string Pt(double size) => size.ToString("0.##", CultureInfo.InvariantCulture) + "pt";

    public string Hex(uint argb) => new ArgbColor(argb).ToLowerHashedHex();

    public string Seconds(int milliseconds) => (milliseconds / 1000.0).ToString("0.###", CultureInfo.InvariantCulture) + "s";

    public string XmlDuration(int milliseconds) => "PT" + (milliseconds / 1000.0).ToString("0.###", CultureInfo.InvariantCulture) + "S";

    public string Align(TextAlign align) => align switch
    {
        TextAlign.Center => "center",
        TextAlign.Right => "end",
        TextAlign.Justify => "justify",
        _ => "start",
    };

    public string Clip(DocumentRect bounds, (double Left, double Top, double Right, double Bottom) crop) =>
        $"rect({Cm(new Twip((int)(crop.Top * bounds.Height.Value)))} " +
        $"{Cm(new Twip((int)(crop.Right * bounds.Width.Value)))} " +
        $"{Cm(new Twip((int)(crop.Bottom * bounds.Height.Value)))} " +
        $"{Cm(new Twip((int)(crop.Left * bounds.Width.Value)))})";

    public string FileSafe(string id) => new([.. id.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')]);

    public string PackageFileName(string? name)
    {
        var cleaned = new string([.. (name ?? string.Empty).Select(ch => ch is '/' or '\\' or ':' || char.IsControl(ch) ? '_' : ch)]).Trim();
        return cleaned.Trim('.').Length == 0 ? "object.bin" : cleaned;
    }

    public OdfImageTypes Images { get; } = new();

    public string MediaExtension(string mimeType) => _media.Extension(mimeType);

    private readonly PulseOffice.Formats.MediaTypes _media = new();

    public IEnumerable<XAttribute> Bounds(DocumentRect bounds, XNamespace svg) =>
    [
        new(svg + "x", Cm(bounds.Left)),
        new(svg + "y", Cm(bounds.Top)),
        new(svg + "width", Cm(bounds.Width)),
        new(svg + "height", Cm(bounds.Height)),
    ];
}
