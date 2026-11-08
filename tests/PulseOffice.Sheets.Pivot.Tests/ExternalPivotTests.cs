using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Pivot.Tests;

public class ExternalPivotTests
{
    private readonly PivotLabels _labels = new("Grand total", "Total {0}");

    private readonly PivotEngine _engine = new(new PivotAggregator(), new PivotGrouping(), new PivotFormulas(new PulseOffice.Sheets.Engine.Parsing.FormulaParser()));

    private IPivotCoordinator Coordinator() => new PivotCoordinator(_engine, new PivotRenderer(), new PivotSources());

    private PivotRecordCache Cache() => new()
    {
        Headers = ["Region", "Amount"],
        Records =
        [
            [CellValue.Text("North"), CellValue.Number(100)],
            [CellValue.Text("South"), CellValue.Number(20)],
            [CellValue.Text("North"), CellValue.Number(30)],
        ],
    };

    private PivotDefinition Definition(Workbook workbook, PivotRecordCache cache) => new()
    {
        Id = "x",
        Name = "Sales",
        SourceSheet = string.Empty,
        SourceRange = default,
        ExternalSource = new PivotExternalSource { Kind = PivotExternalKind.Sql, DatabaseName = "Sales", Command = "SELECT * FROM sales", Cache = cache },
        DestinationSheet = workbook.Sheets[0].Name,
        Destination = CellAddress.First,
        Fields =
        [
            new PivotField { SourceName = "Region", SourceColumnIndex = 0, Area = PivotArea.Row },
            new PivotField { SourceName = "Amount", SourceColumnIndex = 1, Area = PivotArea.Value },
        ],
    };

    [Fact]
    public void ThePivotIsComputedFromItsCachedRecords()
    {
        var workbook = Workbook.CreateDefault();

        var result = Coordinator().Apply(workbook, Definition(workbook, Cache()), _labels);

        Assert.True(result.Succeeded);
        var sheet = workbook.Sheets[0];
        var cells = result.Value.Cells().Select(address => sheet.GetValue(address)).ToList();
        Assert.Contains(CellValue.Number(130), cells);
        Assert.Contains(CellValue.Number(150), cells);
    }

    [Fact]
    public void WithoutCachedRecordsTheLastResultStaysAndTheDefinitionIsKept()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(1, 1), CellValue.Number(42));
        var definition = Definition(workbook, PivotRecordCache.Empty) with { OutputRange = new CellRange(CellAddress.First, new CellAddress(2, 1)) };
        workbook.Pivots.Add(definition);

        var result = Coordinator().RefreshAll(workbook, _labels);

        Assert.True(result.Succeeded);
        Assert.Equal(CellValue.Number(42), sheet.GetValue(new CellAddress(1, 1)));
        Assert.Single(workbook.Pivots);
    }

    [Fact]
    public void GetPivotDataReadsTheCachedRecords()
    {
        var workbook = Workbook.CreateDefault();
        Coordinator().Apply(workbook, Definition(workbook, Cache()), _labels);
        var lookup = new PivotDataLookup(workbook, _engine, _labels, new PivotSources());

        var value = lookup.Lookup(0, CellAddress.First, "Amount", [("Region", "North")]);

        Assert.Equal(CellValue.Number(130), value);
    }
}
