using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round313ListLevelTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static TextDocument Listed()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.List.Add(new ListStyle("Aligned", [
            new ListLevel(NumberFormat.UpperRoman) { Alignment = ListNumberAlignment.Right },
            new ListLevel(NumberFormat.LowerLetter) { Alignment = ListNumberAlignment.Center, RestartAfter = 0 },
            new ListLevel(NumberFormat.Arabic) { RestartAfter = 1 }]));
        document.Styles.Paragraph.Add(new ParagraphStyle("Item") { ParentName = DefaultStyleNames.Standard, ListStyleName = "Aligned" });
        document.Body.Replace([new ParagraphBlock("p", "x", "Item")]);
        return document;
    }

    private static void AssertLevels(TextDocument read)
    {
        var levels = read.Styles.List.Find("Aligned")!.Levels;
        Assert.Equal([ListNumberAlignment.Right, ListNumberAlignment.Center, ListNumberAlignment.Left], levels.Take(3).Select(level => level.Alignment));
        Assert.Equal(new int?[] { null, 0, 1 }, levels.Take(3).Select(level => level.RestartAfter));
    }

    [Fact]
    public void Number_alignment_and_restart_level_cross_docx()
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(Listed(), stream).Succeeded);
        stream.Position = 0;
        AssertLevels(_composer.Reader(DocxFlavour.Document).Read(stream).Value!);
    }

    [Fact]
    public void Number_alignment_and_restart_level_cross_odt()
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(Listed(), stream).Succeeded);
        stream.Position = 0;
        AssertLevels(new OdtReader().Read(stream).Value!);
    }

    [Fact]
    public void Word_level_elements_keep_the_schema_order()
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(Listed(), stream).Succeeded);
        stream.Position = 0;
        using var package = WordprocessingDocument.Open(stream, false);
        var level = package.MainDocumentPart!.NumberingDefinitionsPart!.Numbering!.Descendants<W.Level>().First(item => item.GetFirstChild<W.LevelRestart>() is not null);
        var names = level.ChildElements.Select(child => child.LocalName).ToList();
        Assert.True(names.IndexOf("numFmt") < names.IndexOf("lvlRestart"));
        Assert.True(names.IndexOf("lvlRestart") < names.IndexOf("lvlText"));
        Assert.True(names.IndexOf("lvlText") < names.IndexOf("lvlJc"));
    }

    [Fact]
    public void Levels_an_abstract_numbering_does_not_define_show_no_number()
    {
        var numbering = $"<w:numbering {Namespace}><w:abstractNum w:abstractNumId=\"0\"><w:name w:val=\"Sparse\"/><w:lvl w:ilvl=\"0\"><w:numFmt w:val=\"decimal\"/><w:lvlText w:val=\"%1.\"/><w:lvlJc w:val=\"right\"/></w:lvl></w:abstractNum><w:num w:numId=\"1\"><w:abstractNumId w:val=\"0\"/></w:num></w:numbering>";
        var read = _composer.Reader(DocxFlavour.Document).Read(Docx(numbering)).Value!;
        var levels = read.Styles.List.Find("Sparse")!.Levels;
        Assert.Equal(ListNumberAlignment.Right, levels[0].Alignment);
        Assert.Equal(NumberFormat.None, levels[1].Format);
    }

    private static MemoryStream Docx(string numbering)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document($"<w:document {Namespace}><w:body><w:p><w:pPr><w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"1\"/></w:numPr></w:pPr><w:r><w:t>item</w:t></w:r></w:p></w:body></w:document>");
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = new W.Numbering(numbering);
        }

        stream.Position = 0;
        return stream;
    }
}
