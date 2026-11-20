using System.Globalization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round284FormulaEntryTests
{
    private sealed class English : PulseOffice.Sheets.Formatting.ISpreadsheetLocale
    {
        public CultureInfo Culture => CultureInfo.GetCultureInfo("en-US");
    }

    private static readonly FunctionArgumentNames Names = new();
    private static readonly FunctionSignatures Signatures = new();
    private static readonly SheetFill Fill = new(new English(), new PulseOffice.Sheets.Formatting.CalendarFormats());

    [Theory]
    [InlineData("number1", "it", "num1")]
    [InlineData("lookup_value", "de", "Suchkriterium")]
    [InlineData("criteria_range3", "es", "rango_criterios3")]
    [InlineData("value_if_true", "fr", "valeur_si_vrai")]
    [InlineData("text", "en", "text")]
    [InlineData("unknown_name", "it", "unknown_name")]
    public void Argument_names_follow_the_interface_language(string argument, string language, string expected) =>
        Assert.Equal(expected, Names.Localize(argument, language));

    [Fact]
    public void Every_argument_name_of_every_function_has_a_translation()
    {
        var untranslated = FunctionInventory.Required
            .SelectMany(Signatures.Arguments)
            .Where(argument => !Names.Knows(argument))
            .Distinct()
            .ToList();

        Assert.Empty(untranslated);
    }

    [Fact]
    public void The_toggle_fills_each_lane_of_a_one_row_number_seed_dragged_down_as_a_series()
    {
        var sheet = new Worksheet("Data");
        sheet.SetValue(CellAddress.First, CellValue.Number(1));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Number(10));
        var seed = new CellRange(CellAddress.First, new CellAddress(0, 1));

        Assert.Equal(FillMode.Series, Fill.Toggled(sheet, seed, FillDirection.Down));
        Assert.Equal(FillMode.Copy, Fill.Toggled(sheet, seed, FillDirection.Right));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("x"));
        Assert.Equal(FillMode.Copy, Fill.Toggled(sheet, seed, FillDirection.Down));
    }
}
