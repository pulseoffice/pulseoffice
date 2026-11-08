using PulseOffice.Formats.OpenXml;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxCellStyleRound116Tests
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

    [Fact]
    public void A_custom_cell_style_comes_back_into_the_catalog_with_its_look()
    {
        var workbook = Workbook.CreateDefault();
        workbook.NamedStyles.Define("Évidence €", CellStyle.Default with
        {
            BackgroundHex = "#FFE08A",
            Horizontal = CellHorizontalAlignment.Right,
            Font = CellFont.Default with { Bold = true },
        });
        workbook.Sheets[0].SetValue(CellAddress.First, CellValue.Number(1));

        var reread = RoundTrip(workbook);
        var named = reread.NamedStyles.Find("Évidence €");

        Assert.NotNull(named);
        Assert.False(named.IsBuiltIn);
        var style = reread.NamedStyles.StyleOf(named);
        Assert.Equal("#FFE08A", style.BackgroundHex);
        Assert.Equal(CellHorizontalAlignment.Right, style.Horizontal);
        Assert.True(style.Font.Bold);
    }

    [Fact]
    public void Cell_styles_are_not_duplicated_by_the_round_trip()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(CellAddress.First, CellValue.Number(1));

        var reread = RoundTrip(workbook);

        Assert.Equal(workbook.NamedStyles.All.Select(style => style.Name).Order(), reread.NamedStyles.All.Select(style => style.Name).Order());
    }

    [Fact]
    public void A_cell_style_hierarchy_is_declared_lost_in_xlsx()
    {
        var workbook = Workbook.CreateDefault();
        workbook.NamedStyles.Define("Child", CellStyle.Default with { BackgroundHex = "#FFE08A" }, "Heading");

        var losses = new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses;

        Assert.Contains(losses, loss => loss.Code == "XLSX240");
    }
}
