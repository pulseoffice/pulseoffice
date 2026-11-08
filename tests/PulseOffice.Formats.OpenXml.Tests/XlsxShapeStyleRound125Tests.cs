using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class XlsxShapeStyleRound125Tests
{
    private static Workbook With(ShapeStyle style)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Data");
        sheet.Objects.Add(new PlacedObject
        {
            Id = "s1",
            Content = new ShapeObject { Id = "s1", Shape = ShapeKind.Rectangle, Style = style, Text = "Hallo" },
            Anchor = new CellAddress(1, 1),
            Size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3)),
        });
        return workbook;
    }

    private static ShapeStyle RoundTrip(ShapeStyle style)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(With(style), stream);
        stream.Position = 0;
        var result = new XlsxReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return Assert.IsType<ShapeObject>(Assert.Single(result.Value!.Sheets[0].Objects).Content).Style;
    }

    [Fact]
    public void The_language_of_shape_text_survives_and_is_not_forced_to_english()
    {
        var style = RoundTrip(ShapeStyle.Default with { Text = ShapeTextStyle.Default with { Language = "de-DE" } });

        Assert.Equal("de-DE", style.Text.Language);
    }

    [Fact]
    public void Shape_text_without_a_language_is_not_written_as_english()
    {
        var run = new XlsxShapeStyleMapper().Run(ShapeTextStyle.Default);

        Assert.DoesNotContain("lang=", run.OuterXml, StringComparison.Ordinal);
    }

    [Fact]
    public void A_huge_line_width_is_capped_instead_of_overflowing()
    {
        var outline = new XlsxShapeStyleMapper().Line(ShapeLine.Default with { Width = new Twip(int.MaxValue) });

        Assert.InRange(outline.Width!.Value, 0, 20116800);
    }
}
