using System.Globalization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round280FillModeTests
{
    private sealed class English : PulseOffice.Sheets.Formatting.ISpreadsheetLocale
    {
        public CultureInfo Culture => CultureInfo.GetCultureInfo("en-US");
    }

    private static readonly SheetFill Fill = new(new English(), new PulseOffice.Sheets.Formatting.CalendarFormats());
    private static readonly CellRange Seed = new(CellAddress.First, new CellAddress(1, 0));
    private static readonly CellRange Target = new(CellAddress.First, new CellAddress(3, 0));

    private static Worksheet TwoCells(CellValue first, CellValue second)
    {
        var sheet = new Worksheet("Data");
        sheet.SetValue(CellAddress.First, first);
        sheet.SetValue(new CellAddress(1, 0), second);
        return sheet;
    }

    [Fact]
    public void Numbered_text_with_a_two_cell_seed_steps_by_the_difference_of_its_numbers()
    {
        var sheet = TwoCells(CellValue.Text("Item 1"), CellValue.Text("Item 3"));

        var filled = Fill.Extend(sheet, Seed, Target, FillDirection.Down);

        Assert.Equal(["Item 5", "Item 7"], filled.Select(pair => pair.Cell.Value.AsText()));
    }

    [Fact]
    public void Copy_mode_repeats_a_series_seed()
    {
        var sheet = TwoCells(CellValue.Number(1), CellValue.Number(2));

        var filled = Fill.Extend(sheet, Seed, Target, FillDirection.Down, FillMode.Copy);

        Assert.Equal([1d, 2d], filled.Select(pair => pair.Cell.Value.AsNumber()));
    }

    [Fact]
    public void The_toggle_turns_a_single_number_into_a_series_and_anything_else_into_a_copy()
    {
        var sheet = TwoCells(CellValue.Number(7), CellValue.Number(8));
        var single = new CellRange(CellAddress.First, CellAddress.First);

        Assert.Equal(FillMode.Series, Fill.Toggled(sheet, single, FillDirection.Right));
        Assert.Equal(FillMode.Copy, Fill.Toggled(sheet, Seed, FillDirection.Down));
        var series = Fill.Extend(sheet, single, new CellRange(CellAddress.First, new CellAddress(0, 2)), FillDirection.Right, FillMode.Series);
        Assert.Equal([8d, 9d], series.Select(pair => pair.Cell.Value.AsNumber()));
    }

    [Fact]
    public void Formatting_only_and_without_formatting_split_values_from_styles()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Number(1));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Number(2));
        var bold = workbook.Styles.Intern(CellStyle.Default with { NumberFormatId = 10 });
        sheet.SetStyle(CellAddress.First, bold);
        sheet.SetStyle(new CellAddress(1, 0), bold);
        sheet.SetValue(new CellAddress(2, 0), CellValue.Text("kept"));

        var formats = Fill.Extend(sheet, Seed, Target, FillDirection.Down, FillMode.FormattingOnly);
        var values = Fill.Extend(sheet, Seed, Target, FillDirection.Down, FillMode.WithoutFormatting);

        Assert.Equal("kept", formats[0].Cell.Value.AsText());
        Assert.Equal(bold, formats[0].Cell.StyleId);
        Assert.Equal(3d, values[0].Cell.Value.AsNumber());
        Assert.NotEqual(bold, values[0].Cell.StyleId);
    }
}
