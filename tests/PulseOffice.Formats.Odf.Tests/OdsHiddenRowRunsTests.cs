using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsHiddenRowRunsTests
{
    private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";

    [Fact]
    public void TenThousandHiddenRowsAreOneRepeatedRowAndAllComeBackHidden()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1));
        sheet.Layout.SetRowHeight(5, Twip.FromPoints(30));
        sheet.Layout.HideRows(1, 10_000);

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);

        stream.Position = 0;
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        using (var entry = archive.GetEntry("content.xml")!.Open())
        {
            var rows = XDocument.Load(entry).Descendants(Table + "table-row").Count();
            Assert.True(rows < 10, $"{rows} rows written");
        }

        stream.Position = 0;
        var back = new OdsReader().Read(stream).Value!.Sheets[0];

        Assert.False(back.Layout.IsRowHidden(0));
        Assert.True(back.Layout.IsRowHidden(1));
        Assert.True(back.Layout.IsRowHidden(10_000));
        Assert.False(back.Layout.IsRowHidden(10_001));
        back.Layout.ShowRow(5);
        Assert.Equal(30, back.Layout.RowHeight(5).ToPoints(), 1);
    }
}
