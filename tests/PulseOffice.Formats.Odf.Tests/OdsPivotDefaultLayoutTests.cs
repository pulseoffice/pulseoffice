using System.IO.Compression;
using System.Text.RegularExpressions;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsPivotDefaultLayoutTests
{
    [Fact]
    public void A_pivot_without_layout_info_is_tabular_as_the_specification_says()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Region"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Amount"));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Text("North"));
        sheet.SetValue(new CellAddress(1, 1), CellValue.Number(4));
        workbook.Pivots.Add(new PivotDefinition
        {
            Id = "p",
            Name = "Summary",
            SourceSheet = sheet.Name,
            SourceRange = new CellRange(CellAddress.First, new CellAddress(1, 1)),
            DestinationSheet = sheet.Name,
            Destination = new CellAddress(0, 4),
            OutputRange = new CellRange(new CellAddress(0, 4), new CellAddress(2, 5)),
            Layout = PivotLayout.Compact,
            Fields =
            [
                new PivotField { SourceName = "Region", SourceColumnIndex = 0, Area = PivotArea.Row },
                new PivotField { SourceName = "Amount", SourceColumnIndex = 1, Area = PivotArea.Value },
            ],
        });

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            string content;
            using (var reader = new StreamReader(entry.Open())) content = reader.ReadToEnd();
            Assert.Contains("data-pilot-layout-info", content, StringComparison.Ordinal);
            content = Regex.Replace(content, "<table:data-pilot-layout-info[^>]*/>", string.Empty);
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("content.xml").Open());
            writer.Write(content);
        }

        stream.Position = 0;
        var read = new OdsReader().Read(stream).Value!;

        Assert.Equal(PivotLayout.Tabular, Assert.Single(read.Pivots).Layout);
    }
}
