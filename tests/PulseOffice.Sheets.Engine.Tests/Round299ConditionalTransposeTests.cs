using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round299ConditionalTransposeTests
{
    private static CellRange R(string a1)
    {
        Assert.True(CellRange.TryParseA1(a1, out var range));
        return range;
    }

    [Fact]
    public void Paste_with_transpose_carries_the_rule_on_the_turned_range_with_turned_relative_references()
    {
        var sheet = new Worksheet("Sheet1");
        sheet.ConditionalRules.Add(new ConditionalRule { Range = R("A1:A3"), Formula = "=B1>$Z$1" });
        var clipboard = new SheetClipboard();

        clipboard.Paste(clipboard.Copy(sheet, R("A1:A3")), sheet, R("D10").Start, R("A1").Start,
            new PasteRequest { Parts = PasteParts.StylesOnly, Transpose = true });

        var pasted = Assert.Single(sheet.ConditionalRules, rule => rule.Range == R("D10:F10"));
        Assert.Equal("=D11>$Z$1", pasted.Formula);
    }

    [Fact]
    public void Transposing_a_formula_swaps_relative_offsets_and_keeps_absolute_ones()
    {
        var translator = new FormulaTranslator();

        Assert.Equal("=SUM(C3:E4)", translator.Transpose("=SUM(A1:B3)", R("A1").Start, R("C3").Start));
    }
}
