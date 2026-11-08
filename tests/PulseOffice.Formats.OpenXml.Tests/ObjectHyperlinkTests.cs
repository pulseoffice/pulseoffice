using PulseOffice.Documents;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class ObjectHyperlinkTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void Il_collegamento_di_una_forma_attraversa_docx_e_odt()
    {
        var source = ThroughDocx(LinkedShape(ParagraphOf));
        Assert.Equal("https://example.org/", Linked(source).Hyperlink);
        Assert.Equal("https://example.org/", Linked(ThroughOdt(source)).Hyperlink);
        Assert.Equal("https://example.org/", Linked(ThroughDocx(ThroughOdt(source))).Hyperlink);
    }

    [Fact]
    public void Il_collegamento_di_un_oggetto_ancorato_alla_pagina_si_dichiara_in_odt()
    {
        var document = LinkedShape(_ => new PageAnchor(1));
        Assert.Contains(new OdtWriter().CheckFidelity(document).Losses, loss => loss.Code == "ODT009");
        Assert.DoesNotContain(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "ODT009");
    }

    private static ObjectAnchor ParagraphOf(string paragraphId) => new ParagraphAnchor(paragraphId);

    private static TextDocument LinkedShape(Func<string, ObjectAnchor> anchor)
    {
        var document = new TextDocument();
        var paragraph = new PulseOffice.Documents.Blocks.ParagraphBlock("p1", "Testo");
        document.Body.Replace([paragraph]);
        var shape = new PulseOffice.Drawing.Shapes.ShapeObject { Id = "s1", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle };
        document.Objects.Add(new AnchoredObject("obj1", anchor(paragraph.Id), new EmbeddedContent(shape), new PulseOffice.Foundation.Units.DocumentSize(new PulseOffice.Foundation.Units.Twip(1440), new PulseOffice.Foundation.Units.Twip(1440)))
        {
            Hyperlink = "https://example.org/",
        });
        return document;
    }

    private static AnchoredObject Linked(TextDocument document) => document.Objects.All.Single();

    private TextDocument ThroughDocx(TextDocument document)
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        return _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
    }

    private static TextDocument ThroughOdt(TextDocument document)
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        return new OdtReader().Read(stream).Value!;
    }
}
