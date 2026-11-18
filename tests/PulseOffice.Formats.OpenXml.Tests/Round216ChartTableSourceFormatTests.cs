using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Charts;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round216ChartTableSourceFormatTests
{
    private static CellAddress A(string a1) => CellAddress.TryParseA1(a1, out var address) ? address : throw new ArgumentException(a1);

    private static Workbook Scene()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Data");
        sheet[A("A1")] = new Cell { Value = CellValue.Text("Region") };
        sheet[A("B1")] = new Cell { Value = CellValue.Text("Amount") };
        for (var row = 2; row <= 3; row++)
        {
            sheet[A($"A{row}")] = new Cell { Value = CellValue.Text("R" + row) };
            sheet[A($"B{row}")] = new Cell { Value = CellValue.Number(row) };
        }
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Sales", SheetIndex = 0, Range = new CellRange(A("A1"), A("B3")) });
        var chart = new ChartDefinition
        {
            Id = "chart1",
            Type = ChartType.Column,
            SourceSheet = "Data",
            SourceRange = "A1:B3",
            SeriesSources = [new ChartSeriesSource(null, "Sales[Amount]", "Sales[Region]")],
        };
        sheet.Objects.Add(new PlacedObject { Id = chart.Id, Content = chart, Anchor = A("E2") });
        return workbook;
    }

    private static ChartSeriesSource SourceOf(Workbook workbook) =>
        Assert.Single(Assert.IsType<ChartDefinition>(Assert.Single(workbook.Sheets[0].Objects).Content).SeriesSources!);

    [Fact]
    public void Xlsx_writes_ranges_in_the_series_and_reads_the_table_references_back()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(Scene(), stream);

        stream.Position = 0;
        using (var package = SpreadsheetDocument.Open(stream, false))
        {
            var formulas = package.WorkbookPart!.WorksheetParts.SelectMany(part => part.DrawingsPart?.ChartParts ?? [])
                .SelectMany(part => part.ChartSpace!.Descendants<C.Formula>()).Select(formula => formula.Text).ToList();
            Assert.NotEmpty(formulas);
            Assert.DoesNotContain(formulas, formula => formula.Contains('[', StringComparison.Ordinal));
        }

        stream.Position = 0;
        var source = SourceOf(new XlsxReader().Read(stream).Value!);
        Assert.Equal("Sales[Amount]", source.Values);
        Assert.Equal("Sales[Region]", source.Categories);
    }

    [Fact]
    public void Ods_reads_the_table_references_back()
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(Scene(), stream);
        stream.Position = 0;

        var source = SourceOf(new OdsReader().Read(stream).Value!);

        Assert.Equal("Sales[Amount]", source.Values);
        Assert.Equal("Sales[Region]", source.Categories);
    }
}
