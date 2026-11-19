using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round249TextToColumnsTests
{
    [Fact]
    public void An_empty_part_leaves_a_blank_cell_not_an_empty_text()
    {
        var sheet = new Worksheet("Sheet1");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("a;;b"));

        var plan = new TextToColumns(new PulseOffice.Foundation.Text.DelimitedText())
            .Plan(sheet, new TextToColumnsRequest(new CellRange(new CellAddress(0, 0), new CellAddress(0, 0))));

        Assert.Equal(ValueKind.Empty, plan.Values[new CellAddress(0, 1)].Kind);
        Assert.Equal("b", plan.Values[new CellAddress(0, 2)].AsText());
    }
}
