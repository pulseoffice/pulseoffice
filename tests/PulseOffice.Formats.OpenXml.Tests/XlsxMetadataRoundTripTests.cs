using PulseOffice.Formats.OpenXml;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxMetadataRoundTripTests
{
    private static Workbook RoundTrip(Workbook workbook)
    {
        using var stream = new MemoryStream();

        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;

        var result = new XlsxReader().Read(stream);
        Assert.True(result.Succeeded);

        return result.Value!;
    }

    private static CellRange Range(int firstRow, int firstColumn, int lastRow, int lastColumn) =>
        new(new CellAddress(firstRow, firstColumn), new CellAddress(lastRow, lastColumn));

    [Fact]
    public void UnaConvalidaNumericaSopravvive()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        sheet.SetValue(CellAddress.First, CellValue.Number(5));
        sheet.Validations.Add(new DataValidation
        {
            Range = Range(0, 0, 9, 0),
            Kind = ValidationKind.WholeNumber,
            Operator = ValidationOperator.Between,
            FirstFormula = "1",
            SecondFormula = "10",
            ErrorMessage = "Da 1 a 10",
        });

        var validation = Assert.Single(RoundTrip(workbook).Sheets[0].Validations);

        Assert.Equal(ValidationKind.WholeNumber, validation.Kind);
        Assert.Equal(ValidationOperator.Between, validation.Operator);
        Assert.Equal("1", validation.FirstFormula);
        Assert.Equal("10", validation.SecondFormula);
        Assert.Equal("Da 1 a 10", validation.ErrorMessage);
    }

    [Fact]
    public void UnaConvalidaAElencoSopravvive()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        sheet.SetValue(CellAddress.First, CellValue.Text("Nord"));
        sheet.Validations.Add(new DataValidation
        {
            Range = Range(0, 0, 4, 0),
            Kind = ValidationKind.List,
            ListValues = ["Nord", "Sud"],
        });

        var validation = Assert.Single(RoundTrip(workbook).Sheets[0].Validations);

        Assert.Equal(ValidationKind.List, validation.Kind);
        Assert.Equal(["Nord", "Sud"], validation.ListValues);
    }

    [Fact]
    public void LAreaDiStampaSopravvive()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        sheet.SetValue(CellAddress.First, CellValue.Number(1));
        sheet.PrintRanges.Add(Range(0, 0, 4, 2));
        sheet.RepeatedPrintRows = 2;

        var reread = RoundTrip(workbook).Sheets[0];

        Assert.Equal(Range(0, 0, 4, 2), Assert.Single(reread.PrintRanges));
        Assert.Equal(2, reread.RepeatedPrintRows);
    }

    [Fact]
    public void LeMisureDiRigheEColonneSopravvivono()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        sheet.SetValue(CellAddress.First, CellValue.Number(1));
        sheet.Layout.SetColumnWidth(0, PulseOffice.Foundation.Units.Twip.FromPoints(120));
        sheet.Layout.SetRowHeight(0, PulseOffice.Foundation.Units.Twip.FromPoints(30));

        var reread = RoundTrip(workbook).Sheets[0];

        Assert.Equal(120, reread.Layout.ColumnWidth(0).ToPoints(), 1);
        Assert.Equal(30, reread.Layout.RowHeight(0).ToPoints(), 1);
    }

    [Fact]
    public void UnRaggruppamentoDiRigheSopravvive()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        for (var row = 0; row < 5; row++)
            sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));

        sheet.RowGroups.Add(new OutlineGroup { First = 1, Last = 3, Level = 1 });

        var reread = RoundTrip(workbook).Sheets[0];
        var group = Assert.Single(reread.RowGroups);

        Assert.Equal(1, group.First);
        Assert.Equal(3, group.Last);
        Assert.False(group.IsCollapsed);
    }

    [Fact]
    public void UnRaggruppamentoChiusoTornaChiuso()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        for (var row = 0; row < 5; row++)
            sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));

        sheet.RowGroups.Add(new OutlineGroup { First = 1, Last = 2, Level = 1, IsCollapsed = true });
        sheet.Layout.SetRowHeight(1, PulseOffice.Foundation.Units.Twip.Zero);
        sheet.Layout.SetRowHeight(2, PulseOffice.Foundation.Units.Twip.Zero);

        var group = Assert.Single(RoundTrip(workbook).Sheets[0].RowGroups);

        Assert.True(group.IsCollapsed);
    }

    [Fact]
    public void IlNomeDelloStileRestaAttaccatoAllaCella()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        var good = workbook.NamedStyles.Find("Good")!;

        sheet.SetValue(CellAddress.First, CellValue.Number(1));
        sheet.SetStyle(CellAddress.First, good.StyleId);
        sheet.SetStyleName(CellAddress.First, good.Name);

        var reread = RoundTrip(workbook).Sheets[0][CellAddress.First];

        Assert.Equal("Good", reread.StyleName);
    }

    [Fact]
    public void UnaCellaSenzaStileNominatoNonNeInventaUno()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        sheet.SetValue(CellAddress.First, CellValue.Number(1));

        Assert.Null(RoundTrip(workbook).Sheets[0][CellAddress.First].StyleName);
    }

    [Fact]
    public void UnAvvisoNonBloccanteRestaTale()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        sheet.SetValue(CellAddress.First, CellValue.Number(5));
        sheet.Validations.Add(new DataValidation
        {
            Range = Range(0, 0, 0, 0),
            Kind = ValidationKind.Decimal,
            Operator = ValidationOperator.Greater,
            FirstFormula = "0",
            Alert = ValidationAlert.Warning,
        });

        Assert.Equal(ValidationAlert.Warning, RoundTrip(workbook).Sheets[0].Validations[0].Alert);
    }

    [Fact]
    public void TutteLeFormeDiConvalidaSopravvivono()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.Name = "Dati 2024";
        CellRange Cells(int row) => new(new CellAddress(row, 0), new CellAddress(row, 1));
        DataValidation[] rules =
        [
            new() { Range = Cells(0), Kind = ValidationKind.Time, Operator = ValidationOperator.Greater, FirstFormula = "0.5" },
            new() { Range = Cells(1), Kind = ValidationKind.TextLength, Operator = ValidationOperator.Between, FirstFormula = "2", SecondFormula = "8" },
            new() { Range = Cells(2), Kind = ValidationKind.TextLength, Operator = ValidationOperator.LessOrEqual, FirstFormula = "5" },
            new() { Range = Cells(3), Kind = ValidationKind.List, FirstFormula = "$D$1:$D$4", ShowDropdown = false },
            new() { Range = Cells(4), Kind = ValidationKind.Custom, FirstFormula = "C5>SUM(D1;D2)" },
            new() { Range = Cells(5), Kind = ValidationKind.List, ListValues = ["Nord", "Sud \"est\""] },
            new()
            {
                Range = Cells(6), Kind = ValidationKind.WholeNumber, Operator = ValidationOperator.Between,
                FirstFormula = "D1", SecondFormula = "MAX(D1;D2)", Alert = ValidationAlert.Warning,
                ErrorTitle = "Attenzione", InputTitle = "Suggerimento", InputMessage = "Scrivi un intero",
            },
        ];
        foreach (var rule in rules) sheet.Validations.Add(rule);

        var read = RoundTrip(workbook).Sheets[0].Validations.OrderBy(rule => rule.Range.Start.Row).ToList();

        Assert.Equal(rules.Length, read.Count);
        for (var index = 0; index < rules.Length; index++)
        {
            Assert.Equal(rules[index].Kind, read[index].Kind);
            Assert.Equal(rules[index].Range, read[index].Range);
            Assert.Equal(rules[index].ListValues, read[index].ListValues);
            Assert.Equal(rules[index].ShowDropdown, read[index].ShowDropdown);
            Assert.Equal(rules[index].Alert, read[index].Alert);
            Assert.Equal(rules[index].ErrorTitle, read[index].ErrorTitle);
            Assert.Equal(rules[index].InputTitle, read[index].InputTitle);
            Assert.Equal(rules[index].InputMessage, read[index].InputMessage);
            Assert.Equal(rules[index].FirstFormula?.Replace("$", ""), read[index].FirstFormula?.Replace("$", ""));
            Assert.Equal(rules[index].SecondFormula, read[index].SecondFormula);
            if (rules[index].Kind != ValidationKind.List && rules[index].Kind != ValidationKind.Custom)
                Assert.Equal(rules[index].Operator, read[index].Operator);
        }
    }

    [Fact]
    public void INomiLocaliEICostantiDecimaliSopravvivono()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Name = "Dati 2024";
        workbook.AddSheet("Altro");
        workbook.DefineName(new DefinedName("Aliquota", "0.22"));
        workbook.DefineName(new DefinedName("Zona", "$A$1:$B$3", "Dati 2024"));
        workbook.DefineName(new DefinedName("Punto", "$C$1", "Altro"));
        workbook.DefineName(new DefinedName("Totale", "SUM($A$1:$A$3)*1.5"));

        var read = RoundTrip(workbook);
        var names = read.DefinedNames.ToDictionary(name => (name.Name, name.Scope));

        Assert.Equal("0.22", names[("Aliquota", null)].Expression);
        Assert.Contains("A$1:$B$3", names[("Zona", "Dati 2024")].Expression);
        Assert.Contains("$C$1", names[("Punto", "Altro")].Expression);
        Assert.Contains("SUM(", names[("Totale", null)].Expression);
    }

    [Fact]
    public void IlCommentoSempreVisibileRestaVisibileAncheSulSecondoFoglio()
    {
        var workbook = Workbook.CreateDefault();
        var second = workbook.AddSheet("Secondo");
        workbook.Sheets[0].Comments[new CellAddress(0, 0)] = new CellComment { Text = "Primo", Author = "Ada" };
        second.Comments[new CellAddress(2, 1)] = new CellComment { Text = "Fisso", Author = "Ada", IsAlwaysVisible = true };
        second.Comments[new CellAddress(3, 1)] = new CellComment { Text = "Nascosto" };

        var read = RoundTrip(workbook);

        Assert.False(read.Sheets[0].Comments[new CellAddress(0, 0)].IsAlwaysVisible);
        Assert.True(read.Sheets[1].Comments[new CellAddress(2, 1)].IsAlwaysVisible);
        Assert.False(read.Sheets[1].Comments[new CellAddress(3, 1)].IsAlwaysVisible);
    }

    [Fact]
    public void MessaggiSpentiSopravvivono()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Validations.Add(new DataValidation
        {
            Range = Range(0, 0, 2, 0),
            Kind = ValidationKind.WholeNumber,
            Operator = ValidationOperator.Greater,
            FirstFormula = "0",
            InputTitle = "Quantità",
            InputMessage = "Un intero positivo",
            ShowInputMessage = false,
            ShowErrorAlert = false,
        });
        workbook.Sheets[0].Validations.Add(new DataValidation
        {
            Range = Range(4, 0, 4, 0),
            Kind = ValidationKind.Decimal,
            Operator = ValidationOperator.Less,
            FirstFormula = "10",
        });

        var read = RoundTrip(workbook).Sheets[0].Validations.OrderBy(rule => rule.Range.Start.Row).ToList();

        Assert.False(read[0].ShowInputMessage);
        Assert.False(read[0].ShowErrorAlert);
        Assert.Equal("Un intero positivo", read[0].InputMessage);
        Assert.True(read[1].ShowInputMessage);
        Assert.True(read[1].ShowErrorAlert);
    }
}
