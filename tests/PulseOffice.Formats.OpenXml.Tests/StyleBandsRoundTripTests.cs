using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class StyleBandsRoundTripTests
{
    private static readonly int LastRow = CellAddress.MaxRows - 1;

    private static Workbook Ods(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private static Workbook Xlsx(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        var result = new XlsxReader().Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private static (Workbook Workbook, int Red, int Bold, int RedBold) Banded(int firstRow, int lastRow)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        var red = workbook.Styles.Intern(CellStyle.Default with { BackgroundHex = "#FF0000" });
        var bold = workbook.Styles.Intern(CellStyle.Default with { Font = CellStyle.Default.Font with { Bold = true } });
        var redBold = workbook.Styles.Intern(workbook.Styles.Get(red) with { Font = CellStyle.Default.Font with { Bold = true } });
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1));
        sheet.Layout.SetColumnStyles(1, 2, red);
        sheet.Layout.SetRowStyles(firstRow, lastRow, bold);
        sheet.Layout.SetStyleBlock(new CellRange(new CellAddress(firstRow, 1), new CellAddress(lastRow, 2)), redBold);
        return (workbook, red, bold, redBold);
    }

    private static void AssertBanded(Workbook workbook, int firstRow, int lastRow)
    {
        var sheet = workbook.Sheets[0];
        CellStyle At(int row, int column) => workbook.Styles.Get(sheet.StyleIdAt(new CellAddress(row, column)));

        Assert.True(At(lastRow, 1).Font.Bold);
        Assert.Equal("#FF0000", At(lastRow, 2).BackgroundHex, StringComparer.OrdinalIgnoreCase);
        Assert.True(At(firstRow, 5).Font.Bold);
        Assert.Null(At(firstRow, 5).BackgroundHex);
        Assert.False(At(firstRow - 1, 1).Font.Bold);
        Assert.Equal("#FF0000", At(firstRow - 1, 1).BackgroundHex, StringComparer.OrdinalIgnoreCase);
        Assert.Null(sheet.OwnStyleAt(new CellAddress(lastRow, 1)));
        Assert.True(sheet.CellCount <= 2, $"{sheet.CellCount} cells");
    }

    [Fact]
    public void WholeRowsOverFormattedColumnsCrossOdsAsRepeatedCellsWithoutCells()
    {
        var (workbook, _, _, _) = Banded(1, LastRow);

        AssertBanded(Ods(workbook), 1, LastRow);
    }

    [Fact]
    public void WholeRowsOverFormattedColumnsCrossXlsxExpandingOnlyTheCrossings()
    {
        var (workbook, _, _, _) = Banded(3, 40);

        var reread = Xlsx(workbook);

        AssertBanded(reread, 3, 40);
        Assert.False(reread.Sheets[0].Layout.StyleBlocks.IsEmpty);
    }

    [Fact]
    public void BandsCrossOdsThenXlsxThenOdsUnchanged()
    {
        var (workbook, _, _, _) = Banded(5, 900);

        var reread = Ods(Xlsx(Ods(workbook)));

        AssertBanded(reread, 5, 900);
        Assert.True(Same(workbook, reread, 5, 900));
    }

    [Fact]
    public void AClearedCrossingCrossesBothFormats()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        var red = workbook.Styles.Intern(CellStyle.Default with { BackgroundHex = "#FF0000" });
        var bold = workbook.Styles.Intern(CellStyle.Default with { Font = CellStyle.Default.Font with { Bold = true } });
        sheet.Layout.SetColumnStyle(3, red);
        sheet.Layout.SetRowStyles(10, 20, bold);
        sheet.Layout.SetStyleBlock(new CellRange(new CellAddress(10, 3), new CellAddress(20, 3)), StyleCatalog.DefaultStyleId);

        foreach (var reread in new[] { Ods(workbook), Xlsx(workbook) })
        {
            var target = reread.Sheets[0];
            Assert.Equal(StyleCatalog.DefaultStyleId, target.StyleIdAt(new CellAddress(15, 3)));
            Assert.True(reread.Styles.Get(target.StyleIdAt(new CellAddress(15, 4))).Font.Bold);
            Assert.Equal("#FF0000", reread.Styles.Get(target.StyleIdAt(new CellAddress(21, 3))).BackgroundHex, StringComparer.OrdinalIgnoreCase);
        }
    }

    private static bool Same(Workbook left, Workbook right, int firstRow, int lastRow)
    {
        foreach (var row in new[] { 0, firstRow - 1, firstRow, (firstRow + lastRow) / 2, lastRow, lastRow + 1 })
            foreach (var column in new[] { 0, 1, 2, 3, 100 })
            {
                var address = new CellAddress(row, column);
                if (left.Styles.Get(left.Sheets[0].StyleIdAt(address)) != right.Styles.Get(right.Sheets[0].StyleIdAt(address))) return false;
            }

        return true;
    }
}
