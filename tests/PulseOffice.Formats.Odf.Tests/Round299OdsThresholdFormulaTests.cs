using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class Round299OdsThresholdFormulaTests
{
    [Fact]
    public void A_percentile_threshold_taken_from_a_cell_survives_the_round_trip()
    {
        var workbook = Workbook.CreateDefault();
        var percentile = new ConditionalThreshold(ConditionalThresholdKind.Percentile, 0, "$C$1");
        workbook.Sheets[0].ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(new CellAddress(0, 0), new CellAddress(4, 0)),
            Visual = ConditionalVisual.ColorScale, ScaleColors = ["#FF0000", "#00FF00"],
            Thresholds = [new ConditionalThreshold(ConditionalThresholdKind.Minimum), percentile],
        });

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;

        Assert.Equal(percentile, Assert.Single(new OdsReader().Read(stream).Value!.Sheets[0].ConditionalRules).Thresholds[1]);
    }
}
