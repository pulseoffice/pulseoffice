using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round312ValidationTransposeTests
{
    private static CellRange R(string a1)
    {
        Assert.True(CellRange.TryParseA1(a1, out var range));
        return range;
    }

    [Fact]
    public void Transposed_paste_turns_the_validation_and_its_relative_references()
    {
        var sheet = new Worksheet("Sheet1");
        sheet.Validations.Add(new DataValidation { Range = R("A1:A3"), Kind = ValidationKind.Custom, FirstFormula = "B1>$Z$1" });
        var clipboard = new SheetClipboard();

        clipboard.Paste(clipboard.Copy(sheet, R("A1:A3")), sheet, R("D10").Start, R("A1").Start,
            new PasteRequest { Parts = PasteParts.Validation, Transpose = true });

        var pasted = Assert.Single(sheet.Validations, rule => rule.Range == R("D10:F10"));
        Assert.Equal("D11>$Z$1", pasted.FirstFormula);
    }

    [Fact]
    public void Plain_paste_still_shifts_the_relative_references()
    {
        var sheet = new Worksheet("Sheet1");
        sheet.Validations.Add(new DataValidation { Range = R("A1:A3"), Kind = ValidationKind.Custom, FirstFormula = "B1>0" });
        var clipboard = new SheetClipboard();

        clipboard.Paste(clipboard.Copy(sheet, R("A1:A3")), sheet, R("D10").Start, R("A1").Start,
            new PasteRequest { Parts = PasteParts.Validation });

        Assert.Equal("E10>0", Assert.Single(sheet.Validations, rule => rule.Range == R("D10:D12")).FirstFormula);
    }

    [Fact]
    public void A_fill_copy_clipped_at_the_first_row_keeps_the_references_of_its_own_cells()
    {
        var sheet = new Worksheet("Sheet1");
        sheet.Validations.Add(new DataValidation { Range = R("A3:A6"), Kind = ValidationKind.Custom, FirstFormula = "B3>0" });

        var plan = new FillValidations().Plan(sheet, R("A3:A6"), R("A1:A2"), vertical: true, backwards: true)!;

        Assert.Equal("B1>0", Assert.Single(plan, rule => rule.Range == R("A1:A2")).FirstFormula);
    }
}
