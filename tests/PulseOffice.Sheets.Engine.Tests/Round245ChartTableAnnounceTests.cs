using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round245ChartTableAnnounceTests
{
    private static CellAddress A(string a1) => CellAddress.TryParseA1(a1, out var address) ? address : throw new ArgumentException(a1);

    [Fact]
    public void A_table_already_missing_when_the_workbook_opens_is_announced_once()
    {
        var workbook = Workbook.CreateDefault();
        var chart = new ChartDefinition
        {
            Id = "chart1",
            Type = ChartType.Column,
            SourceSheet = workbook.Sheets[0].Name,
            SourceRange = "A1:B2",
            SeriesSources = [new ChartSeriesSource(null, "Gone[Amount]", "Gone[Region]")],
        };
        workbook.Sheets[0].Objects.Add(new PlacedObject { Id = chart.Id, Content = chart, Anchor = A("D2") });
        var watch = new ChartTableLossWatch(workbook);
        var warnings = new List<IReadOnlyList<string>>();
        watch.TablesLost += (_, lost) => warnings.Add(lost.Tables);

        watch.AnnounceStanding();
        watch.Check();

        Assert.Equal(["Gone"], Assert.Single(warnings));
    }
}
