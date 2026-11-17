using System.Globalization;
using PulseOffice.Formats.Interchange.Text.Html;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests.Text;

public sealed class Round148HtmlMergeTests
{
    private readonly HtmlSpreadsheetWriter _writer = new(new CellPresenter(new NumberFormatter(CultureInfo.InvariantCulture, serial => DateTime.FromOADate(serial))));

    [Fact]
    public void A_merge_starting_in_a_hidden_row_keeps_its_text_and_span()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Sheet1");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("merged"));
        sheet.Merges.Add(new CellRange(new CellAddress(0, 0), new CellAddress(2, 1)));
        sheet.SetValue(new CellAddress(0, 2), CellValue.Text("x"));
        sheet.Layout.HideRow(0);

        var html = _writer.RenderFragment(sheet, new CellRange(new CellAddress(0, 0), new CellAddress(2, 2)), workbook.Styles);

        Assert.Contains(">merged<", html, StringComparison.Ordinal);
        Assert.Contains("colspan=\"2\" rowspan=\"2\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_merge_starting_in_a_hidden_column_keeps_its_text()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Sheet1");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("merged"));
        sheet.Merges.Add(new CellRange(new CellAddress(0, 0), new CellAddress(0, 2)));
        sheet.Layout.HideColumn(0);

        var html = _writer.RenderFragment(sheet, new CellRange(new CellAddress(0, 0), new CellAddress(0, 2)), workbook.Styles);

        Assert.Contains(">merged<", html, StringComparison.Ordinal);
        Assert.Contains("colspan=\"2\"", html, StringComparison.Ordinal);
    }
}
