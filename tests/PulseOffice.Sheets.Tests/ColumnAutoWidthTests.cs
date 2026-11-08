using System.Globalization;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class ColumnAutoWidthTests
{
    private sealed class FixedMeasure : ICellTextMeasure
    {
        public TextExtent Measure(string text, CellFont font, double? wrapWidth, int rotationDegrees) =>
            new(text.Length * 6.0, font.Size * 1.3);
    }

    private static IColumnAutoWidth Service() =>
        new ColumnAutoWidth(new FixedMeasure(), new CellPresenter(
            new NumberFormatter(CultureInfo.InvariantCulture,
                serial => new DateOnly(1899, 12, 30).ToDateTime(TimeOnly.MinValue).AddDays(serial))));

    private static readonly CellRange Everything = new(new CellAddress(0, 0), new CellAddress(1000, 100));

    [Fact]
    public void LaLarghezzaSegueIlTestoPiuLungo()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("ab"));
        sheet.SetValue(new CellAddress(1, 1), CellValue.Text(new string('x', 40)));

        var width = Service().Measure(workbook, sheet, 1, Everything, 0)!.Value;

        Assert.Equal(Math.Ceiling((40 * 6.0 + 6) / CellBoxMetrics.PixelsPerTwip), width.Value);
    }

    [Fact]
    public void LeRigheNascosteELeColonneVuoteNonContano()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(3, 0), CellValue.Text(new string('x', 40)));
        sheet.Layout.HideRow(3);

        Assert.Null(Service().Measure(workbook, sheet, 0, Everything, 0));
        Assert.Null(Service().Measure(workbook, sheet, 2, Everything, 0));
    }
}
