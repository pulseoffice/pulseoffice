using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round309ValidationPasteTests
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
    public void A_pasted_custom_rule_moves_its_relative_references_with_the_cells()
    {
        var sheet = new Worksheet("S");
        sheet.Validations.Add(new DataValidation { Range = R("A1:A5"), Kind = ValidationKind.Custom, FirstFormula = "A1>0" });
        var clipboard = new SheetClipboard();
        var snapshot = clipboard.Copy(sheet, R("A3:A4"));

        clipboard.Paste(snapshot, sheet, A("C1"), A("A3"), new PasteRequest { Parts = PasteParts.Validation });

        var pasted = sheet.Validations[^1];
        Assert.Equal(R("C1:C2"), pasted.Range);
        Assert.Contains("C1", pasted.FirstFormula, StringComparison.Ordinal);
    }

    [Fact]
    public void Pasting_over_part_of_a_rule_cuts_the_old_rule_out_of_the_target()
    {
        var sheet = new Worksheet("S");
        sheet.Validations.Add(new DataValidation { Range = R("A1:A2"), Kind = ValidationKind.WholeNumber, FirstFormula = "1", SecondFormula = "9" });
        sheet.Validations.Add(new DataValidation { Range = R("C1:C10"), Kind = ValidationKind.Decimal, FirstFormula = "0", SecondFormula = "1" });
        var clipboard = new SheetClipboard();
        var snapshot = clipboard.Copy(sheet, R("A1:A2"));

        clipboard.Paste(snapshot, sheet, A("C4"), A("A1"), new PasteRequest { Parts = PasteParts.Validation });

        var validator = new DataValidator();
        Assert.Equal(ValidationKind.WholeNumber, validator.RuleAt(sheet, A("C5"))!.Kind);
        Assert.Equal(ValidationKind.Decimal, validator.RuleAt(sheet, A("C3"))!.Kind);
        Assert.Equal(ValidationKind.Decimal, validator.RuleAt(sheet, A("C6"))!.Kind);
        Assert.DoesNotContain(sheet.Validations, rule => rule.Kind == ValidationKind.Decimal && rule.Range.Contains(A("C4")));
    }
}
