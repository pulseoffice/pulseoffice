using System.Xml.Linq;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Packaging;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdfShapeStylesAuditTests
{
    private readonly XNamespace _style = OdfPackage.Namespaces.Style;
    private readonly XNamespace _draw = OdfPackage.Namespaces.Draw;
    private readonly XNamespace _svg = OdfPackage.Namespaces.Svg;
    private readonly XNamespace _fo = OdfPackage.Namespaces.Fo;
    private readonly XNamespace _loext = OdfPackage.Namespaces.Loext;
    private readonly OdfShapeStyleReader _reader = new();

    [Theory]
    [InlineData("NaN%")]
    [InlineData("Infinity%")]
    [InlineData("-50%")]
    [InlineData("1e300%")]
    public void Le_opacita_restano_fra_zero_e_uno(string value)
    {
        var style = Read(new XAttribute(_draw + "opacity", value), new XAttribute(_svg + "stroke-opacity", value),
            new XAttribute(_draw + "shadow", "visible"), new XAttribute(_draw + "shadow-opacity", value),
            new XAttribute(_loext + "glow-radius", "0.2cm"), new XAttribute(_loext + "glow-transparency", value));

        Assert.InRange(style.Fill.Opacity, 0, 1);
        Assert.InRange(style.Line.Opacity, 0, 1);
        Assert.InRange(style.Shadow.Opacity, 0, 1);
        Assert.InRange(style.Glow.Opacity, 0, 1);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e300")]
    public void Il_tratteggio_ha_conteggi_e_lunghezze_finiti(string value)
    {
        var dash = new XElement(_draw + "stroke-dash", new XAttribute(_draw + "name", "D"),
            new XAttribute(_draw + "dots1", value), new XAttribute(_draw + "dots2", value),
            new XAttribute(_draw + "dots1-length", value + "%"), new XAttribute(_draw + "distance", value + "%"));
        var style = Read(new Dictionary<string, XElement> { ["D"] = dash },
            new XAttribute(_draw + "stroke", "dash"), new XAttribute(_draw + "stroke-dash", "D"));

        var pattern = style.Line.CustomDash;
        Assert.NotNull(pattern);
        Assert.InRange(pattern!.Dots1, 0, 100);
        Assert.InRange(pattern.Dots2, 0, 100);
        Assert.InRange(pattern.Dots1Length, 0, 100);
        Assert.InRange(pattern.Distance, 0, 100);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e300")]
    public void Angoli_del_gradiente_del_tratteggio_e_del_testo_restano_finiti(string value)
    {
        var gradient = new XElement(_draw + "gradient", new XAttribute(_draw + "name", "G"), new XAttribute(_draw + "angle", value),
            new XAttribute(_draw + "border", value + "%"),
            Enumerable.Range(0, 3).Select(_ => new XElement(_loext + "gradient-stop", new XAttribute(_svg + "offset", value), new XAttribute(_loext + "color-value", "#ff0000"))));
        var hatch = new XElement(_draw + "hatch", new XAttribute(_draw + "name", "H"), new XAttribute(_draw + "rotation", value));
        var definitions = new Dictionary<string, XElement> { ["G"] = gradient, ["H"] = hatch };

        var gradientStyle = Read(definitions, new XAttribute(_draw + "fill", "gradient"), new XAttribute(_draw + "fill-gradient-name", "G"),
            new XAttribute(_style + "text-rotation-angle", value));
        var hatchStyle = Read(definitions, new XAttribute(_draw + "fill", "hatch"), new XAttribute(_draw + "fill-hatch-name", "H"));

        Assert.InRange(gradientStyle.Fill.Gradient.AngleDegrees, 0, 360);
        Assert.InRange(gradientStyle.Fill.Gradient.Border, 0, 1);
        Assert.All(gradientStyle.Fill.Gradient.Stops, stop => Assert.InRange(stop.Position, 0, 1));
        Assert.InRange(gradientStyle.Text.RotationDegrees, -360, 360);
        Assert.InRange(hatchStyle.Fill.Hatch.AngleDegrees, -360, 360);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e300pt")]
    [InlineData("-4pt")]
    public void La_dimensione_del_testo_della_forma_resta_nei_limiti(string size)
    {
        var style = Read(new Dictionary<string, XElement>(), [new XAttribute(_draw + "fill", "solid")],
            new XElement(_style + "text-properties", new XAttribute(_fo + "font-size", size)));

        Assert.InRange(style.Text.Size, 1, 4000);
    }

    [Fact]
    public void La_scala_delle_frecce_ha_un_tetto()
    {
        var style = Read(new XAttribute(_svg + "stroke-width", "0.001cm"), new XAttribute(_draw + "marker-end", "Arrow"),
            new XAttribute(_draw + "marker-end-width", "100000cm"));

        Assert.InRange(style.Line.ArrowScale, 0, 10);
    }

    private ShapeStyle Read(params XAttribute[] attributes) => Read(new Dictionary<string, XElement>(), attributes);

    private ShapeStyle Read(IReadOnlyDictionary<string, XElement> definitions, XAttribute[] attributes, params XElement[] extra) =>
        _reader.Read(new XElement(_style + "style", new XAttribute(_style + "name", "gr1"), new XAttribute(_style + "family", "graphic"),
            new XElement(_style + "graphic-properties", attributes), extra), definitions);

    private ShapeStyle Read(IReadOnlyDictionary<string, XElement> definitions, params XAttribute[] attributes) =>
        Read(definitions, attributes, []);
}
