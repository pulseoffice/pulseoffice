using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxVectorRoundTripTests
{
    private const string Svg =
        """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">
          <path d="M2 2 L22 2 L12 22 Z" fill="#DC2626"/>
          <path d="M4 12 L20 12" fill="none" stroke="#1E3A8A" stroke-width="2"/>
        </svg>
        """;

    private static Workbook WithVector(VectorGraphicObject vector)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");

        sheet.Objects.Add(new PlacedObject
        {
            Id = vector.Id,
            Content = vector,
            Anchor = new CellAddress(2, 3),
            Size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(4)),
        });

        return workbook;
    }

    private static VectorGraphicObject Vector(string? recolour = null) => new()
    {
        Id = "logo",
        Asset = new VectorAsset
        {
            Id = "logo",
            Name = "logo",
            SourceKind = VectorSourceKind.InlineSvg,
            SvgContent = Svg,
        },
        RecolorHex = recolour,
    };

    private static Workbook RoundTrip(Workbook source)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);

        stream.Position = 0;
        var result = new XlsxReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static VectorGraphicObject Read(Workbook workbook)
    {
        var placed = Assert.Single(workbook.Sheets[0].Objects);
        return Assert.IsType<VectorGraphicObject>(placed.Content);
    }

    [Fact]
    public void UnImmagineVettorialeTornaComeUnOggettoSolo()
    {
        var read = Read(RoundTrip(WithVector(Vector())));

        Assert.Equal("logo", read.Id);
        Assert.Contains("<path", read.Asset.SvgContent);
    }

    [Fact]
    public void ITracciatiRestanoDue()
    {
        var read = Read(RoundTrip(WithVector(Vector())));

        var drawing = new VectorOutlineReader().Read(read.Asset.SvgContent!);

        Assert.Equal(2, drawing.Paths.Count);
    }

    [Fact]
    public void RiempimentoEContornoSopravvivono()
    {
        var read = Read(RoundTrip(WithVector(Vector())));

        var paths = new VectorOutlineReader().Read(read.Asset.SvgContent!).Paths;

        Assert.Equal("#DC2626", paths[0].Paint.FillHex, ignoreCase: true);
        Assert.Null(paths[1].Paint.FillHex);
        Assert.Equal("#1E3A8A", paths[1].Paint.StrokeHex, ignoreCase: true);
    }

    [Fact]
    public void LaRicolorazioneEGiaApplicataNelFile()
    {
        var read = Read(RoundTrip(WithVector(Vector("#16A34A"))));

        var paths = new VectorOutlineReader().Read(read.Asset.SvgContent!).Paths;

        Assert.Equal("#16A34A", paths[0].Paint.FillHex, ignoreCase: true);
    }

    [Fact]
    public void AncoraggioDimensioneERotazioneSopravvivono()
    {
        var workbook = WithVector(Vector() with { RotationDegrees = 45 });

        var placed = Assert.Single(RoundTrip(workbook).Sheets[0].Objects);
        var vector = Assert.IsType<VectorGraphicObject>(placed.Content);

        Assert.Equal(new CellAddress(2, 3), placed.Anchor);
        Assert.Equal(4, placed.Size.Width.ToCentimeters(), 2);
        Assert.Equal(45, vector.RotationDegrees, 1);
    }

    [Fact]
    public void FormeEImmaginiConvivono()
    {
        var workbook = WithVector(Vector());

        workbook.Sheets[0].Objects.Add(new PlacedObject
        {
            Id = "f1",
            Content = new ShapeObject { Id = "f1", Shape = ShapeKind.Ellipse },
            Anchor = new CellAddress(10, 1),
        });

        var result = RoundTrip(workbook);

        Assert.Single(result.Sheets[0].Objects, o => o.Content is ShapeObject);
        Assert.Single(result.Sheets[0].Objects, o => o.Content is VectorGraphicObject);
    }
}
