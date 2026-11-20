using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Formats.Packaging;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round309OdsStyleMapSizeTests
{
    private readonly XNamespace _style = OdfPackage.Namespaces.Style;
    private readonly XNamespace _table = OdfPackage.Namespaces.Table;

    private static (XDocument Content, MemoryStream Package) Written(Workbook workbook)
    {
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
    public void A_whole_column_rule_gives_one_derived_style_and_a_bounded_document_that_stays_stable_on_reopening()
    {
        const int rows = 60;
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("S");
        for (var row = 0; row < rows; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
            sheet.SetValue(new CellAddress(row, 3), CellValue.Number(row * 2));
        }
        var column = new CellRange(new CellAddress(0, 0), new CellAddress(CellAddress.MaxRows - 1, 0));
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = column,
            Operator = ConditionOperator.GreaterThan,
            Operands = ["5"],
            Style = CellStyle.Default with { Font = CellStyle.Default.Font with { Bold = true } },
        });

        var (first, package) = Written(workbook);
        using (package)
        {
            Assert.Single(first.Descendants(_style + "style"), style => style.Elements(_style + "map").Any());
            Assert.InRange(first.Descendants(_table + "table-cell").Count(), rows, rows * 4 + 8);

            var reread = new OdsReader().Read(package);
            Assert.True(reread.Succeeded);
            Assert.Equal(column, Assert.Single(reread.Value!.Sheets[0].ConditionalRules).Range);

            var (second, again) = Written(reread.Value!);
            using (again)
            {
                Assert.Single(second.Descendants(_style + "style"), style => style.Elements(_style + "map").Any());
                Assert.InRange(second.Descendants(_table + "table-cell").Count(), rows, first.Descendants(_table + "table-cell").Count() + 4);
            }
        }
    }
}
