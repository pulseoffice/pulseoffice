using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round309FillValidationTests
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

    [Fact]
    public void Filling_down_carries_the_source_rule_and_moves_its_relative_references()
    {
        var sheet = new Worksheet("S");
        sheet.Validations.Add(new DataValidation { Range = R("A1"), Kind = ValidationKind.Custom, FirstFormula = "B1>0" });
        sheet.Validations.Add(new DataValidation { Range = R("A3:A4"), Kind = ValidationKind.Decimal, FirstFormula = "0", SecondFormula = "1" });

        var planned = new SheetFill().Validations(sheet, R("A1"), R("A1:A3"), FillDirection.Down)!;
        sheet.Validations.Clear();
        sheet.Validations.AddRange(planned);

        var validator = new DataValidator();
        var third = validator.RuleAt(sheet, A("A3"))!;
        Assert.Equal(ValidationKind.Custom, third.Kind);
        Assert.Contains("B3", third.FirstFormula, StringComparison.Ordinal);
        Assert.Equal(ValidationKind.Decimal, validator.RuleAt(sheet, A("A4"))!.Kind);
    }

    [Fact]
    public void Filling_from_cells_without_rules_over_cells_without_rules_changes_nothing()
    {
        var sheet = new Worksheet("S");
        sheet.Validations.Add(new DataValidation { Range = R("D1"), Kind = ValidationKind.WholeNumber, FirstFormula = "1", SecondFormula = "2" });

        Assert.Null(new SheetFill().Validations(sheet, R("A1"), R("A1:A5"), FillDirection.Down));
    }
}
