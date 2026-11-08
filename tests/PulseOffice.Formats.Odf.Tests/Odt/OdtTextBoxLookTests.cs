using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class OdtTextBoxLookTests
{
    [Fact]
    public void Fill_and_outline_of_text_boxes_cross_the_odt()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([new ParagraphBlock("p1", "Body") { Marks = [new ObjectMark(0, "t1")] }]);
        document.Objects.Add(new AnchoredObject("t1", new CharacterAnchor("p1"),
            new TextBoxContent(new BlockContainer([new ParagraphBlock("b1", "Inside")]))
            {
                Look = new FrameProperties { Background = 0xFFFFFF00, BorderColor = 0xFFFF0000, BorderWidth = 2 },
            },
            new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(2))));

        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = new OdtReader().Read(stream).Value!;

        var box = Assert.IsType<TextBoxContent>(Assert.Single(read.Objects.All).Content);
        Assert.Equal(0xFFFFFF00u, box.Look!.Background);
        Assert.Equal(0xFFFF0000u, box.Look.BorderColor);
        Assert.Equal(2, box.Look.BorderWidth!.Value, 1);
    }
}
