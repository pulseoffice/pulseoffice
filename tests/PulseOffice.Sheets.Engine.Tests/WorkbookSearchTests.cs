using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class WorkbookSearchTests
{
    private static readonly IWorkbookSearch Search = new WorkbookSearch();

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static Workbook Build()
    {
        var workbook = new Workbook();

        var first = workbook.AddSheet("Primo");
        var second = workbook.AddSheet("Secondo");

        first[A("A1")] = new Cell { Value = CellValue.Text("Fattura 2024") };
        first[A("A2")] = new Cell { Value = CellValue.Text("Nota di credito") };

        second[A("B1")] = new Cell { Value = CellValue.Text("Fattura 2025") };
        second[A("B2")] = new Cell { Value = CellValue.Number(42) };

        return workbook;
    }

    [Fact]
    public void SenzaTutteLeSchedeSiCercaSoloNelFoglioCorrente()
    {
        var found = Search.FindAll(Build(), 0, "Fattura");

        Assert.Single(found);
        Assert.Equal("Primo", found[0].SheetName);
    }

    [Fact]
    public void ConTutteLeSchedeSiTrovaAncheAltrove()
    {
        var found = Search.FindAll(
            Build(), 0, "Fattura", SearchOptions.Default with { AllSheets = true });

        Assert.Equal(2, found.Count);
        Assert.Contains(found, match => match.SheetName == "Secondo");
    }

    [Fact]
    public void LaRicercaConEspressioneRegolareTrovaUnoSchema()
    {
        var found = Search.FindAll(
            Build(), 0, @"Fattura \d{4}",
            SearchOptions.Default with { AllSheets = true, UseRegex = true });

        Assert.Equal(2, found.Count);
    }

    [Fact]
    public void UnEspressioneRegolareRottaNonTrovaNulla()
    {
        var found = Search.FindAll(
            Build(), 0, "Fattura [", SearchOptions.Default with { UseRegex = true });

        Assert.Empty(found);
    }

    [Fact]
    public void LaSostituzioneAttraversaLeSchede()
    {
        var workbook = Build();

        var replaced = Search.ReplaceAll(
            workbook, 0, "Fattura", "Ricevuta", SearchOptions.Default with { AllSheets = true });

        Assert.Equal(2, replaced);
        Assert.Equal("Ricevuta 2025", workbook.Sheets[1][A("B1")].Value.ToString());
    }

    [Fact]
    public void SiCercaAncheSoloPerFormato()
    {
        var workbook = Build();
        var bold = workbook.Styles.Intern(new CellStyle { Font = CellFont.Default with { Bold = true } });

        workbook.Sheets[0].SetStyle(A("A2"), bold);

        var found = Search.FindAll(
            workbook, 0, string.Empty,
            SearchOptions.Default with { Style = new StyleFilter { Bold = true } });

        Assert.Single(found);
        Assert.Equal(A("A2"), found[0].Address);
    }

    [Fact]
    public void IlFormatoRestringeLaRicercaPerTesto()
    {
        var workbook = Build();
        var yellow = workbook.Styles.Intern(new CellStyle { BackgroundHex = "#FFFF00" });

        workbook.Sheets[1].SetStyle(A("B1"), yellow);

        var found = Search.FindAll(
            workbook, 0, "Fattura",
            SearchOptions.Default with
            {
                AllSheets = true,
                Style = new StyleFilter { BackgroundHex = "#FFFF00" },
            });

        Assert.Single(found);
        Assert.Equal("Secondo", found[0].SheetName);
    }

    [Fact]
    public void UnFiltroVuotoNonRestringeNulla()
    {
        var found = Search.FindAll(
            Build(), 0, "Fattura",
            SearchOptions.Default with { AllSheets = true, Style = new StyleFilter() });

        Assert.Equal(2, found.Count);
    }

    [Fact]
    public void LaCellaInteraChiedeLaCorrispondenzaEsatta()
    {
        var found = Search.FindAll(
            Build(), 0, "Fattura", SearchOptions.Default with { WholeCell = true });

        Assert.Empty(found);
    }
}
