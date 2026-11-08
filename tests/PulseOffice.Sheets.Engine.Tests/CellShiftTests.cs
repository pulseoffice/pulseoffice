using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class CellShiftTests(EngineFixture fixture) : IClassFixture<EngineFixture>
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

    private (Recalculation.CalculationEngine Engine, Worksheet Sheet) Foglio()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];

        sheet.SetValue(A("A1"), CellValue.Number(1));
        sheet.SetValue(A("A2"), CellValue.Number(2));
        sheet.SetValue(A("A3"), CellValue.Number(3));
        sheet.SetValue(A("B1"), CellValue.Text("x"));

        engine.RecalculateAll();

        return (engine, sheet);
    }

    [Fact]
    public void InserireSpostandoInBassoSpingeSoloLaColonnaToccata()
    {
        var (engine, sheet) = Foglio();

        new SheetCellShift(new SheetStructure()).Insert(engine, 0, R("A1"), CellShift.Down);

        Assert.True(sheet[A("A1")].Value.IsEmpty);
        Assert.Equal(1d, sheet[A("A2")].Value.AsNumber());
        Assert.Equal(3d, sheet[A("A4")].Value.AsNumber());
        Assert.Equal("x", sheet[A("B1")].Value.ToString());
    }

    [Fact]
    public void EliminareSpostandoInAltoRiempieIlVuoto()
    {
        var (engine, sheet) = Foglio();

        new SheetCellShift(new SheetStructure()).Delete(engine, 0, R("A1"), CellShift.Down);

        Assert.Equal(2d, sheet[A("A1")].Value.AsNumber());
        Assert.Equal(3d, sheet[A("A2")].Value.AsNumber());
        Assert.True(sheet[A("A3")].Value.IsEmpty);
        Assert.Equal("x", sheet[A("B1")].Value.ToString());
    }

    [Fact]
    public void InserireSpostandoADestraNonToccaLeAltreRighe()
    {
        var (engine, sheet) = Foglio();

        new SheetCellShift(new SheetStructure()).Insert(engine, 0, R("A1"), CellShift.Right);

        Assert.True(sheet[A("A1")].Value.IsEmpty);
        Assert.Equal(1d, sheet[A("B1")].Value.AsNumber());
        Assert.Equal("x", sheet[A("C1")].Value.ToString());
        Assert.Equal(2d, sheet[A("A2")].Value.AsNumber());
    }

    [Fact]
    public void LeFormuleSpostateSeguonoILoroRiferimenti()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];

        sheet.SetValue(A("A1"), CellValue.Number(10));
        sheet.SetFormula(A("A2"), "=A1*2");

        engine.RecalculateAll();

        new SheetCellShift(new SheetStructure()).Insert(engine, 0, R("A2"), CellShift.Down);

        Assert.Equal("=A1*2", sheet[A("A3")].Formula);
        Assert.Equal(20d, engine.GetValue(new SheetCell(0, A("A3"))).AsNumber());
    }

    [Fact]
    public void LeFormuleSeguonoLeCelleSpostateMaNonQuelleDiAltreColonne()
    {
        var (engine, sheet) = Foglio();
        sheet.SetFormula(A("C1"), "=A3+B3");
        engine.RecalculateAll();

        new SheetCellShift(new SheetStructure()).Insert(engine, 0, R("A2"), CellShift.Down);

        Assert.Equal("=A4+B3", sheet[A("C1")].Formula);
        Assert.Equal(3, sheet[A("C1")].Value.AsNumber());
    }

    [Fact]
    public void InserireCelleSpostaUnioniConvalideRegoleENoteDellaBanda()
    {
        var (engine, sheet) = Foglio();
        sheet.Merges.Add(R("A3:A4"));
        sheet.Merges.Add(R("B3:B4"));
        sheet.Validations.Add(new DataValidation { Range = R("A5:A6"), Kind = ValidationKind.WholeNumber, FirstFormula = "A7" });
        sheet.ConditionalRules.Add(new Formatting.ConditionalRule
        {
            Range = R("A5"),
            Operator = Formatting.ConditionOperator.ContainsText,
            Operands = ["x"],
            Style = new Styling.CellStyle { BackgroundHex = "#00FF00" },
        });
        sheet.Comments[A("A3")] = new CellComment { Text = "nota" };

        new SheetCellShift(new SheetStructure()).Insert(engine, 0, R("A1:A2"), CellShift.Down);

        Assert.Contains(R("A5:A6"), sheet.Merges.All);
        Assert.Contains(R("B3:B4"), sheet.Merges.All);
        Assert.Equal(R("A7:A8"), sheet.Validations[0].Range);
        Assert.Equal("A9", sheet.Validations[0].FirstFormula);
        Assert.Equal(R("A7"), sheet.ConditionalRules[0].Range);
        Assert.Equal("nota", sheet.Comments[A("A5")].Text);
        Assert.False(sheet.Comments.ContainsKey(A("A3")));
    }

    [Fact]
    public void EliminareCelleTogliUnioniEConvalideCancellate()
    {
        var (engine, sheet) = Foglio();
        sheet.Merges.Add(R("A1:B1"));
        sheet.Merges.Add(R("D3:E3"));
        sheet.Validations.Add(new DataValidation { Range = R("A1"), Kind = ValidationKind.WholeNumber });
        sheet.Validations.Add(new DataValidation { Range = R("D1"), Kind = ValidationKind.WholeNumber });

        new SheetCellShift(new SheetStructure()).Delete(engine, 0, R("A1:B1"), CellShift.Right);

        Assert.DoesNotContain(R("A1:B1"), sheet.Merges.All);
        Assert.Contains(R("D3:E3"), sheet.Merges.All);
        Assert.Single(sheet.Validations);
        Assert.Equal(R("B1"), sheet.Validations[0].Range);
    }

    [Fact]
    public void InserireRigheSpostaLeConvalide()
    {
        var (engine, sheet) = Foglio();
        sheet.Validations.Add(new DataValidation { Range = R("A3:A4"), Kind = ValidationKind.WholeNumber });

        new SheetStructure().InsertRows(engine, 0, 0, 2);

        Assert.Equal(R("A5:A6"), sheet.Validations[0].Range);
    }
}
