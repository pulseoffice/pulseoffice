using System.Diagnostics;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Pivot.Tests;

public sealed class PivotLimitsTests
{
    private static readonly PivotLabels Labels = new("Total", "Total {0}");

    private static PivotEngine Engine() =>
        new(new PivotAggregator(), new PivotGrouping(), new PivotFormulas(new PulseOffice.Sheets.Engine.Parsing.FormulaParser()));

    private static PivotDefinition Definition(CellRange source, CellAddress destination, string sheet) => new()
    {
        Id = "p",
        Name = "p",
        SourceSheet = sheet,
        SourceRange = source,
        DestinationSheet = "Out",
        Destination = destination,
        Fields =
        [
            new PivotField { SourceName = "Key", SourceColumnIndex = 0, Area = PivotArea.Row },
            new PivotField { SourceName = "Group", SourceColumnIndex = 1, Area = PivotArea.Row, AreaOrder = 1 },
            new PivotField { SourceName = "Amount", SourceColumnIndex = 2, Area = PivotArea.Value },
        ],
    };

    private static Worksheet Data(Workbook workbook, int rows)
    {
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Key"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Group"));
        sheet.SetValue(new CellAddress(0, 2), CellValue.Text("Amount"));
        for (var row = 1; row <= rows; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Text("K" + row));
            sheet.SetValue(new CellAddress(row, 1), CellValue.Text("G" + (row % 3)));
            sheet.SetValue(new CellAddress(row, 2), CellValue.Number(row));
        }
        return sheet;
    }

    [Fact]
    public void ManyDistinctItemsBuildInLinearTime()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = Data(workbook, 20_000);
        var range = new CellRange(CellAddress.First, new CellAddress(20_000, 2));
        var watch = Stopwatch.StartNew();
        var table = Engine().Build(Definition(range, CellAddress.First, sheet.Name), new RangePivotSource(sheet, range));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), watch.Elapsed.ToString());
        Assert.Equal(20_000 * 2 + 20_000 + 1, table.Rows.Count);
        Assert.Equal(20_000.0 * 20_001 / 2, table.Value(table.Rows.Count - 1, 0).AsNumber());
    }

    [Fact]
    public void WholeColumnSourceIgnoresTheEmptyTail()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = Data(workbook, 3);
        var range = new CellRange(CellAddress.First, new CellAddress(CellAddress.MaxRows - 1, 2));
        Assert.Equal(3, new RangePivotSource(sheet, range).RowCount);
    }

    [Fact]
    public void DateGroupingOfAnOutOfRangeSerialDoesNotThrow()
    {
        var field = new PivotField { SourceName = "D", SourceColumnIndex = 0, DateGrouping = DateGrouping.Years };
        Assert.Equal("1E+300", new PivotGrouping().KeyOf(CellValue.Number(1e300), field));
    }

    [Fact]
    public void PivotThatWouldLeaveTheSheetIsRefused()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = Data(workbook, 10);
        var range = new CellRange(CellAddress.First, new CellAddress(10, 2));
        var coordinator = new PivotCoordinator(Engine(), new PivotRenderer());
        var result = coordinator.Apply(workbook, Definition(range, new CellAddress(CellAddress.MaxRows - 5, 0), sheet.Name), Labels);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PVT006");
    }
}

public sealed class PivotLookupCacheTests
{
    private sealed class CountingEngine(IPivotEngine inner) : IPivotEngine
    {
        public int Builds { get; private set; }

        public PivotTable Build(PivotDefinition definition, IPivotSource source)
        {
            Builds++;
            return inner.Build(definition, source);
        }
    }

    [Fact]
    public void RepeatedLookupsReuseTheTableUntilTheSourceChanges()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Region"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Amount"));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Text("North"));
        sheet.SetValue(new CellAddress(1, 1), CellValue.Number(5));
        workbook.Pivots.Add(new PivotDefinition
        {
            Id = "p", Name = "p", SourceSheet = sheet.Name,
            SourceRange = new CellRange(CellAddress.First, new CellAddress(1, 1)),
            DestinationSheet = sheet.Name, Destination = new CellAddress(5, 5),
            Fields =
            [
                new PivotField { SourceName = "Region", SourceColumnIndex = 0, Area = PivotArea.Row },
                new PivotField { SourceName = "Amount", SourceColumnIndex = 1, Area = PivotArea.Value },
            ],
        });
        var engine = new CountingEngine(new PivotEngine(new PivotAggregator(), new PivotGrouping(), new PivotFormulas(new PulseOffice.Sheets.Engine.Parsing.FormulaParser())));
        var lookup = new PivotDataLookup(workbook, engine, new PivotLabels("Total", "Total {0}"));
        Assert.Equal(5, lookup.Lookup(0, new CellAddress(5, 5), "Amount", [("Region", "North")])!.Value.AsNumber());
        Assert.Equal(5, lookup.Lookup(0, new CellAddress(5, 5), "Amount", [])!.Value.AsNumber());
        Assert.Equal(1, engine.Builds);
        sheet.SetValue(new CellAddress(1, 1), CellValue.Number(7));
        Assert.Equal(7, lookup.Lookup(0, new CellAddress(5, 5), "Amount", [])!.Value.AsNumber());
        Assert.Equal(2, engine.Builds);
    }
}
