using System.IO.Compression;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class OpenXmlDrawingAuditTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static Workbook WithImage(ImageDuotone duotone)
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Objects.Add(new PlacedObject
        {
            Id = "foto",
            Content = new RasterImageObject { Id = "foto", Data = Png, MediaType = "image/png", Duotone = duotone },
            Anchor = new CellAddress(2, 1),
            Size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3)),
        });
        return workbook;
    }

    private static MemoryStream Written(Workbook workbook)
    {
        var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        return stream;
    }

    private static string Drawing(MemoryStream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        using var reader = new StreamReader(archive.Entries.Single(entry => entry.FullName.StartsWith("xl/drawings/drawing", StringComparison.Ordinal)).Open());
        return reader.ReadToEnd();
    }

    [Fact]
    public void A_duotone_colour_of_six_characters_that_are_not_hex_does_not_reject_the_workbook()
    {
        using var stream = Written(WithImage(new ImageDuotone("#1F2937", "#FDE68A")));
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.Entries.Single(found => found.FullName.StartsWith("xl/drawings/drawing", StringComparison.Ordinal));
            string xml;
            using (var reader = new StreamReader(entry.Open())) xml = reader.ReadToEnd();
            var name = entry.FullName;
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry(name).Open());
            writer.Write(xml.Replace("1F2937", "ZZZZZZ", StringComparison.Ordinal));
        }

        stream.Position = 0;
        var result = new XlsxReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        var image = Assert.IsType<RasterImageObject>(Assert.Single(result.Value!.Sheets[0].Objects).Content);
        Assert.Null(image.Duotone);
    }

    [Fact]
    public void A_duotone_colour_that_is_not_hex_is_not_written_as_an_invalid_rgb_value()
    {
        using var stream = Written(WithImage(new ImageDuotone("rosso", "#FDE68A")));

        var xml = Drawing(stream);

        Assert.DoesNotContain("ROSSO", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void A_shape_fill_colour_that_is_not_hex_is_not_written_as_an_invalid_rgb_value()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Objects.Add(new PlacedObject
        {
            Id = "forma",
            Content = new PulseOffice.Drawing.Shapes.ShapeObject
            {
                Id = "forma",
                Shape = PulseOffice.Drawing.Shapes.ShapeKind.Ellipse,
                Style = PulseOffice.Drawing.Shapes.ShapeStyle.Default with { Fill = PulseOffice.Drawing.Shapes.ShapeFill.Default with { ColorHex = "verde" } },
            },
            Anchor = new CellAddress(2, 1),
            Size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3)),
        });
        using var stream = Written(workbook);

        var xml = Drawing(stream);

        Assert.DoesNotContain("VERDE", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void A_vml_anchor_with_an_enormous_pixel_offset_does_not_wrap_to_a_negative_position()
    {
        var box = new XlsxVmlAnchor(new SheetPlacement()).Read(new SheetLayout(), "0, 2000000000, 0, 0, 1, 0, 1, 0");

        Assert.NotNull(box);
        Assert.True(box.Value.Left.Value >= 0);
        Assert.True(box.Value.Width.Value > 0);
    }
}
