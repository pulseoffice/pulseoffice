using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class CellOverflowTests
{
    private static readonly Func<int, double> Width = _ => 10;

    [Fact]
    public void IlTestoCentratoTraboccaDaiDueLati()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.SetValue(new CellAddress(0, 5), CellValue.Text("titolo lungo"));
        sheet.SetValue(new CellAddress(0, 3), CellValue.Number(1));
        var style = CellStyle.Default with { Horizontal = CellHorizontalAlignment.Center };

        var span = new CellOverflow().Span(sheet, new CellAddress(0, 5), style, 50, 60, Width);

        Assert.Equal(40, span.Left);
        Assert.True(span.Right > 60);
    }

    [Fact]
    public void UnaCellaVuotaMaFormattataNonFermaIlTrabocco()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("testo lungo"));
        sheet.SetStyle(new CellAddress(0, 1), 1);

        var span = new CellOverflow().Span(sheet, new CellAddress(0, 0), CellStyle.Default, 0, 10, Width);

        Assert.True(span.Right > 20);
    }

    [Fact]
    public void UnaCellaUnitaFermaIlTrabocco()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("testo lungo"));
        sheet.Merges.Add(new CellRange(new CellAddress(0, 2), new CellAddress(1, 3)));

        var span = new CellOverflow().Span(sheet, new CellAddress(0, 0), CellStyle.Default, 0, 10, Width);

        Assert.Equal(20, span.Right);
    }

    [Fact]
    public void IlTestoRuotatoNonTrabocca()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("testo lungo"));
        var style = CellStyle.Default with { RotationDegrees = 90 };

        var span = new CellOverflow().Span(sheet, new CellAddress(0, 0), style, 0, 10, Width);

        Assert.Equal(new OverflowSpan(0, 10), span);
    }
}
