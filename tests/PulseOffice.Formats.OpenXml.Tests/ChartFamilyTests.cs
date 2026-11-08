using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using PulseOffice.Charts;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class ChartFamilyTests
{
    private CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private Workbook With(ChartType type, bool threeDimensional)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet[A("A1")] = new Cell { Value = CellValue.Text("X") };
        sheet[A("B1")] = new Cell { Value = CellValue.Text("Y") };
        for (var i = 0; i < 4; i++)
        {
            sheet[A($"A{i + 2}")] = new Cell { Value = CellValue.Number(i + 1) };
            sheet[A($"B{i + 2}")] = new Cell { Value = CellValue.Number(10 * (i + 1)) };
        }
        sheet.Objects.Add(new PlacedObject
        {
            Id = "g",
            Anchor = A("D1"),
            Content = new ChartDefinition { Id = "g", Type = type, SourceSheet = "Dati", SourceRange = "A1:B5", ThreeDimensional = threeDimensional },
        });
        return workbook;
    }

    private (ChartDefinition Chart, MemoryStream Package) ThroughXlsx(Workbook workbook)
    {
        var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        var read = new XlsxReader().Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics));
        stream.Position = 0;
        return (Assert.IsType<ChartDefinition>(Assert.Single(read.Value!.Sheets[0].Objects).Content), stream);
    }

    private void Valid(MemoryStream stream)
    {
        using var package = SpreadsheetDocument.Open(stream, false);
        var errors = new OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Microsoft365).Validate(package, TestContext.Current.CancellationToken)
            .Where(error => error.Part?.Uri.OriginalString.Contains("/charts/", StringComparison.Ordinal) == true)
            .Select(error => error.Description).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Theory]
    [InlineData(ChartType.Scatter)]
    [InlineData(ChartType.ScatterWithLines)]
    [InlineData(ChartType.Radar)]
    [InlineData(ChartType.FilledRadar)]
    [InlineData(ChartType.ExplodedPie)]
    [InlineData(ChartType.PieOfPie)]
    [InlineData(ChartType.BarOfPie)]
    public void I_tipi_di_grafico_di_DrawingML_si_rileggono_come_se_stessi_in_XLSX(ChartType type)
    {
        var (chart, package) = ThroughXlsx(With(type, false));

        Assert.Equal(type, chart.Type);
        Valid(package);
    }

    [Theory]
    [InlineData(ChartType.Column)]
    [InlineData(ChartType.StackedBar)]
    [InlineData(ChartType.Area)]
    [InlineData(ChartType.Pie)]
    public void I_grafici_3D_restano_3D_in_XLSX(ChartType type)
    {
        var (chart, package) = ThroughXlsx(With(type, true));

        Assert.Equal(type, chart.Type);
        Assert.True(chart.ThreeDimensional);
        Valid(package);
    }

    [Fact]
    public void I_grafici_3D_restano_3D_in_ODS()
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(With(ChartType.Column, true), stream);
        stream.Position = 0;
        var chart = Assert.IsType<ChartDefinition>(Assert.Single(new OdsReader().Read(stream).Value!.Sheets[0].Objects).Content);

        Assert.True(chart.ThreeDimensional);
    }

    [Fact]
    public void I_tipi_senza_equivalente_e_le_linee_3D_si_dichiarano_in_XLSX()
    {
        var writer = new XlsxSpreadsheetWriter();

        Assert.Contains(writer.CheckFidelity(With(ChartType.Waterfall, false)).Losses, loss => loss.Code == "XLSX087");
        Assert.Contains(writer.CheckFidelity(With(ChartType.Line, true)).Losses, loss => loss.Code == "XLSX088");
        Assert.DoesNotContain(writer.CheckFidelity(With(ChartType.Scatter, false)).Losses, loss => loss.Code is "XLSX087" or "XLSX088");
        Assert.DoesNotContain(writer.CheckFidelity(With(ChartType.Column, true)).Losses, loss => loss.Code is "XLSX087" or "XLSX088");
    }

    private Workbook WithColumns(ChartType type, int columns)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        for (var column = 0; column < columns; column++)
        {
            sheet[new CellAddress(0, column)] = new Cell { Value = CellValue.Text($"S{column + 1}") };
            for (var row = 1; row <= 4; row++)
                sheet[new CellAddress(row, column)] = new Cell { Value = CellValue.Number(10 * row + column) };
        }
        sheet.Objects.Add(new PlacedObject
        {
            Id = "g",
            Anchor = A("H1"),
            Content = new ChartDefinition
            {
                Id = "g", Type = type, SourceSheet = "Dati", SourceRange = new CellRange(new CellAddress(0, 0), new CellAddress(4, columns - 1)).ToString(),
                FirstColumnIsCategory = false,
            },
        });
        return workbook;
    }

    [Theory]
    [InlineData(ChartType.Bubble, 3, "bubbleChart")]
    [InlineData(ChartType.StockHighLowClose, 3, "stockChart")]
    [InlineData(ChartType.StockOpenHighLowClose, 4, "stockChart")]
    [InlineData(ChartType.Candlestick, 4, "stockChart")]
    public void Bolle_e_azionari_si_scrivono_nativi_in_DrawingML_e_si_rileggono(ChartType type, int columns, string element)
    {
        var workbook = WithColumns(type, columns);
        var (chart, package) = ThroughXlsx(workbook);

        Assert.Equal(type, chart.Type);
        Assert.Equal("A1:" + new CellAddress(4, columns - 1).ToA1(false, false), chart.SourceRange);
        Valid(package);
        using var document = SpreadsheetDocument.Open(package, false);
        var space = document.WorkbookPart!.WorksheetParts.Single().DrawingsPart!.ChartParts.Single().ChartSpace!;
        Assert.Contains(space.Descendants(), node => node.LocalName == element);
        Assert.Equal(type != ChartType.Bubble, space.Descendants().Any(node => node.LocalName == "chartType"));
        Assert.DoesNotContain(new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "XLSX087");
    }

    [Fact]
    public void Una_bolla_scritta_in_linea_si_rilegge_con_ascisse_valori_e_dimensioni()
    {
        var parts = new InlineChartParts();
        using var stream = new MemoryStream();
        using (var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            var id = parts.Write(main, new ChartDefinition
            {
                Id = "b", Type = ChartType.Bubble, SourceSheet = string.Empty, SourceRange = string.Empty,
                InlineData = new ChartData(["a", "b"], [new ChartSeries("X", [1, 2]), new ChartSeries("Y", [5, 7]), new ChartSeries("Size", [3, 9])]),
            });
            var read = parts.Read(main, id, "b")!;

            Assert.Equal(ChartType.Bubble, read.Type);
            Assert.Equal([1d, 2d], read.InlineData!.Series[0].Values);
            Assert.Equal([5d, 7d], read.InlineData.Series[1].Values);
            Assert.Equal([3d, 9d], read.InlineData.Series[2].Values);
        }
    }
}
