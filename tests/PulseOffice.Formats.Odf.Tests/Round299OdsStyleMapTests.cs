using System.Text;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class Round299OdsStyleMapTests
{
    private const string Head = """
        <?xml version="1.0" encoding="UTF-8"?>
        <office:document xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0" xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0" office:version="1.3" office:mimetype="application/vnd.oasis.opendocument.spreadsheet">
        <office:styles><style:style style:name="Good" style:family="table-cell"><style:table-cell-properties fo:background-color="#c6efce"/></style:style></office:styles>
        """;

    private static CellRange R(string a1)
    {
        Assert.True(CellRange.TryParseA1(a1, out var range));
        return range;
    }

    private static PulseOffice.Sheets.Worksheet Read(string automatic, string rows) =>
        new OdsSpreadsheetReader().Read(new MemoryStream(Encoding.UTF8.GetBytes(
            Head + "<office:automatic-styles>" + automatic + "</office:automatic-styles><office:body><office:spreadsheet><table:table table:name=\"S\">"
            + rows + "</table:table></office:spreadsheet></office:body></office:document>"))).Value!.Sheets[0];

    [Fact]
    public void Standard_style_maps_become_conditional_rules_over_the_cells_that_use_the_style()
    {
        var sheet = Read(
            """<style:style style:name="ce1" style:family="table-cell"><style:map style:condition="cell-content()&gt;5" style:apply-style-name="Good" style:base-cell-address="S.A1"/></style:style>""",
            """<table:table-row table:number-rows-repeated="3"><table:table-cell table:style-name="ce1" table:number-columns-repeated="2"/><table:table-cell/></table:table-row>""");

        var rule = Assert.Single(sheet.ConditionalRules);
        Assert.Equal(R("A1:B3"), rule.Range);
        Assert.Equal(ConditionOperator.GreaterThan, rule.Operator);
        Assert.Equal(["5"], rule.Operands);
        Assert.Equal("#C6EFCE", rule.Style.BackgroundHex?.ToUpperInvariant());
    }

    [Fact]
    public void A_comparison_with_a_cell_reference_becomes_a_formula_relative_to_the_rule()
    {
        var sheet = Read(
            """<style:style style:name="ce1" style:family="table-cell"><style:map style:condition="cell-content-is-between([.$D$1],10)" style:apply-style-name="Good" style:base-cell-address="S.A1"/></style:style>""",
            """<table:table-row><table:table-cell table:style-name="ce1"/></table:table-row>""");

        var rule = Assert.Single(sheet.ConditionalRules);
        Assert.Equal("=AND(A1>=$D$1,A1<=10)", rule.Formula);
    }
}
