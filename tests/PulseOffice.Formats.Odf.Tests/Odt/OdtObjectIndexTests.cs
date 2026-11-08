using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class OdtObjectIndexTests
{
    [Fact]
    public void LIndiceDegliOggettiAttraversaLOdtConIGeneriScelti()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var definition = new IndexDefinition(IndexKind.Objects) { Title = "Oggetti", ObjectSources = IndexObjectSources.Charts | IndexObjectSources.Other, Hyperlinks = false };
        document.Body.Replace(
        [
            new SectionBlock("ix", "Table of Objects1", new BlockContainer(
            [
                new ParagraphBlock("t", "Oggetti", DefaultStyleNames.ObjectIndexHeading),
                new ParagraphBlock("e", "Grafico1\t1", DefaultStyleNames.ObjectIndex1),
            ])) { Index = definition },
            new ParagraphBlock("p", "Testo"),
        ]);

        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, true))
        {
            using var content = zip.GetEntry("content.xml")!.Open();
            var xml = XDocument.Load(content);
            XNamespace text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
            var source = Assert.Single(xml.Descendants(text + "object-index-source"));
            Assert.Equal("true", (string?)source.Attribute(text + "use-chart-objects"));
            Assert.Equal("false", (string?)source.Attribute(text + "use-math-objects"));
        }
        stream.Position = 0;
        var read = new OdtReader().Read(stream).Value!;

        var section = Assert.Single(read.Body.Blocks.OfType<SectionBlock>());
        Assert.Equal(IndexKind.Objects, section.Index!.Kind);
        Assert.Equal(IndexObjectSources.Charts | IndexObjectSources.Other, section.Index.ObjectSources);
        Assert.Equal("Oggetti", section.Index.Title);
        Assert.Contains(section.Content.Blocks.OfType<ParagraphBlock>(), paragraph => paragraph.Text == "Grafico1\t1");
    }
}
