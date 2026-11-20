using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round299ThresholdFormulaTests
{
    [Fact]
    public void Percent_thresholds_taken_from_a_cell_keep_their_kind_in_classic_and_extended_rules()
    {
        var range = new CellRange(new CellAddress(0, 0), new CellAddress(4, 0));
        var percent = new ConditionalThreshold(ConditionalThresholdKind.Percent, 0, "$C$1");
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("S");
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = range, Visual = ConditionalVisual.ColorScale, ScaleColors = ["#FF0000", "#00FF00"],
            Thresholds = [new ConditionalThreshold(ConditionalThresholdKind.Minimum), percent],
        });
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = range, Priority = 1, Visual = ConditionalVisual.IconSet, IconSetName = "3Stars",
            Thresholds = [new ConditionalThreshold(ConditionalThresholdKind.Percent), percent, new ConditionalThreshold(ConditionalThresholdKind.Percent, 67)],
        });

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Value!.Sheets[0].ConditionalRules;

        Assert.All(read, rule => Assert.Equal(percent, rule.Thresholds[1]));
    }
}
