using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class Round152ChartTitleTests
{
    private const string Title = "Revenue\nby  month\twide";
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace Chart = "urn:oasis:names:tc:opendocument:xmlns:chart:1.0";

    [Fact]
    public void Chart_and_axis_titles_keep_breaks_tabs_and_spaces_as_odf_markup()
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(Workbook(), stream);

        stream.Position = 0;
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        {
            var entry = archive.Entries.Single(e => e.FullName.EndsWith("content.xml", StringComparison.Ordinal) && e.FullName.Contains('/'));
            using var content = entry.Open();
            var titles = XDocument.Load(content).Descendants(Chart + "title").ToList();
            Assert.Equal(3, titles.Count);
            foreach (var title in titles)
            {
                Assert.Equal(2, title.Elements(Text + "p").Count());
                Assert.Single(title.Descendants(Text + "tab"));
                Assert.Single(title.Descendants(Text + "s"));
                Assert.DoesNotContain(title.DescendantNodes().OfType<XText>(), text => text.Value.Contains('\n') || text.Value.Contains('\t') || text.Value.Contains("  "));
            }
        }

        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        var chart = Assert.IsType<ChartDefinition>(Assert.Single(result.Value!.Sheets[0].Objects).Content);
        Assert.Equal(Title, chart.ChartTitle);
        Assert.Equal(Title, chart.CategoryAxisTitle);
        Assert.Equal(Title, chart.ValueAxisTitle);
    }

    private static Workbook Workbook()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Data");
        Assert.True(CellAddress.TryParseA1("A1", out var a1));
        Assert.True(CellAddress.TryParseA1("B1", out var b1));
        Assert.True(CellAddress.TryParseA1("B2", out var b2));
        Assert.True(CellAddress.TryParseA1("D3", out var d3));
        sheet[a1] = new Cell { Value = CellValue.Text("Month") };
        sheet[b1] = new Cell { Value = CellValue.Text("Revenue") };
        sheet[b2] = new Cell { Value = CellValue.Number(10) };
        sheet.Objects.Add(new PlacedObject
        {
            Id = "chart1",
            Anchor = d3,
            Content = new ChartDefinition
            {
                Id = "chart1",
                Type = ChartType.Column,
                SourceSheet = "Data",
                SourceRange = "A1:B2",
                ChartTitle = Title,
                CategoryAxisTitle = Title,
                ValueAxisTitle = Title,
            },
        });
        return workbook;
    }
}
