using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class OpenXmlChartAuditTests
{
    private static Workbook With(string sheetName, ChartDefinition chart)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet(sheetName);
        sheet[new CellAddress(0, 0)] = new Cell { Value = CellValue.Text("X") };
        sheet[new CellAddress(0, 1)] = new Cell { Value = CellValue.Text("Y") };
        for (var row = 1; row < 4; row++)
        {
            sheet[new CellAddress(row, 0)] = new Cell { Value = CellValue.Number(row) };
            sheet[new CellAddress(row, 1)] = new Cell { Value = CellValue.Number(row * 10) };
        }

        sheet.Objects.Add(new PlacedObject { Id = "g", Anchor = new CellAddress(0, 4), Content = chart with { SourceSheet = sheetName } });
        return workbook;
    }

    private static C.ChartSpace Written(Workbook workbook, MemoryStream stream)
    {
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        var document = SpreadsheetDocument.Open(stream, false);
        return document.WorkbookPart!.WorksheetParts.SelectMany(part => part.DrawingsPart?.ChartParts ?? []).Single().ChartSpace!;
    }

    [Fact]
    public void A_sheet_name_with_an_apostrophe_or_a_dash_is_quoted_in_the_chart_formulas()
    {
        using var stream = new MemoryStream();
        var space = Written(With("L'anno-2024", new ChartDefinition { Id = "g", Type = ChartType.Column, SourceSheet = string.Empty, SourceRange = "A1:B4" }), stream);

        var formulas = space.Descendants<C.Formula>().Select(formula => formula.Text).ToList();

        Assert.NotEmpty(formulas);
        Assert.All(formulas, formula => Assert.StartsWith("'L''anno-2024'!", formula, StringComparison.Ordinal));
    }

    [Fact]
    public void A_series_colour_that_is_not_six_hex_digits_is_not_written_as_an_invalid_rgb_value()
    {
        using var stream = new MemoryStream();
        var space = Written(With("Dati", new ChartDefinition { Id = "g", Type = ChartType.Column, SourceSheet = string.Empty, SourceRange = "A1:B4", SeriesColors = ["rosso"] }), stream);

        var colours = space.Descendants<DocumentFormat.OpenXml.Drawing.RgbColorModelHex>().Select(colour => colour.Val?.Value ?? string.Empty).ToList();

        Assert.NotEmpty(colours);
        Assert.All(colours, colour => Assert.Matches("^[0-9A-F]{6}$", colour));
    }

    [Fact]
    public void Declared_point_counts_of_many_inline_series_do_not_allocate_beyond_a_total_budget()
    {
        var parts = new InlineChartParts();
        using var stream = new MemoryStream();
        using var document = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var main = document.AddMainDocumentPart();
        var chart = main.AddNewPart<ChartPart>();
        var bar = new C.BarChart(new C.BarDirection { Val = C.BarDirectionValues.Column });
        for (var index = 0; index < 60; index++)
            bar.Append(new C.BarChartSeries(new C.Index { Val = (uint)index }, new C.Order { Val = (uint)index },
                new C.Values(new C.NumberLiteral(new C.PointCount { Val = 100_000 }))));
        chart.ChartSpace = new C.ChartSpace(new C.Chart(new C.PlotArea(new C.Layout(), bar)));
        chart.ChartSpace.Save();

        var read = parts.Read(main, main.GetIdOfPart(chart), "g")!;

        Assert.True(read.InlineData!.Series.Sum(series => series.Values.Count) <= 1_000_000);
    }

    [Fact]
    public void A_chart_on_a_sheet_whose_name_has_an_apostrophe_reads_back_its_source_sheet()
    {
        var workbook = With("L'anno", new ChartDefinition { Id = "g", Type = ChartType.Column, SourceSheet = string.Empty, SourceRange = "A1:B4" });
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;

        var chart = Assert.IsType<ChartDefinition>(Assert.Single(new XlsxReader().Read(stream).Value!.Sheets[0].Objects).Content);

        Assert.Equal("L'anno", chart.SourceSheet);
        Assert.Equal("A1:B4", chart.SourceRange);
    }

    [Fact]
    public void A_formula_object_on_a_sheet_whose_name_has_a_bar_survives_the_round_trip()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Conti|2024");
        sheet.Objects.Add(new PlacedObject
        {
            Id = "f|1",
            Anchor = new CellAddress(1, 1),
            Content = new PulseOffice.Document.Embedding.FormulaObject { Id = "f|1", Expression = "a over b" },
        });
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;

        var placed = Assert.Single(new XlsxReader().Read(stream).Value!.Sheets[0].Objects);

        Assert.Equal("f|1", placed.Id);
        Assert.Equal("a over b", Assert.IsType<PulseOffice.Document.Embedding.FormulaObject>(placed.Content).Expression);
    }
}
