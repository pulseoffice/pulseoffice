using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class XlsxHyperlinkTests
{
    [Fact]
    public void IlCollegamentoDellaCellaSopravvive()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet[CellAddress.First] = new Cell { Value = CellValue.Text("Sito"), Hyperlink = "https://example.org/" };

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;

        var cell = new XlsxReader().Read(stream).Value!.Sheets[0][CellAddress.First];

        Assert.Equal("https://example.org/", cell.Hyperlink);
        Assert.Equal("Sito", cell.Value.AsText());
    }
}
