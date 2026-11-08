using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class ChartDataTests
{
    private static Workbook Workbook()
    {
        var workbook = PulseOffice.Sheets.Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("2024"));
        sheet.SetValue(new CellAddress(0, 2), CellValue.Text("2025"));

        string[] mesi = ["Gennaio", "Febbraio", "Marzo"];
        double[,] valori = { { 10, 15 }, { 20, 25 }, { 30, 35 } };

        for (var row = 0; row < 3; row++)
        {
            sheet.SetValue(new CellAddress(row + 1, 0), CellValue.Text(mesi[row]));
            sheet.SetValue(new CellAddress(row + 1, 1), CellValue.Number(valori[row, 0]));
            sheet.SetValue(new CellAddress(row + 1, 2), CellValue.Number(valori[row, 1]));
        }

        return workbook;
    }

    private static ChartDefinition Definition(string range = "A1:C4") => new()
    {
        Id = "c1",
        Type = ChartType.Column,
        SourceSheet = "Sheet1",
        SourceRange = range,
    };

    [Fact]
    public void LeCategorieMostranoIlTestoDellaCellaComeLaGriglia()
    {
        var workbook = Workbook();
        workbook.Sheets[0].SetValue(new CellAddress(1, 0), CellValue.Boolean(true));
        workbook.Sheets[0].SetValue(new CellAddress(2, 0), CellValue.Number(0.5));
        var presenter = new PulseOffice.Sheets.Formatting.CellPresenter(new PulseOffice.Sheets.Formatting.NumberFormatter(
            System.Globalization.CultureInfo.GetCultureInfo("it-IT"), serial => DateTime.FromOADate(serial)));

        var shown = new ChartDataExtractor(new CanonicalSeriesNames(), new PresentedChartCellText(presenter)).Extract(workbook, Definition());
        var raw = new ChartDataExtractor().Extract(workbook, Definition());

        Assert.Equal(["VERO", "0,5", "Marzo"], shown.Categories);
        Assert.Equal("TRUE", raw.Categories[0]);
    }

    [Fact]
    public void LeSerieStannoNelleColonne()
    {
        var data = new ChartDataExtractor().Extract(Workbook(), Definition());

        Assert.Equal(2, data.Series.Count);
        Assert.Equal("2024", data.Series[0].Name);
        Assert.Equal("2025", data.Series[1].Name);
    }

    [Fact]
    public void LeCategorieVengonoDallaPrimaColonna()
    {
        var data = new ChartDataExtractor().Extract(Workbook(), Definition());
        Assert.Equal(["Gennaio", "Febbraio", "Marzo"], data.Categories);
    }

    [Fact]
    public void IValoriSonoQuelliDelFoglio()
    {
        var data = new ChartDataExtractor().Extract(Workbook(), Definition());

        Assert.Equal([10, 20, 30], data.Series[0].Values);
        Assert.Equal([15, 25, 35], data.Series[1].Values);
    }

    [Fact]
    public void SiPossonoLeggereLeSerieDalleRighe()
    {
        var data = new ChartDataExtractor().Extract(Workbook(), Definition() with { SeriesInColumns = false });

        Assert.Equal(3, data.Series.Count);
        Assert.Equal("Gennaio", data.Series[0].Name);
    }

    [Fact]
    public void IlGraficoSegueIlFoglio()
    {
        var workbook = Workbook();
        var definition = Definition();

        Assert.Equal(10, new ChartDataExtractor().Extract(workbook, definition).Series[0].Values[0]);

        workbook.Sheets[0].SetValue(new CellAddress(1, 1), CellValue.Number(99));

        Assert.Equal(99, new ChartDataExtractor().Extract(workbook, definition).Series[0].Values[0]);
    }

    [Fact]
    public void LeCelleNonNumericheSonoValoriMancanti()
    {
        var workbook = Workbook();
        workbook.Sheets[0].SetValue(new CellAddress(1, 1), CellValue.Text("n/d"));

        var data = new ChartDataExtractor().Extract(workbook, Definition());
        Assert.True(double.IsNaN(data.Series[0].Values[0]));
        Assert.Equal(15, data.Minimum);
    }

    [Fact]
    public void LeCelleVuoteSonoValoriMancanti()
    {
        var workbook = Workbook();
        workbook.Sheets[0].SetValue(new CellAddress(2, 1), CellValue.Empty);

        Assert.True(double.IsNaN(new ChartDataExtractor().Extract(workbook, Definition()).Series[0].Values[1]));
    }

    [Fact]
    public void UnFoglioInesistenteNonProduceDati()
    {
        var data = new ChartDataExtractor().Extract(Workbook(), Definition() with { SourceSheet = "Fantasma" });
        Assert.True(data.IsEmpty);
    }

    [Fact]
    public void UnIntervalloNonValidoNonProduceDati()
    {
        var data = new ChartDataExtractor().Extract(Workbook(), Definition("non-un-intervallo"));
        Assert.True(data.IsEmpty);
    }

    [Fact]
    public void SenzaIntestazioniLeCategorieSiNumerano()
    {
        var data = new ChartDataExtractor().Extract(Workbook(), Definition("B2:C4") with
        {
            FirstRowIsHeader = false,
            FirstColumnIsCategory = false,
        });

        Assert.Equal(["1", "2", "3"], data.Categories);
        Assert.Equal(2, data.Series.Count);
    }

    [Fact]
    public void GliEstremiServonoADimensionareGliAssi()
    {
        var data = new ChartDataExtractor().Extract(Workbook(), Definition());

        Assert.Equal(10, data.Minimum);
        Assert.Equal(35, data.Maximum);
    }

    [Fact]
    public void OgniSerieRiceveUnColoreDiverso()
    {
        var data = new ChartDataExtractor().Extract(Workbook(), Definition());
        Assert.NotEqual(data.Series[0].ColorHex, data.Series[1].ColorHex);
    }

    [Fact]
    public void UnaColonnaSolaDiventaComunqueUnaSerie()
    {
        var workbook = PulseOffice.Sheets.Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        for (var row = 0; row < 4; row++)
            sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row + 1));

        var data = new ChartDataExtractor().Extract(workbook, new ChartDefinition
        {
            Id = "g1",
            Type = ChartType.Column,
            SourceSheet = sheet.Name,
            SourceRange = "A1:A4",
            FirstRowIsHeader = false,
        });

        Assert.False(data.IsEmpty);
        Assert.Single(data.Series);
        Assert.Equal(4, data.Series[0].Values.Count);
    }

    [Fact]
    public void ConDueColonneLaPrimaRestaLaCategoria()
    {
        var data = new ChartDataExtractor().Extract(Workbook(), new ChartDefinition
        {
            Id = "g1",
            Type = ChartType.Column,
            SourceSheet = "Sheet1",
            SourceRange = "A1:B4",
            FirstRowIsHeader = true,
        });

        Assert.Single(data.Series);
        Assert.Equal(3, data.Categories.Count);
    }

    [Fact]
    public void SullaGrigliaLeRigheEColonneNascosteNonSiDisegnano()
    {
        var workbook = Workbook();
        workbook.Sheets[0].Layout.HideRow(2);
        workbook.Sheets[0].Layout.HideColumn(2);

        var visible = new ChartDataExtractor().Extract(workbook, Definition(), visibleOnly: true);
        var all = new ChartDataExtractor().Extract(workbook, Definition());

        Assert.Equal(["Gennaio", "Marzo"], visible.Categories);
        Assert.Single(visible.Series);
        Assert.Equal(3, all.Categories.Count);
    }

    [Fact]
    public void UnaSolaColonnaHaCategorieGenerateEDueColonneNo()
    {
        var single = new ChartDataExtractor().Extract(Workbook(), Definition("B2:B4") with { FirstRowIsHeader = false });
        var pair = new ChartDataExtractor().Extract(Workbook(), Definition());

        Assert.True(single.OrdinalCategories);
        Assert.False(pair.OrdinalCategories);
    }
}
