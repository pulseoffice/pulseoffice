using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using X = DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxAuditRoundTripTests
{
    private static Workbook RoundTrip(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        var result = new XlsxReader().Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    [Fact]
    public void LeStringheInLineaEIRisultatiDiTestoSiLeggono()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(Workbook.CreateDefault(), stream);

        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var data = document.WorkbookPart!.WorksheetParts.First().Worksheet!.GetFirstChild<X.SheetData>()!;
            data.RemoveAllChildren();
            data.Append(new X.Row(
                new X.Cell(new X.InlineString(new X.Text("ciao"))) { CellReference = "A1", DataType = X.CellValues.InlineString },
                new X.Cell(new X.CellFormula("A1&\"x\""), new X.CellValue("ciaox")) { CellReference = "B1", DataType = X.CellValues.String })
            { RowIndex = 1 });
        }

        stream.Position = 0;
        var sheet = new XlsxReader().Read(stream).Value!.Sheets[0];

        Assert.Equal("ciao", sheet[A("A1")].Value.AsText());
        Assert.Equal("ciaox", sheet[A("B1")].Value.AsText());
    }

    [Fact]
    public void INomiDefinitiEIFogliConApostrofoSopravvivono()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("L'anno");
        sheet.SetValue(A("A1"), CellValue.Text("  spazi  "));
        sheet.PrintRanges.Add(new CellRange(A("A1"), A("B2")));
        workbook.DefineName(new DefinedName("Totali", "'L''anno'.$A$1:$A$10"));

        var back = RoundTrip(workbook);

        Assert.Equal("'L''anno'.$A$1:$A$10", Assert.Single(back.DefinedNames).Expression);
        Assert.Single(back.Sheets[0].PrintRanges);
        Assert.Equal("  spazi  ", back.Sheets[0][A("A1")].Value.AsText());
    }

    [Fact]
    public void RigheNascosteEVuoteEGruppiDiColonneChiusiSopravvivono()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(A("A1"), CellValue.Number(1));
        sheet.SetValue(A("A10"), CellValue.Number(10));
        sheet.Layout.HideRow(0);
        sheet.Layout.SetRowHeight(4, PulseOffice.Foundation.Units.Twip.FromPoints(30));
        sheet.RowGroups.Add(new OutlineGroup { First = 5, Last = 9, Level = 1 });
        sheet.ColumnGroups.Add(new OutlineGroup { First = 1, Last = 3, Level = 1, IsCollapsed = true });
        for (var column = 1; column <= 3; column++) sheet.Layout.HideColumn(column);

        var back = RoundTrip(workbook).Sheets[0];

        Assert.True(back.Layout.IsRowHidden(0));
        Assert.Equal(30, back.Layout.RowHeight(4).ToPoints(), 1);
        var rows = Assert.Single(back.RowGroups);
        Assert.Equal((5, 9), (rows.First, rows.Last));
        var columns = Assert.Single(back.ColumnGroups);
        Assert.True(columns.IsCollapsed);
    }

    [Fact]
    public void ICommentiSopravvivono()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Comments[A("C5")] = new CellComment { Text = "riga uno\nriga due", Author = "Ada" };

        var back = RoundTrip(workbook).Sheets[0];

        Assert.Equal("riga uno\nriga due", back.Comments[A("C5")].Text);
        Assert.Equal("Ada", back.Comments[A("C5")].Author);
    }
}
