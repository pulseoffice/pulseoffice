using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Charts;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;
using CellValue = PulseOffice.Sheets.Values.CellValue;
using Workbook = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxChartsheetTests
{
    [Fact]
    public void UnFoglioGraficoSiLeggeComeFoglioConIlSuoGrafico()
    {
        var workbook = Workbook.CreateDefault();
        var data = workbook.Sheets[0];
        for (var row = 0; row < 4; row++)
        {
            data.SetValue(new CellAddress(row, 0), CellValue.Text("M" + row));
            data.SetValue(new CellAddress(row, 1), CellValue.Number(row * 3));
        }
        data.Objects.Add(new PlacedObject
        {
            Id = "g",
            Anchor = new CellAddress(0, 3),
            Content = new ChartDefinition { Id = "g", Type = ChartType.Column, SourceSheet = data.Name, SourceRange = "A1:B4" },
        });

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var book = document.WorkbookPart!;
            var worksheet = book.WorksheetParts.First();
            var drawings = worksheet.DrawingsPart!;
            var chartsheet = book.AddNewPart<ChartsheetPart>();
            var drawingId = chartsheet.CreateRelationshipToPart(drawings);
            chartsheet.Chartsheet = new Chartsheet(new ChartSheetViews(new ChartSheetView { WorkbookViewId = 0 }), new DocumentFormat.OpenXml.Spreadsheet.Drawing { Id = drawingId });
            book.Workbook!.Sheets!.AppendChild(new Sheet { Name = "Grafico", SheetId = 99, Id = book.GetIdOfPart(chartsheet) });
            book.Workbook.Save();
        }

        stream.Position = 0;
        var result = new XlsxReader().Read(stream);

        Assert.True(result.Succeeded);
        var sheet = result.Value!.FindSheet("Grafico");
        Assert.NotNull(sheet);
        Assert.IsType<ChartDefinition>(Assert.Single(sheet.Objects).Content);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "XLSX080");
    }
}
