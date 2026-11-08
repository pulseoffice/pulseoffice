using PulseOffice.Drawing.Assets;
using System.Globalization;
using System.Security;
using System.Text;
using PulseOffice.Document.Forms;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Formats;

public sealed class SignatureLineImage(IVectorRasterizer rasterizer)
{
    private const double PixelsPerPoint = 96.0 / 72.0 * 2;
    private const int LargestSide = 2048;

    public SignatureLineImage()
        : this(new NoVectorRasterizer())
    {
    }

    public string MediaType => "image/svg+xml";

    public string RasterMediaType => "image/png";

    public byte[]? UnsignedRaster(SignatureLineObject line, DocumentSize size)
    {
        var width = Math.Max(1, size.Width.ToPoints()) * PixelsPerPoint;
        var height = Math.Max(1, size.Height.ToPoints()) * PixelsPerPoint;
        var scale = Math.Min(1, LargestSide / Math.Max(width, height));
        return rasterizer.Png(Unsigned(line, size), (int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale));
    }

    public byte[] Unsigned(SignatureLineObject line, DocumentSize size)
    {
        var width = Math.Max(1, size.Width.ToPoints());
        var height = Math.Max(1, size.Height.ToPoints());
        var baseline = height * 0.55;
        var font = Math.Clamp(height * 0.16, 6, 14);
        var lines = new[] { line.SignerName, line.SignerTitle }.Where(part => !string.IsNullOrWhiteSpace(part)).ToList();
        var svg = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{Number(width)}pt\" height=\"{Number(height)}pt\" viewBox=\"0 0 {Number(width)} {Number(height)}\">")
            .Append(CultureInfo.InvariantCulture, $"<text x=\"{Number(font * 0.4)}\" y=\"{Number(baseline - font * 0.3)}\" font-family=\"sans-serif\" font-size=\"{Number(font * 1.6)}\" fill=\"#000000\">X</text>")
            .Append(CultureInfo.InvariantCulture, $"<line x1=\"0\" y1=\"{Number(baseline)}\" x2=\"{Number(width)}\" y2=\"{Number(baseline)}\" stroke=\"#000000\" stroke-width=\"0.75\"/>");
        for (var index = 0; index < lines.Count; index++)
            svg.Append(CultureInfo.InvariantCulture, $"<text x=\"{Number(font * 0.4)}\" y=\"{Number(baseline + font * (1.3 + index * 1.25))}\" font-family=\"sans-serif\" font-size=\"{Number(font)}\" fill=\"#000000\">{SecurityElement.Escape(lines[index])}</text>");
        return Encoding.UTF8.GetBytes(svg.Append("</svg>").ToString());
    }

    private string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
