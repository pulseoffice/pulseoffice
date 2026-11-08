using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsImageRoundTripTests
{
    private const string Svg =
        """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path d="M2 2 L22 2 L12 22 Z"/></svg>""";

    private static Workbook WithImage()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");

        sheet.Objects.Add(new PlacedObject
        {
            Id = "logo",
            Content = new VectorGraphicObject
            {
                Id = "logo",
                Asset = new VectorAsset
                {
                    Id = "logo",
                    Name = "logo",
                    SourceKind = VectorSourceKind.InlineSvg,
                    SvgContent = Svg,
                },
            },
            Anchor = new CellAddress(1, 1),
            Size = new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(3)),
        });

        return workbook;
    }

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
    public void LImmagineVettorialeSopravvive()
    {
        var placed = Assert.Single(RoundTrip(WithImage()).Sheets[0].Objects);
        var vector = Assert.IsType<VectorGraphicObject>(placed.Content);

        Assert.Contains("<path", vector.Asset.SvgContent);
        Assert.Equal(new CellAddress(1, 1), placed.Anchor);
        Assert.Equal(3, placed.Size.Width.ToCentimeters(), 2);
    }

    [Fact]
    public void IlDisegnoFinisceInUnaVoceDelPacchetto()
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(WithImage(), stream);

        stream.Position = 0;
        using var archive = new System.IO.Compression.ZipArchive(stream);

        Assert.Contains(archive.Entries, entry => entry.FullName.StartsWith("Pictures/"));
        Assert.Contains(archive.Entries, entry => entry.FullName == "META-INF/manifest.xml");
    }
}
