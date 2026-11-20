using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class Round294ConditionalExtrasTests
{
    private static readonly CellRange Range = new(new CellAddress(0, 0), new CellAddress(4, 0));

    [Fact]
    public void Options_without_an_odf_attribute_survive_ods_in_the_pulseoffice_extension()
    {
        var workbook = new Workbook();
        var rules = workbook.AddSheet("Data").ConditionalRules;
        rules.Add(new ConditionalRule
        {
            Range = Range, Priority = 0, Operator = ConditionOperator.AboveAverage, StandardDeviations = 2, StopIfTrue = true,
            Style = CellStyle.Default with { BackgroundHex = "#FF0000" },
        });
        rules.Add(new ConditionalRule { Range = Range, Priority = 1, Visual = ConditionalVisual.IconSet, IconSetName = "3Arrows", ReverseIcons = true });
        rules.Add(new ConditionalRule
        {
            Range = Range, Priority = 2, Visual = ConditionalVisual.DataBar,
            Bar = new DataBarOptions { BorderColorHex = "#112233", NegativeBorderColorHex = "#445566", Direction = DataBarDirection.RightToLeft },
        });

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        var read = new OdsReader().Read(stream).Value!.Sheets[0].ConditionalRules.OrderBy(rule => rule.Priority).ToList();

        Assert.Equal(2, read[0].StandardDeviations);
        Assert.True(read[0].StopIfTrue);
        Assert.True(read[1].ReverseIcons);
        Assert.Equal("#112233", read[2].Bar.BorderColorHex);
        Assert.Equal("#445566", read[2].Bar.NegativeBorderColorHex);
        Assert.Equal(DataBarDirection.RightToLeft, read[2].Bar.Direction);
    }
}
