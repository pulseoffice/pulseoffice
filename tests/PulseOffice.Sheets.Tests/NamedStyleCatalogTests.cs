using PulseOffice.Sheets;
using PulseOffice.Sheets.Styling;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class NamedStyleCatalogTests
{
    [Fact]
    public void UnDocumentoNuovoHaGliStiliDiBase()
    {
        var workbook = Workbook.CreateDefault();

        Assert.NotNull(workbook.NamedStyles.Find("Default"));
        Assert.NotNull(workbook.NamedStyles.Find("Good"));
        Assert.All(workbook.NamedStyles.All, style => Assert.True(style.IsBuiltIn));
    }

    [Fact]
    public void IlNomeSiCercaSenzaBadareAlleMaiuscole()
    {
        var workbook = Workbook.CreateDefault();

        Assert.NotNull(workbook.NamedStyles.Find("good"));
    }

    [Fact]
    public void UnoStileNuovoSiAggiungeEsiPuoTogliere()
    {
        var workbook = Workbook.CreateDefault();

        workbook.NamedStyles.Define("Mio", CellStyle.Default with
        {
            Font = CellFont.Default with { Bold = true },
        });

        Assert.NotNull(workbook.NamedStyles.Find("Mio"));
        Assert.True(workbook.NamedStyles.Remove("Mio"));
        Assert.Null(workbook.NamedStyles.Find("Mio"));
    }

    [Fact]
    public void UnoStileDiBaseNonSiPuoTogliere()
    {
        var workbook = Workbook.CreateDefault();

        Assert.False(workbook.NamedStyles.Remove("Default"));
    }

    [Fact]
    public void RidefinireUnNomeNonNeCreaUnSecondo()
    {
        var workbook = Workbook.CreateDefault();
        var before = workbook.NamedStyles.All.Count;

        workbook.NamedStyles.Define("Good", CellStyle.Default with { BackgroundHex = "#00FF00" });

        Assert.Equal(before, workbook.NamedStyles.All.Count);
        Assert.Equal("#00FF00", workbook.Styles.Get(workbook.NamedStyles.Find("Good")!.StyleId).BackgroundHex);
    }

    [Fact]
    public void UnoStileEreditaDalPadreCioCheNonDichiara()
    {
        var workbook = Workbook.CreateDefault();

        var heading2 = workbook.NamedStyles.Find("Heading 2");
        Assert.NotNull(heading2);

        var resolved = workbook.NamedStyles.StyleOf(heading2);

        Assert.True(resolved.Font.Italic);
        Assert.Equal(CellHorizontalAlignment.Center, resolved.Horizontal);
    }
}
