using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsCellStyleRound123Tests
{
    private static Workbook RoundTrip(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    [Fact]
    public void The_properties_of_the_default_style_survive_a_round_trip()
    {
        var workbook = Workbook.CreateDefault();
        workbook.NamedStyles.Define("Default", CellStyle.Default with { BackgroundHex = "#FFE08A", Font = CellFont.Default with { Bold = true } }, builtIn: true);

        var reread = RoundTrip(workbook);
        var style = reread.Styles.Get(reread.NamedStyles.Find("Default")!.StyleId);

        Assert.Equal("#FFE08A", style.BackgroundHex, ignoreCase: true);
        Assert.True(style.Font.Bold);
        Assert.Null(reread.NamedStyles.Find("Default")!.Parent);
    }

    [Fact]
    public void A_cell_can_reset_the_language_of_its_named_style()
    {
        var workbook = Workbook.CreateDefault();
        workbook.NamedStyles.Define("German", CellStyle.Default with { Language = "de-DE" });
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("x"));
        sheet.SetStyle(CellAddress.First, workbook.Styles.Intern(CellStyle.Default with { BackgroundHex = "#EEEEEE" }));
        sheet.SetStyleName(CellAddress.First, "German");

        var reread = RoundTrip(workbook);
        var style = reread.Styles.Get(reread.Sheets[0][CellAddress.First].StyleId);

        Assert.NotEqual("de-DE", style.Language);
        Assert.Equal("German", reread.Sheets[0][CellAddress.First].StyleName);
        Assert.Equal("de-DE", reread.Styles.Get(reread.NamedStyles.Find("German")!.StyleId).Language);
    }

    [Fact]
    public void A_general_cell_under_a_named_style_with_a_number_format_stays_general()
    {
        var workbook = Workbook.CreateDefault();
        var percent = workbook.NumberFormats.Register("0.00%");
        workbook.NamedStyles.Define("Percent", CellStyle.Default with { NumberFormatId = percent });
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Number(0.5));
        sheet.SetStyle(CellAddress.First, workbook.Styles.Intern(CellStyle.Default with { BackgroundHex = "#EEEEEE" }));
        sheet.SetStyleName(CellAddress.First, "Percent");

        var reread = RoundTrip(workbook);
        var style = reread.Styles.Get(reread.Sheets[0][CellAddress.First].StyleId);

        Assert.Equal(0, style.NumberFormatId);
        Assert.Equal("Percent", reread.Sheets[0][CellAddress.First].StyleName);
        Assert.NotEqual(0, reread.Styles.Get(reread.NamedStyles.Find("Percent")!.StyleId).NumberFormatId);
    }
}
