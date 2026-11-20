using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using PulseOffice.Formats.Packaging;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round304OdsStyleMapTests
{
    private readonly XNamespace _style = OdfPackage.Namespaces.Style;
    private readonly XNamespace _table = OdfPackage.Namespaces.Table;

    private static CellRange R(string a1)
    {
        Assert.True(CellRange.TryParseA1(a1, out var range));
        return range;
    }

    private static (XDocument Content, MemoryStream Package) Written(ConditionalRule rule)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("S");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(7));
        sheet.ConditionalRules.Add(rule);
        var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        using var input = archive.GetEntry("content.xml")!.Open();
        var content = XDocument.Load(input);
        stream.Position = 0;
        return (content, stream);
    }

    [Fact]
    public void A_cell_value_rule_is_also_written_as_a_standard_style_map_on_the_cells_of_its_range()
    {
        var rule = new ConditionalRule { Range = R("A1:B2"), Operator = ConditionOperator.GreaterThan, Operands = ["5"], Style = CellStyle.Default with { Font = CellStyle.Default.Font with { Bold = true } } };
        var (content, package) = Written(rule);
        using (package)
        {
            var mapped = content.Descendants(_style + "style").Where(style => style.Elements(_style + "map").Any()).ToList();
            var map = Assert.Single(mapped).Element(_style + "map")!;
            Assert.Equal("of:cell-content()>5", (string?)map.Attribute(_style + "condition"));
            var name = (string?)mapped[0].Attribute(_style + "name");
            var cells = content.Descendants(_table + "table-cell").Where(cell => (string?)cell.Attribute(_table + "style-name") == name).ToList();
            Assert.NotEmpty(cells);

            var reread = new OdsReader().Read(package);
            Assert.True(reread.Succeeded);
            Assert.Equal(R("A1:B2"), Assert.Single(reread.Value!.Sheets[0].ConditionalRules).Range);
        }
    }

    [Fact]
    public void Visual_rules_write_no_style_map()
    {
        var rule = new ConditionalRule { Range = R("A1"), Visual = ConditionalVisual.ColorScale, ScaleColors = ["#F8696B", "#63BE7B"] };
        var (content, package) = Written(rule);
        using (package)
            Assert.DoesNotContain(content.Descendants(_style + "map"), _ => true);
    }

    [Fact]
    public void Cells_taking_a_mapped_style_from_their_column_default_are_covered()
    {
        const string document = """
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0" xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0" office:version="1.3" office:mimetype="application/vnd.oasis.opendocument.spreadsheet">
            <office:styles><style:style style:name="Good" style:family="table-cell"><style:table-cell-properties fo:background-color="#c6efce"/></style:style></office:styles>
            <office:automatic-styles><style:style style:name="ce1" style:family="table-cell"><style:map style:condition="cell-content()&gt;5" style:apply-style-name="Good" style:base-cell-address="S.B1"/></style:style></office:automatic-styles>
            <office:body><office:spreadsheet><table:table table:name="S"><table:table-column/><table:table-column table:default-cell-style-name="ce1"/><table:table-column/>
            <table:table-row table:number-rows-repeated="2"><table:table-cell table:number-columns-repeated="3"/></table:table-row>
            </table:table></office:spreadsheet></office:body></office:document>
            """;
        var sheet = new OdsSpreadsheetReader().Read(new MemoryStream(Encoding.UTF8.GetBytes(document))).Value!.Sheets[0];

        Assert.Equal(R("B1:B2"), Assert.Single(sheet.ConditionalRules).Range);
    }
}
