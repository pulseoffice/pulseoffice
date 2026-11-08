using System.Globalization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.UI.DataDialogs;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round120FilterMenuTests
{
    [Fact]
    public void Numbers_in_the_filter_list_follow_the_spreadsheet_locale()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Base");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("N"));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Number(0.5));
        sheet.SetValue(new CellAddress(2, 0), CellValue.Number(2));

        var reader = new FilterColumnFactsReader(new FilterValueText(new FixedSpreadsheetLocale(CultureInfo.GetCultureInfo("it-IT"))));
        var facts = reader.Read(sheet, new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)), 0, true, workbook.Styles, workbook.NumberFormats);
        var model = new FilterMenuModel(facts, null);

        Assert.Equal(["0,5", "2"], model.Items.Select(model.Label));
        Assert.Equal(["0.5", "2"], model.Items.Select(item => item.Value.Value));

        model.Search = "0,5";
        Assert.Single(model.Visible);
    }

    [Fact]
    public void While_searching_the_shown_count_and_the_choice_agree_on_blanks()
    {
        var facts = new FilterColumnFacts([new FilterValueCount("Roma", 2), new FilterValueCount("Milano", 1)], 4, false, []);
        var model = new FilterMenuModel(facts, null) { Search = "rom" };

        Assert.False(model.IncludesBlanks);
        Assert.Equal(2, model.ShownRows());

        model.Search = string.Empty;
        Assert.True(model.IncludesBlanks);
        Assert.Equal(7, model.ShownRows());
    }

    [Fact]
    public void The_advanced_filter_preview_counts_rows_through_computed_criteria()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Base");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("N"));
        for (var row = 1; row <= 4; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
        sheet.SetValue(new CellAddress(0, 3), CellValue.Text("Computed"));
        sheet.SetFormula(new CellAddress(1, 3), "=A2>2");

        var probe = new AdvancedFilterProbes(new SheetFilterService(), new SheetRangeReferences()).For(workbook, 0, new AboveTwo());

        Assert.Equal(2, probe.Probe(new AdvancedFilterChoice("A1:A5", "D1:D2", false, string.Empty)).Passing);
    }

    private sealed class AboveTwo : IShiftedFormulaEvaluator
    {
        public CellValue EvaluateShifted(Worksheet sheet, CellAddress formula, int rowDelta) => CellValue.Boolean(rowDelta + 1 > 2);
    }
}
