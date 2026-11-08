using System.Globalization;
using PulseOffice.Document.Forms;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class FormControlLocaleTests
{
    [Theory]
    [InlineData("it-IT", "en-US", "1234.5", "0.1")]
    [InlineData("en-US", "it-IT", "1234,5", "0,1")]
    public void LinkedNumbersFollowTheSpreadsheetLocaleNotTheSystemCulture(string system, string locale, string first, string second)
    {
        var sheet = new Worksheet("Sheet1");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1234.5));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Number(0.1 + 0.2 - 0.2));
        var binding = new FormControlBinding(new FixedSpreadsheetLocale(CultureInfo.GetCultureInfo(locale)));
        var list = new FormControlProperties { SourceRange = "A1:A2" };
        var field = new FormControlProperties { LinkedCell = "A1" };

        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(system);
        try
        {
            Assert.Equal([first, second], binding.ItemsFor(sheet, list));
            Assert.Equal(first, binding.Absorb(sheet, FormControlKind.TextBox, field).Value.Text);
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Theory]
    [InlineData("it-IT", "en-US", "TRUE", "FALSE")]
    [InlineData("en-US", "it-IT", "VERO", "FALSO")]
    public void LinkedBooleansShowTheLocalizedNames(string system, string locale, string yes, string no)
    {
        var sheet = new Worksheet("Sheet1");
        sheet.SetValue(new CellAddress(0, 0), CellValue.True);
        sheet.SetValue(new CellAddress(1, 0), CellValue.False);
        var binding = new FormControlBinding(new FixedSpreadsheetLocale(CultureInfo.GetCultureInfo(locale)));

        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(system);
        try
        {
            Assert.Equal([yes, no], binding.ItemsFor(sheet, new FormControlProperties { SourceRange = "A1:A2" }));
            Assert.Equal(yes, binding.Absorb(sheet, FormControlKind.TextBox, new FormControlProperties { LinkedCell = "A1" }).Value.Text);
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }
}
