using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round178ChartErrorReferenceTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private (Workbook Workbook, Recalculation.CalculationEngine Engine, Worksheet Data, Worksheet Other) Scene()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var data = workbook.Sheets[0];
        var other = workbook.AddSheet("Errori");
        for (var i = 0; i < 2; i++)
        {
            data[A($"A{i + 1}")] = new Cell { Value = CellValue.Number(10 + i) };
            other[A($"B{i + 2}")] = new Cell { Value = CellValue.Number(1 + i) };
        }
        var bars = new ChartErrorBars(ErrorBarKind.Custom, 0)
        {
            Plus = new ChartErrorValues("Errori!B2:B3", [9, 9]),
            Minus = new ChartErrorValues(null, [0.5, 0.5]),
        };
        var chart = new ChartDefinition { Id = "g", Type = ChartType.Column, SourceSheet = data.Name, SourceRange = "A1:A2" }
            .WithSeriesFormat(0, new ChartSeriesFormat { ErrorBars = bars });
        data.Objects.Add(new PlacedObject { Id = chart.Id, Content = chart, Anchor = A("D2") });
        return (workbook, engine, data, other);
    }

    private static ChartErrorBars Bars(Worksheet sheet) =>
        Assert.IsType<ChartDefinition>(sheet.Objects[0].Content).SeriesFormat(0).ErrorBars!;

    [Fact]
    public void Inserted_rows_move_the_error_value_reference()
    {
        var (_, engine, data, _) = Scene();

        new SheetStructure().InsertRows(engine, 1, 0, 2);

        Assert.Equal("Errori!B4:B5", Bars(data).Plus!.Reference);
        Assert.Null(Bars(data).Minus!.Reference);
    }

    [Fact]
    public void Deleted_rows_break_the_error_value_reference()
    {
        var (_, engine, data, _) = Scene();

        new SheetStructure().DeleteRows(engine, 1, 0, 4);

        Assert.Equal(ChartSourceReferences.Broken, Bars(data).Plus!.Reference);
    }

    [Fact]
    public void Renaming_the_sheet_follows_and_reverts_in_one_step()
    {
        var (workbook, engine, data, other) = Scene();
        var renaming = new SheetRenaming();
        var plan = renaming.Plan(workbook, other, "Margini");

        renaming.Apply(engine, plan);
        Assert.Equal("Margini!B2:B3", Bars(data).Plus!.Reference);

        renaming.Revert(engine, plan);
        Assert.Equal("Errori!B2:B3", Bars(data).Plus!.Reference);
    }

    [Fact]
    public void Refresh_reads_the_current_cells_and_keeps_literal_values()
    {
        var (workbook, _, data, other) = Scene();
        other[A("B3")] = new Cell { Value = CellValue.Number(4) };

        var refreshed = new ChartErrorValueRefresh().Refresh(workbook, Assert.IsType<ChartDefinition>(data.Objects[0].Content));

        var bars = refreshed.SeriesFormat(0).ErrorBars!;
        Assert.Equal([1d, 4d], bars.Plus!.Values);
        Assert.Equal([0.5, 0.5], bars.Minus!.Values);
    }
}
