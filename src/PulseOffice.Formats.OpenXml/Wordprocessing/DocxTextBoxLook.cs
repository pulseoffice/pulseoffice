using System.Globalization;
using DocumentFormat.OpenXml;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using A = DocumentFormat.OpenXml.Drawing;
using WPS = DocumentFormat.OpenXml.Office2010.Word.DrawingShape;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxTextBoxLook(XlsxShapeStyleMapper styles)
{
    private const double WidestLine = 1584;

    public IEnumerable<OpenXmlElement> Write(FrameProperties? look)
    {
        yield return look?.Background is { } background ? styles.Fill(ShapeFill.Default with { ColorHex = Hex(background) }) : new A.NoFill();
        yield return look is { BorderWidth: not null } or { BorderColor: not null }
            ? styles.Line(ShapeLine.Default with { ColorHex = Hex(look.BorderColor ?? 0xFF000000), Width = Twip.FromPoints(LineWidth(look.BorderWidth ?? 0.5)) })
            : new A.Outline(new A.NoFill());
    }

    public FrameProperties? Read(WPS.ShapeProperties? properties)
    {
        if (properties is null) return null;
        var style = styles.Read(properties, null);
        var background = properties.GetFirstChild<A.SolidFill>() is not null && style.Fill is { Kind: ShapeFillKind.Solid, IsVisible: true } fill ? Colour(fill.ColorHex) : null;
        var line = properties.GetFirstChild<A.Outline>() is { } outline && outline.GetFirstChild<A.NoFill>() is null && outline.HasChildren && style.Line.IsVisible ? style.Line : null;
        if (background is null && line is null) return null;
        return new FrameProperties
        {
            Background = background,
            BorderColor = line is null ? null : Colour(line.ColorHex),
            BorderWidth = line?.Width.ToPoints(),
        };
    }

    public bool Approximated(WPS.ShapeProperties? properties) =>
        properties?.GetFirstChild<A.GradientFill>() is not null || properties?.GetFirstChild<A.BlipFill>() is not null || properties?.GetFirstChild<A.PatternFill>() is not null;

    private double LineWidth(double points) => double.IsFinite(points) ? Math.Clamp(points, 0, WidestLine) : 0.5;

    private string Hex(uint argb) => "#" + (argb & 0xFFFFFF).ToString("X6", CultureInfo.InvariantCulture);

    private uint? Colour(string hex) =>
        uint.TryParse(hex.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb) && hex.TrimStart('#').Length == 6 ? 0xFF000000 | rgb : null;
}
