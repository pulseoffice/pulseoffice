using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxTextBoxLookTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void Fill_and_outline_of_linked_text_boxes_cross_the_docx()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([new ParagraphBlock("p1", "Body") { Marks = [new ObjectMark(0, "t1"), new ObjectMark(0, "t2")] }]);
        var look = new FrameProperties { Background = 0xFFFFFF00, BorderColor = 0xFFFF0000, BorderWidth = 2 };
        document.Objects.Add(new AnchoredObject("t1", new CharacterAnchor("p1"),
            new TextBoxContent(new BlockContainer([new ParagraphBlock("b1", "First")])) { AutoGrowHeight = false, NextFrameName = "Second", Look = look },
            new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(2))) { Name = "First" });
        document.Objects.Add(new AnchoredObject("t2", new CharacterAnchor("p1"),
            new TextBoxContent(new BlockContainer([new ParagraphBlock("b2", string.Empty, DefaultStyleNames.Standard)])) { AutoGrowHeight = false, Look = look },
            new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(2))) { Name = "Second" });

        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);

        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX184");
        var boxes = read.Value!.Objects.All.Select(item => item.Content).OfType<TextBoxContent>().ToList();
        Assert.Equal(2, boxes.Count);
        Assert.All(boxes, box =>
        {
            Assert.Equal(0xFFFFFF00u, box.Look!.Background);
            Assert.Equal(0xFFFF0000u, box.Look.BorderColor);
            Assert.Equal(2, box.Look.BorderWidth!.Value, 1);
        });
    }

    [Theory]
    [InlineData(1e9, 1584)]
    [InlineData(-3, 0)]
    public void An_out_of_range_border_width_is_written_as_a_valid_outline(double width, double expected)
    {
        var outline = new DocxTextBoxLook(new XlsxShapeStyleMapper()).Write(new FrameProperties { BorderWidth = width })
            .OfType<DocumentFormat.OpenXml.Drawing.Outline>().Single();

        Assert.Equal(expected * 12700, outline.Width!.Value);
    }
}
