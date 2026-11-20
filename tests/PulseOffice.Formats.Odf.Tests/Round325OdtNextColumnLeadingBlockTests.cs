using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round325OdtNextColumnLeadingBlockTests
{
    private static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace Fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
    private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

    private static MemoryStream Written(Block leading)
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Add(new SectionBlock("s1", "Section1", new BlockContainer([leading]))
        {
            Columns = new SectionColumns(2, Twip.FromCentimeters(1)),
            StartsInNextColumn = true,
        });
        var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        return stream;
    }

    private static TableBlock Table1() =>
        new("t1", [new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("c1", "cell")]))])], [Twip.FromCentimeters(3)]);

    private static SectionBlock Nested() =>
        new("s2", "Section2", new BlockContainer([new ParagraphBlock("np", "nested", DefaultStyleNames.Standard)]));

    private static string? BreakOf(MemoryStream stream, XName element)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open());
        var content = XDocument.Parse(reader.ReadToEnd());
        stream.Position = 0;
        var target = content.Descendants(Text + "section").First().Descendants(element).First();
        var name = (string?)target.Attribute(element == Table + "table" ? Table + "style-name" : Text + "style-name");
        return content.Descendants(Style + "style").Where(style => (string?)style.Attribute(Style + "name") == name)
            .SelectMany(style => style.Elements()).Select(properties => (string?)properties.Attribute(Fo + "break-before")).FirstOrDefault(value => value is not null);
    }

    [Fact]
    public void A_section_starting_with_a_table_writes_a_native_column_break_on_the_table()
    {
        using var stream = Written(Table1());

        Assert.Equal("column", BreakOf(stream, Table + "table"));
        Assert.True(new OdtReader().Read(stream).Value!.Body.Blocks.OfType<SectionBlock>().Single().StartsInNextColumn);
    }

    [Fact]
    public void A_section_starting_with_a_nested_section_breaks_its_first_paragraph_and_reads_back_clean()
    {
        using var stream = Written(Nested());

        Assert.Equal("column", BreakOf(stream, Text + "p"));
        var section = new OdtReader().Read(stream).Value!.Body.Blocks.OfType<SectionBlock>().Single();
        Assert.True(section.StartsInNextColumn);
        var nested = Assert.IsType<SectionBlock>(section.Content.Blocks[0]);
        Assert.Null(((ParagraphBlock)nested.Content.Blocks[0]).Content.ColumnBreakBefore);
    }
}
