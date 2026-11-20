using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round305StyleTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static TextDocument Linked()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.Paragraph.Add(new ParagraphStyle("Lead") { ParentName = DefaultStyleNames.Standard, LinkedStyleName = "Lead Char" });
        document.Styles.Character.Add(new CharacterStyle("Lead Char") { LinkedStyleName = "Lead" });
        document.Body.Replace([new ParagraphBlock("p", "x")]);
        return document;
    }

    [Fact]
    public void Words_default_paragraph_font_is_not_imported_as_a_custom_character_style()
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(Linked(), stream).Succeeded);
        using (var package = WordprocessingDocument.Open(stream, true))
        {
            var styles = package.MainDocumentPart!.StyleDefinitionsPart!.Styles!;
            styles.Append(new W.Style(new W.StyleName { Val = "Default Paragraph Font" }) { Type = W.StyleValues.Character, Default = true, StyleId = "DefaultParagraphFont" });
            var partner = styles.Elements<W.Style>().Single(style => style.StyleName?.Val?.Value == "Lead Char");
            partner.InsertAfter(new W.BasedOn { Val = "DefaultParagraphFont" }, partner.StyleName);
            styles.Save();
        }
        stream.Position = 0;

        var read = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;

        Assert.Null(read.Styles.Character.Find("Default Paragraph Font"));
        Assert.Null(read.Styles.Character.Find("Lead Char")!.ParentName);
        Assert.Equal("Lead", read.Styles.Character.Find("Lead Char")!.LinkedStyleName);
    }

    [Fact]
    public void Linked_styles_are_written_with_the_extension_other_odf_readers_know_and_read_from_it()
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(Linked(), stream).Succeeded);
        using var edited = new MemoryStream();
        stream.Position = 0;
        stream.CopyTo(edited);
        using (var archive = new ZipArchive(edited, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("styles.xml")!;
            XDocument styles;
            using (var reader = entry.Open()) styles = XDocument.Load(reader);
            Assert.Contains(styles.Descendants().Attributes(), attribute => attribute.Name.LocalName == "linked-style-name" && attribute.Name.NamespaceName != "urn:pulseoffice:text");
            styles.Descendants().Attributes().Where(attribute => attribute.Name.LocalName == "linked-style-name" && attribute.Name.NamespaceName == "urn:pulseoffice:text").Remove();
            entry.Delete();
            using var writer = archive.CreateEntry("styles.xml").Open();
            styles.Save(writer);
        }
        edited.Position = 0;

        var read = new OdtReader().Read(edited).Value!;

        Assert.Equal("Lead Char", read.Styles.Paragraph.Find("Lead")!.LinkedStyleName);
    }
}

public sealed class Round305DanglingLinkTests
{
    [Fact]
    public void A_link_to_a_style_the_file_does_not_hold_is_dropped_on_reading()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.Paragraph.Add(new ParagraphStyle("Lead") { ParentName = DefaultStyleNames.Standard, LinkedStyleName = "Missing" });
        document.Body.Replace([new ParagraphBlock("p", "x")]);
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;

        Assert.Null(new OdtReader().Read(stream).Value!.Styles.Paragraph.Find("Lead")!.LinkedStyleName);
    }
}
