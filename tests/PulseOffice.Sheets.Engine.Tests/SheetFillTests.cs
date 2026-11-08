using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class SheetFillTests
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static CellRange R(string a1)
    {
        Assert.True(CellRange.TryParseA1(a1, out var range));
        return range;
    }

    private static Worksheet Sheet(params (string Address, object Value)[] cells)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");

        foreach (var (address, value) in cells)
            sheet[A(address)] = value switch
            {
                double number => new Cell { Value = CellValue.Number(number) },
                int number => new Cell { Value = CellValue.Number(number) },
                string text when text.StartsWith('=') => new Cell { Formula = text },
                string text => new Cell { Value = CellValue.Text(text) },
                _ => new Cell(),
            };

        return sheet;
    }

    [Fact]
    public void DueNumeriDefinisconoUnaProgressione()
    {
        var sheet = Sheet(("A1", 10), ("A2", 20));

        var filled = new SheetFill().Extend(sheet, R("A1:A2"), R("A1:A5"), FillDirection.Down);

        Assert.Equal(3, filled.Count);
        Assert.Equal(30, filled[0].Cell.Value.AsNumber());
        Assert.Equal(40, filled[1].Cell.Value.AsNumber());
        Assert.Equal(50, filled[2].Cell.Value.AsNumber());
    }

    [Fact]
    public void UnNumeroSoloSiRipeteInvecediIndovinareIlPasso()
    {
        var sheet = Sheet(("A1", 7));

        var filled = new SheetFill().Extend(sheet, R("A1:A1"), R("A1:A3"), FillDirection.Down);

        Assert.All(filled, item => Assert.Equal(7, item.Cell.Value.AsNumber()));
    }

    [Fact]
    public void UnaProgressioneIrregolareNonVieneContinuata()
    {
        var sheet = Sheet(("A1", 1), ("A2", 2), ("A3", 4));

        var filled = new SheetFill().Extend(sheet, R("A1:A3"), R("A1:A4"), FillDirection.Down);

        Assert.Equal(1, Assert.Single(filled).Cell.Value.AsNumber());
    }

    [Fact]
    public void IlTestoConNumeroFinaleAvanza()
    {
        var sheet = Sheet(("A1", "Trimestre 1"));

        var filled = new SheetFill().Extend(sheet, R("A1:A1"), R("A1:A3"), FillDirection.Down);

        Assert.Equal("Trimestre 2", filled[0].Cell.Value.AsText());
        Assert.Equal("Trimestre 3", filled[1].Cell.Value.AsText());
    }

    [Fact]
    public void GliZeriInizialiSiConservano()
    {
        var sheet = Sheet(("A1", "Fattura 007"));

        var filled = new SheetFill().Extend(sheet, R("A1:A1"), R("A1:A3"), FillDirection.Down);

        Assert.Equal("Fattura 008", filled[0].Cell.Value.AsText());
        Assert.Equal("Fattura 009", filled[1].Cell.Value.AsText());
    }

    [Fact]
    public void IlTestoSenzaNumeroSiRipeteCiclicamente()
    {
        var sheet = Sheet(("A1", "Sì"), ("A2", "No"));

        var filled = new SheetFill().Extend(sheet, R("A1:A2"), R("A1:A6"), FillDirection.Down);

        Assert.Equal(["Sì", "No", "Sì", "No"], filled.Select(f => f.Cell.Value.AsText()));
    }

    [Fact]
    public void LeFormuleSiTraslanoComeNellIncolla()
    {
        var sheet = Sheet(("B1", "=A1*2"));

        var filled = new SheetFill().Extend(sheet, R("B1:B1"), R("B1:B3"), FillDirection.Down);

        Assert.Equal("=A2*2", filled[0].Cell.Formula);
        Assert.Equal("=A3*2", filled[1].Cell.Formula);
    }

    [Fact]
    public void SiRiempieAncheInOrizzontale()
    {
        var sheet = Sheet(("A1", 5), ("B1", 10));

        var filled = new SheetFill().Extend(sheet, R("A1:B1"), R("A1:D1"), FillDirection.Right);

        Assert.Equal(15, filled[0].Cell.Value.AsNumber());
        Assert.Equal(20, filled[1].Cell.Value.AsNumber());
    }

    [Fact]
    public void TrascinandoVersoLAltoLaSerieProsegueARitroso()
    {
        var sheet = Sheet(("A3", 10), ("A4", 20));

        var filled = new SheetFill().Extend(sheet, R("A3:A4"), R("A1:A4"), FillDirection.Up);

        Assert.Equal(0, filled[0].Cell.Value.AsNumber());
        Assert.Equal(-10, filled[1].Cell.Value.AsNumber());
    }

    [Fact]
    public void OgniColonnaContinuaPerContoProprio()
    {
        var sheet = Sheet(("A1", 1), ("A2", 2), ("B1", 10), ("B2", 20));

        var filled = new SheetFill().Extend(sheet, R("A1:B2"), R("A1:B3"), FillDirection.Down);

        var third = filled.ToDictionary(f => f.Address.ToA1(), f => f.Cell.Value.AsNumber());

        Assert.Equal(3, third["A3"]);
        Assert.Equal(30, third["B3"]);
    }

    [Fact]
    public void RiempireVersoLAltoTraslaLaFormulaDallaCellaGiusta()
    {
        var sheet = Sheet(("A2", "=B2"), ("A3", "=B3"));

        var filled = new SheetFill().Extend(sheet, R("A2:A3"), R("A1:A3"), FillDirection.Up);

        Assert.Equal("=B1", filled.Single().Item2.Formula);
    }

    [Fact]
    public void IlTestoNumeratoConPiuCelleProsegueSenzaDoppioni()
    {
        var sheet = Sheet(("A1", "Voce1"), ("A2", "Voce2"));

        var filled = new SheetFill().Extend(sheet, R("A1:A2"), R("A1:A6"), FillDirection.Down);

        Assert.Equal(["Voce3", "Voce4", "Voce5", "Voce6"], filled.Select(pair => pair.Item2.Value.AsText()).ToArray());
    }

    [Fact]
    public void LeRigheNascosteSiSaltanoELaSerieContinuaSulleVisibili()
    {
        var sheet = Sheet(("A1", 1), ("A2", 2));
        sheet.Layout.HideRow(3);

        var produced = new SheetFill().Extend(sheet, R("A1:A2"), R("A1:A6"), FillDirection.Down);

        Assert.Equal(["A3", "A5", "A6"], produced.Select(p => p.Address.ToA1()));
        Assert.Equal([3.0, 4.0, 5.0], produced.Select(p => p.Cell.Value.AsNumber()));
    }

    [Fact]
    public void UnaFormulaSaltandoUnaRigaNascostaPuntaAllaRigaGiusta()
    {
        var sheet = Sheet(("B1", "=A1*2"));
        sheet.Layout.HideRow(1);

        var produced = new SheetFill().Extend(sheet, R("B1"), R("B1:B3"), FillDirection.Down);

        Assert.Equal(["B3"], produced.Select(p => p.Address.ToA1()));
        Assert.Equal("=A3*2", produced[0].Cell.Formula);
    }

    [Fact]
    public void LeCelleUniteDellOrigineSiRipetonoNellaDestinazione()
    {
        var sheet = Sheet(("A1", "Titolo"));
        sheet.Merges.Add(R("A1:B1"));
        sheet.Merges.Add(R("A4:B5"));

        var plan = new SheetFill().Merges(sheet, R("A1:B2"), R("A1:B7"), FillDirection.Down);

        Assert.Equal(["A3:B3", "A5:B5", "A7:B7"], plan.Added.Select(r => r.ToString()));
        Assert.Equal(["A4:B5"], plan.Removed.Select(r => r.ToString()));
    }

    [Fact]
    public void ConRigheNascosteLeUnioniNonSiToccano()
    {
        var sheet = Sheet(("A1", "Titolo"));
        sheet.Merges.Add(R("A1:B1"));
        sheet.Layout.HideRow(3);

        Assert.True(new SheetFill().Merges(sheet, R("A1:B1"), R("A1:B4"), FillDirection.Down).IsEmpty);
    }

    [Theory]
    [InlineData("Lotto 123456789012345678901234")]
    [InlineData("Voce \u0663")]
    public void TestoConNumeriFinaliTroppoLunghiONonAsciiSiRipeteSenzaEccezione(string text)
    {
        var sheet = Sheet(("A1", text));
        var filled = new SheetFill().Extend(sheet, R("A1:A1"), R("A1:A3"), FillDirection.Down);
        Assert.Equal(2, filled.Count);
        Assert.All(filled, entry => Assert.Equal(text, entry.Cell.Value.AsText()));
    }
}
