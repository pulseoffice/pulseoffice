using PulseOffice.Formats.OpenXml;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxDefaultStyleRoundTripTests
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
    public void GliStiliDiColonnaEDiRigaSopravvivono()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        var bold = workbook.Styles.Intern(CellStyle.Default with { Font = CellStyle.Default.Font with { Bold = true } });
        var yellow = workbook.Styles.Intern(CellStyle.Default with { BackgroundHex = "#FFFF00" });

        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1));
        sheet.Layout.SetColumnStyle(1, bold);
        sheet.Layout.SetRowStyle(4, yellow);

        var reread = RoundTrip(workbook);
        var target = reread.Sheets[0];

        Assert.True(reread.Styles.Get(target.StyleIdAt(new CellAddress(200, 1))).Font.Bold);
        Assert.Equal("#FFFF00", reread.Styles.Get(target.StyleIdAt(new CellAddress(4, 300))).BackgroundHex, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("#FFFF00", reread.Styles.Get(target.StyleIdAt(new CellAddress(4, 1))).BackgroundHex, StringComparer.OrdinalIgnoreCase);
        Assert.False(reread.Styles.Get(target.StyleIdAt(new CellAddress(200, 2))).Font.Bold);
    }

    [Fact]
    public void UnaCellaSempliceEsplicitaInUnaColonnaERigaFormattateResta()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        var bold = workbook.Styles.Intern(CellStyle.Default with { Font = CellStyle.Default.Font with { Bold = true } });
        sheet.Layout.SetColumnStyle(1, bold);
        sheet.Layout.SetRowStyle(6, bold);
        sheet.SetStyle(new CellAddress(3, 1), StyleCatalog.DefaultStyleId);
        sheet.SetValue(new CellAddress(3, 1), CellValue.Number(7));
        sheet.SetStyle(new CellAddress(5, 1), StyleCatalog.DefaultStyleId);
        sheet.SetStyle(new CellAddress(6, 4), StyleCatalog.DefaultStyleId);
        sheet.SetValue(new CellAddress(6, 4), CellValue.Number(2));

        var target = RoundTrip(workbook).Sheets[0];

        Assert.Equal(StyleCatalog.DefaultStyleId, target.StyleIdAt(new CellAddress(3, 1)));
        Assert.Equal(StyleCatalog.DefaultStyleId, target.StyleIdAt(new CellAddress(5, 1)));
        Assert.Equal(StyleCatalog.DefaultStyleId, target.StyleIdAt(new CellAddress(6, 4)));
        Assert.NotEqual(StyleCatalog.DefaultStyleId, target.StyleIdAt(new CellAddress(4, 1)));
        Assert.NotEqual(StyleCatalog.DefaultStyleId, target.StyleIdAt(new CellAddress(6, 5)));
    }
}
