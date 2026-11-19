using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class Round263OdtCommonColumnStyleTests
{
    private static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";

    private static XDocument Load(ZipArchive zip, string path)
    {
        using var input = zip.GetEntry(path)!.Open();
        return XDocument.Load(input);
    }

    private static void Save(ZipArchive zip, string path, XDocument document)
    {
        zip.GetEntry(path)!.Delete();
        using var output = zip.CreateEntry(path).Open();
        document.Save(output);
    }

    [Fact]
    public void A_column_width_in_a_common_style_is_read()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var row = new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("a", "a")])), new TableCell(new BlockContainer([new ParagraphBlock("b", "b")]))]);
        document.Body.Replace([new TableBlock("t", [row], [new Twip(2000), new Twip(3000)]), new ParagraphBlock("p", "")]);
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, true))
        {
            var content = Load(zip, "content.xml");
            var styles = Load(zip, "styles.xml");
            var name = (string?)content.Descendants(Table + "table-column").First().Attribute(Table + "style-name");
            var style = content.Descendants(Style + "style").Single(element => (string?)element.Attribute(Style + "name") == name);
            style.Remove();
            styles.Root!.Element(Office + "styles")!.Add(style);
            Save(zip, "content.xml", content);
            Save(zip, "styles.xml", styles);
        }
        stream.Position = 0;

        var table = new OdtReader().Read(stream).Value!.Body.Blocks.OfType<TableBlock>().Single();

        Assert.InRange(table.Columns[0].Value, 1980, 2020);
    }
}
