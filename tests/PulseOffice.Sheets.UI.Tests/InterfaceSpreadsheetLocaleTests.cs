using System.Globalization;
using PulseOffice.Localization;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.UI;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class InterfaceSpreadsheetLocaleTests
{
    private static InterfaceSpreadsheetLocale Locale(UiLanguage language, string system) =>
        new(new TextCatalog(language), () => CultureInfo.GetCultureInfo(system));

    [Fact]
    public void AnEnglishInterfaceOnAnItalianSystemUsesTheDecimalPoint()
    {
        var locale = Locale(UiLanguage.English, "it-IT");
        Assert.Equal(".", locale.Culture.NumberFormat.NumberDecimalSeparator);

        var formatter = new LocaleNumberFormatter(locale, serial => DateTime.FromOADate(serial));
        Assert.Equal("1.5", formatter.Format(CellValue.Number(1.5), NumberFormat.General).Text);
    }

    [Fact]
    public void AnItalianInterfaceOnAnEnglishSystemUsesTheDecimalComma()
    {
        var locale = Locale(UiLanguage.Italian, "en-US");
        Assert.Equal(",", locale.Culture.NumberFormat.NumberDecimalSeparator);

        var formatter = new LocaleNumberFormatter(locale, serial => DateTime.FromOADate(serial));
        Assert.Equal("1,5", formatter.Format(CellValue.Number(1.5), NumberFormat.General).Text);
    }

    [Fact]
    public void TheSystemRegionIsKeptWhenItsLanguageIsTheInterfaceLanguage() =>
        Assert.Equal("de-CH", Locale(UiLanguage.German, "de-CH").Culture.Name);

    [Fact]
    public void TheLocaleFollowsALanguageChange()
    {
        var texts = new TextCatalog(UiLanguage.English);
        var locale = new InterfaceSpreadsheetLocale(texts, () => CultureInfo.GetCultureInfo("en-US"));
        Assert.Equal(".", locale.Culture.NumberFormat.NumberDecimalSeparator);
        texts.Use(UiLanguage.French);
        Assert.Equal(",", locale.Culture.NumberFormat.NumberDecimalSeparator);
    }

    [Fact]
    public void PastedPlainTextIsReadWithTheInterfaceLanguage()
    {
        var english = new SheetClipboard(Locale(UiLanguage.English, "it-IT")).FromGrid([["1,5", "2.5"]]);
        Assert.Equal(CellValue.Text("1,5"), english.Cells[0].Cell.Value);
        Assert.Equal(CellValue.Number(2.5), english.Cells[1].Cell.Value);

        var italian = new SheetClipboard(Locale(UiLanguage.Italian, "en-US")).FromGrid([["1,5"]]);
        Assert.Equal(CellValue.Number(1.5), italian.Cells[0].Cell.Value);
    }
}
