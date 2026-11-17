using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace PulseOffice.Formats.OpenXml.Tests;

public class Round152ChartTitleTests
{
    private const string Title = "Revenue\nby month";

    [Fact]
    public void Chart_and_axis_titles_with_line_breaks_become_paragraphs_and_come_back()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(Workbook(), stream);

        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            var chartPart = document.WorkbookPart!.WorksheetParts.SelectMany(part => part.DrawingsPart?.ChartParts ?? []).Single();
            var titles = chartPart.ChartSpace!.Descendants<C.Title>().ToList();
            Assert.Equal(3, titles.Count);
            foreach (var title in titles)
            {
                Assert.Equal(2, title.Descendants<A.Paragraph>().Count());
                Assert.DoesNotContain(title.Descendants<A.Text>(), text => text.Text.Contains('\n'));
            }
        }

        stream.Position = 0;
        var result = new XlsxReader().Read(stream);
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
