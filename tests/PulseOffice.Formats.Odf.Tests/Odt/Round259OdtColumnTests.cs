using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class Round259OdtColumnTests
{
    private static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";

    [Fact]
    public void A_column_without_width_takes_what_the_table_leaves()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var row = new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("a", "a")])), new TableCell(new BlockContainer([new ParagraphBlock("b", "b")]))]);
        document.Body.Replace([new TableBlock("t", [row], [new Twip(2000), new Twip(3000)]), new ParagraphBlock("p", "")]);
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, true))
        {
            var entry = zip.GetEntry("content.xml")!;
            XDocument content;
            using (var input = entry.Open()) content = XDocument.Load(input);
            var second = (string?)content.Descendants(Table + "table-column").ElementAt(1).Attribute(Table + "style-name");
            var properties = content.Descendants(Style + "style").Single(style => (string?)style.Attribute(Style + "name") == second).Element(Style + "table-column-properties")!;
            properties.Attribute(Style + "column-width")!.Remove();
            properties.Attribute(Style + "rel-column-width")?.Remove();
            entry.Delete();
            using var output = zip.CreateEntry("content.xml").Open();
            content.Save(output);
        }
        stream.Position = 0;

        var table = new OdtReader().Read(stream).Value!.Body.Blocks.OfType<TableBlock>().Single();

        Assert.InRange(table.Columns[1].Value, 2980, 3020);
    }
}
