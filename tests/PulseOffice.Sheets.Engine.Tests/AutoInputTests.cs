using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class AutoInputTests
{
    private static readonly AutoInput Completer = new();

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static Worksheet Colonna(params string[] valori)
    {
        var sheet = new Worksheet("Dati");

        for (var row = 0; row < valori.Length; row++)
            sheet.SetValue(new CellAddress(row, 0), CellValue.Text(valori[row]));

        return sheet;
    }

    [Fact]
    public void UnPrefissoUnicoSiCompleta()
    {
        Assert.Equal("Milano", Completer.Complete(Colonna("Milano", "Torino"), A("A3"), "Mi"));
    }

    [Fact]
    public void DueCandidatiDiversiNonCompletanoNulla()
    {
        Assert.Null(Completer.Complete(Colonna("Milano", "Mirandola"), A("A3"), "Mi"));
    }

    [Fact]
    public void LoStessoValoreRipetutoCompletaLoStesso()
    {
        Assert.Equal("Milano", Completer.Complete(Colonna("Milano", "Milano"), A("A3"), "Mi"));
    }

    [Fact]
    public void INumeriNonSiCompletano()
    {
        Assert.Null(Completer.Complete(Colonna("100", "120"), A("A3"), "10"));
    }

    [Fact]
    public void UnaFormulaNonSiCompleta()
    {
        Assert.Null(Completer.Complete(Colonna("=SUM(A1)"), A("A3"), "=S"));
    }

    [Fact]
    public void SiGuardaAncheSotto()
    {
        var sheet = new Worksheet("Dati");
        sheet.SetValue(A("A5"), CellValue.Text("Bologna"));

        Assert.Equal("Bologna", Completer.Complete(sheet, A("A1"), "Bo"));
    }

    [Fact]
    public void InFondoAlFoglioNonCade()
    {
        var sheet = new Worksheet("Dati");
        var last = new CellAddress(CellAddress.MaxRows - 1, 0);
        sheet.SetValue(new CellAddress(CellAddress.MaxRows - 2, 0), CellValue.Text("Milano"));
        Assert.Equal("Milano", Completer.Complete(sheet, last, "Mi"));
    }

    [Fact]
    public void IRisultatiDelleFormuleNonSiPropongono()
    {
        var sheet = Colonna("Milano");
        sheet[A("A2")] = new Cell { Value = CellValue.Text("Mirto"), Formula = "=\"Mirto\"" };

        Assert.Equal("Milano", Completer.Complete(sheet, A("A3"), "Mi"));
    }

    [Fact]
    public void LElencoDiSceltaDellaColonnaHaITestiDistintiInOrdine()
    {
        Assert.Equal(["Milano", "Roma", "Torino"], Completer.Choices(Colonna("Torino", "Milano", "Torino", "Roma"), A("A6")));
    }
}
