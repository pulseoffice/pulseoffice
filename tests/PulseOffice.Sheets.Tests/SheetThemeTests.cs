using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class SheetThemeTests
{
    [Fact]
    public void IlTemaRidefinisceGliStiliEAggiornaLeCelleCheLiUsano()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        var accent = workbook.NamedStyles.Find("Accent 1")!;
        var plain = new CellAddress(0, 1);
        var styled = new CellAddress(0, 0);
        sheet.SetStyle(styled, workbook.Styles.Intern(workbook.NamedStyles.StyleOf(accent)));
        sheet.SetStyleName(styled, "Accent 1");
        sheet.SetStyle(plain, workbook.Styles.Intern(workbook.NamedStyles.StyleOf(accent)));

        var theme = new PulseOffice.Document.Themes.ThemePalettes().Find("warm")!;
        var count = new SheetThemeApplier(new CellStyleDelta()).Apply(workbook, theme);

        Assert.Equal(1, count);
        var after = workbook.Styles.Get(sheet.StyleIdAt(styled));
        Assert.Equal(theme.Accent1, after.BackgroundHex);
        Assert.Equal(theme.FontFamily, after.Font.Family);
        Assert.Equal("#2563EB", workbook.Styles.Get(sheet.StyleIdAt(plain)).BackgroundHex);
        Assert.Equal(theme.HeadingColor, workbook.NamedStyles.StyleOf(workbook.NamedStyles.Find("Heading 1")!).Font.ColorHex);
    }

    [Fact]
    public void IlTemaConservaLaFormattazioneDirettaDellaCella()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        var accent = workbook.NamedStyles.Find("Accent 1")!;
        var styled = new CellAddress(0, 0);
        var named = workbook.NamedStyles.StyleOf(accent);
        sheet.SetStyle(styled, workbook.Styles.Intern(named with { Font = named.Font with { Bold = !named.Font.Bold }, NumberFormatId = 10 }));
        sheet.SetStyleName(styled, "Accent 1");
        sheet.SetValue(styled, PulseOffice.Sheets.Values.CellValue.Number(1));

        var theme = new PulseOffice.Document.Themes.ThemePalettes().Find("cool")!;
        new SheetThemeApplier(new CellStyleDelta()).Apply(workbook, theme);

        var after = workbook.Styles.Get(sheet.StyleIdAt(styled));
        Assert.Equal(theme.Accent1, after.BackgroundHex);
        Assert.Equal(!named.Font.Bold, after.Font.Bold);
        Assert.Equal(10, after.NumberFormatId);
    }
}

public sealed class SheetThemeCaseTests
{
    [Fact]
    public void DuplicatedNamesFromAFileDoNotBreakTheTheme()
    {
        var workbook = Workbook.CreateDefault();
        workbook.NamedStyles.ReplaceAll([.. workbook.NamedStyles.All, workbook.NamedStyles.Find("Note")!]);
        var theme = new PulseOffice.Document.Themes.ThemePalettes().Find("warm")!;
        new SheetThemeApplier(new CellStyleDelta()).Apply(workbook, theme);
    }
}
