using System.Globalization;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round253ShownDuplicateTests
{
    private readonly DuplicateRemover _remover = new(new SelectionWorkArea(),
        new CellPresenter(new NumberFormatter(CultureInfo.InvariantCulture, serial => new DateTime(1899, 12, 30).AddDays(serial))));

    [Fact]
    public void Rows_that_show_the_same_text_are_duplicates_even_when_the_stored_values_differ()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        var twoPlaces = workbook.Styles.Intern(workbook.Styles.Get(0) with { NumberFormatId = workbook.NumberFormats.Register("0.00") });
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1.234));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Number(1.2341));
        sheet.SetValue(new CellAddress(2, 0), CellValue.Number(1.236));
        for (var row = 0; row < 3; row++) sheet.SetStyle(new CellAddress(row, 0), twoPlaces);

        var plan = _remover.Plan(sheet, new DuplicateRemovalRequest(new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)), HasHeader: false));

        Assert.Equal([1], plan.RowsToRemove);
    }

    [Fact]
    public void The_same_value_shown_in_two_formats_is_kept_twice()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(0.5));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Number(0.5));
        sheet.SetStyle(new CellAddress(1, 0), workbook.Styles.Intern(workbook.Styles.Get(0) with { NumberFormatId = workbook.NumberFormats.Register("0%") }));

        var plan = _remover.Plan(sheet, new DuplicateRemovalRequest(new CellRange(new CellAddress(0, 0), new CellAddress(1, 0)), HasHeader: false));

        Assert.Empty(plan.RowsToRemove);
    }
}
