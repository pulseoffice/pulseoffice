using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Pivot.Tests;

public class PivotCoordinatorTests
{
    private static readonly PivotLabels Labels = new("Totale generale", "Totale {0}");

    private static IPivotCoordinator Coordinator() =>
        new PivotCoordinator(new PivotEngine(new PivotAggregator(), new PivotGrouping(), new PivotFormulas(new PulseOffice.Sheets.Engine.Parsing.FormulaParser())), new PivotRenderer());

    private static Workbook WorkbookWithData()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Regione"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Importo"));

        string[] regions = ["Nord", "Sud", "Nord"];
        double[] amounts = [100, 20, 30];

        for (var row = 0; row < regions.Length; row++)
        {
            sheet.SetValue(new CellAddress(row + 1, 0), CellValue.Text(regions[row]));
            sheet.SetValue(new CellAddress(row + 1, 1), CellValue.Number(amounts[row]));
        }

        return workbook;
    }

    private static PivotDefinition Definition(Workbook workbook, string destinationSheet = "Pivot") => new()
    {
        Id = "p1",
        Name = "Riepilogo",
        SourceSheet = workbook.Sheets[0].Name,
        SourceRange = new CellRange(new CellAddress(0, 0), new CellAddress(3, 1)),
        DestinationSheet = destinationSheet,
        Destination = CellAddress.First,
        Fields =
        [
            new PivotField { SourceName = "Regione", SourceColumnIndex = 0, Area = PivotArea.Row },
            new PivotField { SourceName = "Importo", SourceColumnIndex = 1, Area = PivotArea.Value },
        ],
    };

    [Fact]
    public void LaTabellaSiScriveSuUnFoglioNuovoSeNonEsiste()
    {
        var workbook = WorkbookWithData();

        var result = Coordinator().Apply(workbook, Definition(workbook), Labels);

        Assert.True(result.Succeeded);

        var pivot = workbook.FindSheet("Pivot");
        Assert.NotNull(pivot);
        Assert.Equal(130, pivot.GetValue(new CellAddress(1, 1)).AsNumber());
    }

    [Fact]
    public void LaDefinizioneRestaNelDocumento()
    {
        var workbook = WorkbookWithData();
        Coordinator().Apply(workbook, Definition(workbook), Labels);

        var stored = Assert.Single(workbook.Pivots);
        Assert.Equal("p1", stored.Id);
        Assert.NotNull(stored.OutputRange);
    }

    [Fact]
    public void RicalcolandoNonRestanoRigheVecchie()
    {
        var workbook = WorkbookWithData();
        var coordinator = Coordinator();

        coordinator.Apply(workbook, Definition(workbook), Labels);

        var source = workbook.Sheets[0];
        source.SetValue(new CellAddress(2, 0), CellValue.Text("Nord"));

        coordinator.RefreshAll(workbook, Labels);

        var pivot = workbook.FindSheet("Pivot")!;

        Assert.Equal("Nord", pivot.GetValue(new CellAddress(1, 0)).AsText());
        Assert.Equal("Totale generale", pivot.GetValue(new CellAddress(2, 0)).AsText());
        Assert.True(pivot.GetValue(new CellAddress(3, 0)).IsEmpty);
    }

    [Fact]
    public void UnaDestinazioneDentroIDatiVieneRifiutata()
    {
        var workbook = WorkbookWithData();

        var definition = Definition(workbook, workbook.Sheets[0].Name) with
        {
            Destination = new CellAddress(1, 1),
        };

        var result = Coordinator().Apply(workbook, definition, Labels);

        Assert.False(result.Succeeded);
        Assert.Empty(workbook.Pivots);
    }

    [Fact]
    public void SenzaCampiValoreNonSiScriveNulla()
    {
        var workbook = WorkbookWithData();

        var definition = Definition(workbook) with
        {
            Fields = [new PivotField { SourceName = "Regione", SourceColumnIndex = 0, Area = PivotArea.Row }],
        };

        Assert.False(Coordinator().Apply(workbook, definition, Labels).Succeeded);
        Assert.Null(workbook.FindSheet("Pivot"));
    }

    [Fact]
    public void TogliendoLaTabellaSiPuliscePureIlFoglio()
    {
        var workbook = WorkbookWithData();
        var coordinator = Coordinator();

        coordinator.Apply(workbook, Definition(workbook), Labels);
        coordinator.Remove(workbook, "p1");

        Assert.Empty(workbook.Pivots);
        Assert.Equal(0, workbook.FindSheet("Pivot")!.CellCount);
    }

    [Fact]
    public void UnaTabellaCheSiSovrapporrebbeAiDatiVieneRifiutata()
    {
        var workbook = WorkbookWithData();
        var definition = Definition(workbook, workbook.Sheets[0].Name) with { Destination = new CellAddress(0, 3) };
        definition = definition with { SourceRange = new CellRange(new CellAddress(0, 0), new CellAddress(3, 4)) };

        var result = Coordinator().Apply(workbook, definition, Labels);

        Assert.False(result.Succeeded);
        Assert.Empty(workbook.Pivots);
    }

    [Fact]
    public void UnaPivotNuovaNonSovrascriveDatiEsistenti()
    {
        var workbook = WorkbookWithData();
        var other = workbook.AddSheet("Pivot");
        other.SetValue(new CellAddress(1, 1), CellValue.Text("importante"));

        var result = Coordinator().Apply(workbook, Definition(workbook), Labels);

        Assert.False(result.Succeeded);
        Assert.Equal("PVT005", result.Diagnostics[0].Code);
        Assert.Equal("importante", other.GetValue(new CellAddress(1, 1)).ToString());
    }
}
