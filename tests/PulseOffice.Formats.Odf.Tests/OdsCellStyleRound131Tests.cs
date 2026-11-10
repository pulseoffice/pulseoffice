using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsCellStyleRound131Tests
{
    private static readonly BorderEdge Thin = new(BorderStyle.Thin, "#112233", Twip.FromPoints(0.5));

    private static Workbook RoundTrip(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private static Workbook Family()
    {
        var workbook = Workbook.CreateDefault();
        workbook.NamedStyles.Define("Base", CellStyle.Default with
        {
            WrapText = true,
            Locked = false,
            Font = CellFont.Default with { Bold = true, Size = 14 },
            Borders = CellBorders.None with { Top = Thin },
        });
        workbook.NamedStyles.Define("Child", CellStyle.Default with { Font = CellFont.Default with { ColorHex = "#FF0000" } }, parent: "Base",
            explicitProperties: CellStyleProperties.Wrap | CellStyleProperties.Locked | CellStyleProperties.Bold | CellStyleProperties.FontColor);
        return workbook;
    }

    [Fact]
    public void A_child_that_switches_off_its_parents_settings_keeps_them_off()
    {
        var reread = RoundTrip(Family());
        var child = reread.NamedStyles.Find("Child")!;
        var style = reread.NamedStyles.StyleOf(child);

        Assert.Equal("Base", child.Parent);
        Assert.False(style.WrapText);
        Assert.True(style.Locked);
        Assert.False(style.Font.Bold);
        Assert.Equal(14, style.Font.Size);
        Assert.Equal("#FF0000", style.Font.ColorHex, ignoreCase: true);
        Assert.Equal(Thin.Style, style.Borders.Top.Style);
        Assert.Equal(CellStyleProperties.Wrap | CellStyleProperties.Locked | CellStyleProperties.Bold | CellStyleProperties.FontColor, child.Explicit);
    }

    [Fact]
    public void A_cell_carrying_the_child_style_keeps_its_look()
    {
        var workbook = Family();
        var child = workbook.NamedStyles.Find("Child")!;
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("x"));
        sheet.SetStyle(CellAddress.First, workbook.Styles.Intern(workbook.NamedStyles.StyleOf(child)));
        sheet.SetStyleName(CellAddress.First, "Child");

        var reread = RoundTrip(workbook);
        var style = reread.Styles.Get(reread.Sheets[0][CellAddress.First].StyleId);

        Assert.False(style.WrapText);
        Assert.True(style.Locked);
        Assert.False(style.Font.Bold);
        Assert.Equal(14, style.Font.Size);
    }
}
