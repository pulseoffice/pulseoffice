using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class Round293ConditionalDxfTests
{
    [Fact]
    public void Underline_strikethrough_and_borders_of_a_conditional_format_survive_xlsx()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Sheet1");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(5));
        var edge = new BorderEdge(BorderStyle.Thin, "#FF0000", new BorderPresets().Pen(BorderStyle.Thin, string.Empty).Width);
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(new CellAddress(0, 0), new CellAddress(0, 0)),
            Operator = ConditionOperator.GreaterThan,
            Operands = ["1"],
            Style = CellStyle.Default with
            {
                Font = CellFont.Default with { DoubleUnderline = true, Strikethrough = true },
                Borders = new CellBorders(edge, BorderEdge.None, BorderEdge.None, edge),
            },
        });

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        var style = Assert.Single(new XlsxReader().Read(stream).Value!.Sheets[0].ConditionalRules).Style;

        Assert.True(style.Font.DoubleUnderline);
        Assert.False(style.Font.Underline);
        Assert.True(style.Font.Strikethrough);
        Assert.Equal(BorderStyle.Thin, style.Borders.Left.Style);
        Assert.Equal("#FF0000", style.Borders.Bottom.ColorHex);
        Assert.False(style.Borders.Top.IsVisible);
    }
}
