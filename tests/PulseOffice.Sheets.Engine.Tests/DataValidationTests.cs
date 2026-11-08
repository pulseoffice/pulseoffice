using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class DataValidationTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static readonly IDataValidator Validator = new DataValidator();

    private static CellRange Range(int firstRow, int firstColumn, int lastRow, int lastColumn) =>
        new(new CellAddress(firstRow, firstColumn), new CellAddress(lastRow, lastColumn));

    [Fact]
    public void SenzaRegolaVaBeneTutto()
    {
        var (_, engine) = fixture.CreateWorkbook();

        Assert.True(Validator
            .Validate(engine, 0, CellAddress.First, CellValue.Text("qualunque"))
            .IsValid);
    }

    [Fact]
    public void UnInteroFraDueEstremi()
    {
        var (workbook, engine) = fixture.CreateWorkbook();

        workbook.Sheets[0].Validations.Add(new DataValidation
        {
            Range = Range(0, 0, 9, 0),
            Kind = ValidationKind.WholeNumber,
            Operator = ValidationOperator.Between,
            FirstFormula = "1",
            SecondFormula = "10",
        });

        Assert.True(Validator.Validate(engine, 0, CellAddress.First, CellValue.Number(5)).IsValid);
        Assert.False(Validator.Validate(engine, 0, CellAddress.First, CellValue.Number(50)).IsValid);
        Assert.False(Validator.Validate(engine, 0, CellAddress.First, CellValue.Number(5.5)).IsValid);
    }

    [Fact]
    public void UnElencoDiValoriAmmessi()
    {
        var (workbook, engine) = fixture.CreateWorkbook();

        workbook.Sheets[0].Validations.Add(new DataValidation
        {
            Range = Range(0, 0, 9, 0),
            Kind = ValidationKind.List,
            ListValues = ["Nord", "Sud"],
        });

        Assert.True(Validator.Validate(engine, 0, CellAddress.First, CellValue.Text("nord")).IsValid);
        Assert.False(Validator.Validate(engine, 0, CellAddress.First, CellValue.Text("Est")).IsValid);
    }

    [Fact]
    public void LElencoPuoVenireDaUnIntervallo()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];

        engine.SetValue(new SheetCell(0, new CellAddress(0, 5)), CellValue.Text("Vite"));
        engine.SetValue(new SheetCell(0, new CellAddress(1, 5)), CellValue.Text("Dado"));

        sheet.Validations.Add(new DataValidation
        {
            Range = Range(0, 0, 9, 0),
            Kind = ValidationKind.List,
            FirstFormula = "F1:F2",
        });

        var choices = Validator.ChoicesOf(engine, 0, sheet.Validations[0]);

        Assert.Equal(["Vite", "Dado"], choices);
        Assert.True(Validator.Validate(engine, 0, CellAddress.First, CellValue.Text("Dado")).IsValid);
    }

    [Fact]
    public void UnaCellaVuotaPassaSoloSeAmmessa()
    {
        var (workbook, engine) = fixture.CreateWorkbook();

        workbook.Sheets[0].Validations.Add(new DataValidation
        {
            Range = Range(0, 0, 9, 0),
            Kind = ValidationKind.WholeNumber,
            FirstFormula = "1",
            SecondFormula = "10",
            AllowBlank = false,
        });

        Assert.False(Validator.Validate(engine, 0, CellAddress.First, CellValue.Empty).IsValid);
    }

    [Fact]
    public void UnaFormulaPersonalizzataDecide()
    {
        var (workbook, engine) = fixture.CreateWorkbook();

        engine.SetValue(new SheetCell(0, new CellAddress(0, 1)), CellValue.Number(3));

        workbook.Sheets[0].Validations.Add(new DataValidation
        {
            Range = Range(0, 0, 9, 0),
            Kind = ValidationKind.Custom,
            FirstFormula = "=B1>2",
        });

        Assert.True(Validator.Validate(engine, 0, CellAddress.First, CellValue.Number(1)).IsValid);

        engine.SetValue(new SheetCell(0, new CellAddress(0, 1)), CellValue.Number(1));

        Assert.False(Validator.Validate(engine, 0, CellAddress.First, CellValue.Number(1)).IsValid);
    }

    [Fact]
    public void SoloLoStopImpedisceDavveroLaScrittura()
    {
        var (workbook, engine) = fixture.CreateWorkbook();

        workbook.Sheets[0].Validations.Add(new DataValidation
        {
            Range = Range(0, 0, 9, 0),
            Kind = ValidationKind.WholeNumber,
            FirstFormula = "1",
            SecondFormula = "10",
            Alert = ValidationAlert.Warning,
        });

        var outcome = Validator.Validate(engine, 0, CellAddress.First, CellValue.Number(99));

        Assert.False(outcome.IsValid);
        Assert.False(outcome.Blocks);
    }

    [Fact]
    public void LaLunghezzaDelTestoSiControlla()
    {
        var (workbook, engine) = fixture.CreateWorkbook();

        workbook.Sheets[0].Validations.Add(new DataValidation
        {
            Range = Range(0, 0, 9, 0),
            Kind = ValidationKind.TextLength,
            Operator = ValidationOperator.LessOrEqual,
            FirstFormula = "3",
        });

        Assert.True(Validator.Validate(engine, 0, CellAddress.First, CellValue.Text("abc")).IsValid);
        Assert.False(Validator.Validate(engine, 0, CellAddress.First, CellValue.Text("abcd")).IsValid);
        Assert.False(Validator.Validate(engine, 0, CellAddress.First, CellValue.Number(12345)).IsValid);
        Assert.True(Validator.Validate(engine, 0, CellAddress.First, CellValue.Number(123)).IsValid);
    }

    [Fact]
    public void LaFormulaPersonalizzataERelativaAllInizioDellIntervallo()
    {
        var (workbook, engine) = fixture.CreateWorkbook();

        workbook.Sheets[0].Validations.Add(new DataValidation
        {
            Range = Range(0, 0, 9, 0),
            Kind = ValidationKind.Custom,
            FirstFormula = "=B1>2",
        });

        engine.SetValue(new SheetCell(0, new CellAddress(0, 1)), CellValue.Number(5));
        engine.SetValue(new SheetCell(0, new CellAddress(3, 1)), CellValue.Number(1));

        Assert.True(Validator.Validate(engine, 0, CellAddress.First, CellValue.Number(1)).IsValid);
        Assert.False(Validator.Validate(engine, 0, new CellAddress(3, 0), CellValue.Number(1)).IsValid);
    }

    [Fact]
    public void GliEstremiConRiferimentiSonoRelativiAllaCella()
    {
        var (workbook, engine) = fixture.CreateWorkbook();

        workbook.Sheets[0].Validations.Add(new DataValidation
        {
            Range = Range(0, 0, 9, 0),
            Kind = ValidationKind.Decimal,
            Operator = ValidationOperator.Less,
            FirstFormula = "B1",
        });

        engine.SetValue(new SheetCell(0, new CellAddress(2, 1)), CellValue.Number(100));

        Assert.True(Validator.Validate(engine, 0, new CellAddress(2, 0), CellValue.Number(50)).IsValid);
    }
}
