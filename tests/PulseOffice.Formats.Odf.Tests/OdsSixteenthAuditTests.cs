using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsSixteenthAuditTests
{
    private static Workbook RoundTrip(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        return new OdsReader().Read(stream).Value!;
    }

    [Fact]
    public void UnaMisuraPredefinitaManualeNonDiventaOttimale()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(2, 0), CellValue.Text("a"));
        sheet.Layout.DefaultRowHeight = Twip.FromCentimeters(3);
        sheet.Layout.DefaultRowHeightManual = true;

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        using (var content = zip.GetEntry("content.xml")!.Open())
        {
            var rows = XDocument.Load(content).Descendants()
                .Where(element => element.Name.LocalName == "table-row-properties").ToList();
            Assert.All(rows, row => Assert.Equal("false", (string?)row.Attributes().First(a => a.Name.LocalName == "use-optimal-row-height")));
        }

        stream.Position = 0;
        var layout = new OdsReader().Read(stream).Value!.Sheets[0].Layout;
        Assert.True(layout.DefaultRowHeightManual);
        Assert.Equal(3, layout.DefaultRowHeight.ToCentimeters(), 2);
        Assert.Equal(3, layout.RowHeight(2).ToCentimeters(), 2);
    }

    [Fact]
    public void UnaMisuraPredefinitaOttimaleRestaOttimale()
    {
        var layout = RoundTrip(Workbook.CreateDefault()).Sheets[0].Layout;
        Assert.False(layout.DefaultRowHeightManual);
    }

    [Fact]
    public void UnaQuotaDiCalcSiLeggeComeLinea()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Calc", "LargeAnchorOffset.ods");
        using var stream = File.OpenRead(path);
        var sheet = new OdsReader().Read(stream).Value!.Sheets[0];

        var line = Assert.Single(sheet.Objects);
        Assert.Equal(ShapeKind.Line, Assert.IsType<ShapeObject>(line.Content).Shape);
        Assert.Equal(8, line.Size.Width.ToCentimeters(), 1);
    }

    [Fact]
    public void ConPredefinitoManualeLeRigheOttimaliRestanoOttimali()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(4, 0), CellValue.Text("a"));
        sheet.Layout.DefaultRowHeight = Twip.FromPoints(30);
        sheet.Layout.DefaultRowHeightManual = true;
        sheet.Layout.MakeRowOptimal(2);
        sheet.Layout.SetAutomaticRowHeight(3, Twip.FromPoints(50));

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        var layout = new OdsReader().Read(stream).Value!.Sheets[0].Layout;

        Assert.True(layout.DefaultRowHeightManual);
        Assert.True(layout.IsRowAutomatic(2));
        Assert.True(layout.IsRowAutomatic(3));
        Assert.Equal(50, layout.RowHeight(3).ToPoints(), 1);
        Assert.False(layout.IsRowAutomatic(4));
        Assert.False(layout.IsRowAutomatic(1));
    }
}
