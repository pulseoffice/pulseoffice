using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Charts;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;
using X = DocumentFormat.OpenXml.Spreadsheet;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxTenthAuditTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static MemoryStream Write(Workbook workbook)
    {
        var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        return stream;
    }

    private static Workbook Read(Stream stream)
    {
        stream.Position = 0;
        var result = new XlsxReader().Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    [Fact]
    public void UnCollegamentoInternoAUnFoglioConApostrofoTornaFraApici()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati");
        var sheet = workbook.AddSheet("L'anno");
        sheet.SetValue(A("A1"), CellValue.Number(5));
        sheet[A("A1")] = sheet[A("A1")] with { Hyperlink = "#'L''anno'.B2" };

        Assert.Equal("#'L''anno'.B2", Read(Write(workbook)).Sheets[1][A("A1")].Hyperlink);
    }

    [Fact]
    public void LaLarghezzaDelleColonneSiMisuraSulCarattereDelloStileNormale()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Layout.SetColumnWidth(3, Twip.FromPoints(90));
        using var stream = Write(workbook);

        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var column = document.WorkbookPart!.WorksheetParts.First().Worksheet!.Descendants<X.Column>().Single(c => c.Min!.Value == 4);
            Assert.Equal(90 / (10 * 0.507), column.Width!.Value, 1);

            var font = document.WorkbookPart.WorkbookStylesPart!.Stylesheet!.Fonts!.Elements<X.Font>().First();
            font.GetFirstChild<X.FontName>()!.Val = "Arial";
            column.Width = 16.22;
        }

        Assert.Equal(90, Read(stream).Sheets[0].Layout.ColumnWidth(3).ToPoints(), 0);
    }

    [Fact]
    public void UnGraficoSuUnaSolaColonnaNonPuntaFuoriDallIntervallo()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < 4; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
        sheet.Objects.Add(new PlacedObject
        {
            Id = "g",
            Anchor = A("D2"),
            Content = new ChartDefinition { Id = "g", Type = ChartType.Line, SourceSheet = sheet.Name, SourceRange = "A1:A4" },
        });

        var chart = Assert.IsType<ChartDefinition>(Assert.Single(Read(Write(workbook)).Sheets[0].Objects).Content);

        Assert.Equal("A1:A4", chart.SourceRange);
    }

    [Fact]
    public void UnOggettoInUnaColonnaNascostaResta()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.Layout.HideColumn(3);
        sheet.Layout.HideRow(4);
        sheet.Objects.Add(new PlacedObject
        {
            Id = "foto",
            Anchor = A("D5"),
            Content = new RasterImageObject { Id = "foto", Data = Png, MediaType = "image/png" },
        });

        Assert.Equal(A("D5"), Assert.Single(Read(Write(workbook)).Sheets[0].Objects).Anchor);
    }

    [Fact]
    public void LeCostantiScritteComeFormulaDaUnAltraSuiteTornanoCostanti()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(A("A1"), CellValue.FromError(CellError.DivideByZero));
        using var stream = Write(workbook);

        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var cell = document.WorkbookPart!.WorksheetParts.First().Worksheet!.Descendants<X.Cell>().First();
            cell.CellFormula = new X.CellFormula("#DIV/0!");
        }

        var back = Read(stream).Sheets[0][A("A1")];
        Assert.Null(back.Formula);
        Assert.Equal(CellError.DivideByZero, back.Value.Error);
    }

    [Fact]
    public void UnNomeDiFoglioCheExcelRifiutaSiDichiara()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Un nome di foglio davvero troppo lungo");

        Assert.Contains(new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "XLSX038");
    }
}
