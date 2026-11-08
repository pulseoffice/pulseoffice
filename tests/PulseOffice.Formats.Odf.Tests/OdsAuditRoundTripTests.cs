using PulseOffice.Foundation.Units;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsAuditRoundTripTests
{
    private static Workbook RoundTrip(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    [Fact]
    public void UnCommentoSuUnaCellaVuotaSopravvive()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Comments[A("C5")] = new CellComment { Text = "riga uno\nriga  due" };

        var back = RoundTrip(workbook).Sheets[0];

        Assert.Equal("riga uno\nriga  due", back.Comments[A("C5")].Text);
    }

    [Fact]
    public void IlTestoSuPiuRigheConSpaziETabulazioniSopravvive()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(A("A1"), CellValue.Text("  due  spazi\tposto\nseconda riga"));

        Assert.Equal("  due  spazi\tposto\nseconda riga", RoundTrip(workbook).Sheets[0][A("A1")].Value.AsText());
    }

    [Fact]
    public void UnaRigaNascostaEUnGruppoDopoUnaRigaVuotaSopravvivono()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(A("A1"), CellValue.Number(1));
        sheet.SetValue(A("A20"), CellValue.Number(20));
        sheet.SetValue(A("A25"), CellValue.Number(25));
        sheet.RowGroups.Add(new OutlineGroup { First = 19, Last = 24, Level = 1 });
        sheet.Layout.HideRow(2);
        sheet.Layout.HideColumn(3);

        var back = RoundTrip(workbook).Sheets[0];

        var group = Assert.Single(back.RowGroups);
        Assert.Equal(19, group.First);
        Assert.Equal(24, group.Last);
        Assert.True(back.Layout.IsRowHidden(2));
        Assert.True(back.Layout.IsColumnHidden(3));
        Assert.Equal(25, back[A("A25")].Value.AsNumber());
    }

    [Fact]
    public void LAreaDiStampaEIlNomeDiUnFoglioConSpaziSopravvivono()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati 2024");
        workbook.AddSheet("Altro");
        sheet.SetValue(A("A1"), CellValue.Number(1));
        sheet.PrintRanges.Add(new CellRange(A("A1"), A("B5")));
        workbook.DefineName(new DefinedName("Totali", "'Dati 2024'.$A$1:$A$10"));
        workbook.Sheets[1].SetFormula(A("A1"), "='Dati 2024'.A1*2");

        var back = RoundTrip(workbook);

        Assert.Equal(new CellRange(A("A1"), A("B5")), Assert.Single(back.Sheets[0].PrintRanges));
        Assert.Equal("'Dati 2024'.$A$1:$A$10", Assert.Single(back.DefinedNames).Expression);
        Assert.Equal("='Dati 2024'.A1*2", back.Sheets[1][A("A1")].Formula);
    }
}
