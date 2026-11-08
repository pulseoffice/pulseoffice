using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests.PropertyAudit;

public sealed class ShapeStylePropertyAuditTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private readonly ModelPropertyProbe _probe = new(new Dictionary<string, object>
    {
        ["*.ColorHex"] = "#3366CC",
        ["*.BackgroundHex"] = "#FFCC00",
        ["@Twip"] = Twip.FromPoints(5),
        ["*.Opacity"] = 0.5,
        ["*.AngleDegrees"] = 30.0,
        ["*.RotationDegrees"] = 90.0,
        ["*.Size"] = 18.0,
        ["*.Family"] = "Arial",
        ["*.ArrowScale"] = 2.0,
        ["*.Position"] = 0.5,
        ["*.CentreX"] = 0.25,
        ["*.CentreY"] = 0.75,
        ["*.Border"] = 0.2,
        ["Fill.Gradient.Stops"] = new List<GradientStop> { new("#2563EB", 0), new("#3366CC", 0.5), new("#0F172A", 1) },
        ["Fill.Bitmap.Data"] = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 },
        ["Fill.Bitmap.MediaType"] = new object[] { "image/png" },
    });

    private object Prepare(string path, object instance)
    {
        var style = (ShapeStyle)instance;
        return path switch
        {
            "Fill" => style with { Fill = style.Fill with { Bitmap = null } },
            "Line" or "Line.Dash" => style with { Line = style.Line with { CustomDash = null } },
            _ when path.StartsWith("Line.CustomDash", StringComparison.Ordinal) => style with { Line = style.Line with { Dash = style.Line.CustomDash!.NearestPreset() } },
            _ when path.StartsWith("Fill.Gradient.Centre", StringComparison.Ordinal) => style with { Fill = style.Fill with { Kind = ShapeFillKind.Gradient, Gradient = style.Fill.Gradient with { Kind = GradientKind.Radial } } },
            _ when path.StartsWith("Fill.Gradient", StringComparison.Ordinal) => style with { Fill = style.Fill with { Kind = ShapeFillKind.Gradient } },
            _ when path.StartsWith("Fill.Hatch", StringComparison.Ordinal) => style with { Fill = style.Fill with { Kind = ShapeFillKind.Hatch } },
            _ when path.StartsWith("Fill.Bitmap.Tile", StringComparison.Ordinal) && path.EndsWith("Ratio", StringComparison.Ordinal) => style with { Fill = style.Fill with { Kind = ShapeFillKind.Bitmap, Bitmap = style.Fill.Bitmap! with { Tiled = true, TileWidth = Twip.Zero, TileHeight = Twip.Zero } } },
            _ when path.StartsWith("Fill.Bitmap.Tile", StringComparison.Ordinal) => style with { Fill = style.Fill with { Kind = ShapeFillKind.Bitmap, Bitmap = style.Fill.Bitmap! with { Tiled = true, TileWidthRatio = 0, TileHeightRatio = 0 } } },
            _ when path.StartsWith("Fill.Bitmap", StringComparison.Ordinal) => style with { Fill = style.Fill with { Kind = ShapeFillKind.Bitmap } },
            _ when path.StartsWith("Shadow.", StringComparison.Ordinal) => style with { Shadow = style.Shadow with { Enabled = true } },
            _ when path.StartsWith("Glow.", StringComparison.Ordinal) => style with { Glow = style.Glow with { Enabled = true } },
            _ when path == "Extrusion.Depth" => style with { Extrusion = style.Extrusion with { Kind = ExtrusionKind.Extrude } },
            _ when path == "Bevel.Depth" => style with { Bevel = style.Bevel with { Style = BevelStyle.Round } },
            _ when path.StartsWith("Text.", StringComparison.Ordinal) && path != "Text.AutomaticColor" => style with { Text = style.Text with { AutomaticColor = false } },
            _ => style,
        };
    }

    private Presentation Place(object style)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Add(new ObjectElement
        {
            Id = "probe",
            Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(2), Twip.FromCentimeters(6), Twip.FromCentimeters(4)),
            Content = new ShapeObject { Id = "probe", Shape = ShapeKind.Rectangle, Style = (ShapeStyle)style, Text = "Sonda" },
        });
        deck.AddExisting(slide);
        return deck;
    }

    private object? Recover(Presentation deck) =>
        deck.Slides.SelectMany(slide => slide.Elements).OfType<ObjectElement>().Select(element => element.Content).OfType<ShapeObject>().FirstOrDefault()?.Style;

    private Presentation Through(IPresentationWriter writer, IPresentationReader reader, Presentation deck)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(deck, stream).Succeeded);
        stream.Position = 0;
        return reader.Read(stream).Value!;
    }

    [Fact]
    public void Ogni_proprieta_dello_stile_di_forma_attraversa_ODP_e_PPTX_o_la_perdita_e_dichiarata()
    {
        var audit = new CodecPropertyAudit<Presentation>(_probe, Place, Recover, Prepare);
        var odp = new OdpWriter();
        var pptx = new PptxWriter();

        var silent = audit.Silent(ShapeStyle.Default, "odp", odp.CheckFidelity, deck => Through(odp, new OdpReader(_themes, _masters), deck))
            .Concat(audit.Silent(ShapeStyle.Default, "pptx", pptx.CheckFidelity, deck => Through(pptx, new PptxReader(_themes, _masters), deck)))
            .Select(finding => $"{finding.Format} {finding.Path}: {finding.Expected} -> {finding.Actual}")
            .ToList();

        Assert.True(silent.Count == 0, string.Join(Environment.NewLine, silent));
    }
}
