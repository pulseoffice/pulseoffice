using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsRasterRoundTripTests
{
    private static readonly byte[] PngRosso = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static Workbook RoundTrip(Workbook source)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);

        stream.Position = 0;
        var result = new OdsReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    [Fact]
    public void UnPngTornaIndietroIntatto()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");

        sheet.Objects.Add(new PlacedObject
        {
            Id = "foto",
            Content = new RasterImageObject
            {
                Id = "foto",
                Data = PngRosso,
                MediaType = "image/png",
            },
            Anchor = new CellAddress(1, 1),
            Size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3)),
        });

        var letto = RoundTrip(workbook).Sheets[0];
        var placed = Assert.Single(letto.Objects);
        var raster = Assert.IsType<RasterImageObject>(placed.Content);

        Assert.Equal(PngRosso, raster.Data.ToArray());
        Assert.Equal("image/png", raster.MediaType);
        Assert.Equal(new CellAddress(1, 1), placed.Anchor);
    }
}
