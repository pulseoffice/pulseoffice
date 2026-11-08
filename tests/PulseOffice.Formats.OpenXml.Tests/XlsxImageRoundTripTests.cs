using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxImageRoundTripTests
{
    private static readonly byte[] PngRosso = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static Workbook RoundTrip(Workbook source)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);

        stream.Position = 0;
        var result = new XlsxReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static Workbook ConImmagine()
    {
        var workbook = Workbook.CreateDefault();

        workbook.Sheets[0].Objects.Add(new PlacedObject
        {
            Id = "foto",
            Content = new RasterImageObject
            {
                Id = "foto",
                Data = PngRosso,
                MediaType = "image/png",
            },
            Anchor = new CellAddress(2, 1),
            Size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3)),
        });

        return workbook;
    }

    [Fact]
    public void LImmagineTornaIndietroConIProprIByte()
    {
        var sheet = RoundTrip(ConImmagine()).Sheets[0];

        var placed = Assert.Single(sheet.Objects);
        var raster = Assert.IsType<RasterImageObject>(placed.Content);

        Assert.Equal(PngRosso, raster.Data.ToArray());
        Assert.Equal("image/png", raster.MediaType);
    }

    [Fact]
    public void LAncoraggioELaDimensioneSopravvivono()
    {
        var placed = Assert.Single(RoundTrip(ConImmagine()).Sheets[0].Objects);

        Assert.Equal(new CellAddress(2, 1), placed.Anchor);
        Assert.Equal(4, placed.Size.Width.ToCentimeters(), 2);
        Assert.Equal(3, placed.Size.Height.ToCentimeters(), 2);
    }

    [Fact]
    public void IlDuotonoSopravviveInXlsxESiDichiaraPersoInOds()
    {
        var workbook = ConImmagine();
        workbook.Sheets[0].Objects[0] = workbook.Sheets[0].Objects[0] with
        {
            Content = ((RasterImageObject)workbook.Sheets[0].Objects[0].Content) with { Duotone = new ImageDuotone("#1F2937", "#FDE68A") },
        };

        var raster = Assert.IsType<RasterImageObject>(Assert.Single(RoundTrip(workbook).Sheets[0].Objects).Content);

        Assert.Equal(new ImageDuotone("#1F2937", "#FDE68A"), raster.Duotone);
        Assert.Contains("ODF044", new PulseOffice.Formats.Odf.OdsSpreadsheetWriter().CheckFidelity(workbook).Losses.Select(loss => loss.Code));
    }
}
