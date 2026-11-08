using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class SheetShapeAutomaticColourTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Il_colore_automatico_del_testo_attraversa_XLSX_e_ODS(bool automatic)
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati").Objects.Add(new PlacedObject
        {
            Id = "Forma",
            Anchor = new PulseOffice.Sheets.Addressing.CellAddress(1, 1),
            Size = new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.FromCentimeters(4), PulseOffice.Foundation.Units.Twip.FromCentimeters(2)),
            Content = new ShapeObject
            {
                Id = "Forma",
                Shape = ShapeKind.Rectangle,
                Text = "Testo",
                Style = ShapeStyle.Default with
                {
                    Fill = ShapeFill.None,
                    Text = ShapeTextStyle.Default with { AutomaticColor = automatic, ColorHex = "#C00000" },
                },
            },
        });

        using var xlsx = new MemoryStream();
        new XlsxWriter().Write(workbook, xlsx);
        var fromXlsx = new XlsxReader().Read(new MemoryStream(xlsx.ToArray())).Value!;
        Assert.Equal(automatic, Text(fromXlsx).AutomaticColor);

        using var ods = new MemoryStream();
        new OdsWriter().Write(fromXlsx, ods);
        var fromOds = new OdsReader().Read(new MemoryStream(ods.ToArray())).Value!;
        Assert.Equal(automatic, Text(fromOds).AutomaticColor);
        if (!automatic) Assert.Equal("#c00000", Text(fromOds).ColorHex, ignoreCase: true);
    }

    private ShapeTextStyle Text(Workbook workbook) =>
        Assert.IsType<ShapeObject>(Assert.Single(workbook.Sheets[0].Objects).Content).Style.Text;
}
