using PulseOffice.Drawing.Shapes;
using PulseOffice.UI.Controls.Shapes;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round292UnseenListLabelTests
{
    [Fact]
    public void A_numbered_paragraph_whose_text_is_wholly_transparent_hides_its_label()
    {
        var shape = new ShapeObject
        {
            Id = "s",
            Shape = ShapeKind.Rectangle,
            Text = "one\ntwo",
            Runs = [new ShapeTextRun("one\n"), new ShapeTextRun("two") { ColorHex = "#00000000" }],
            Lists = [new ShapeListItem { Numbered = true }, new ShapeListItem { Numbered = true }],
        };

        var framed = new ShapeListText().Framed(shape, 0);

        Assert.Null(framed.Paragraphs[0].Color);
        Assert.Equal(0u, framed.Paragraphs[1].Color);
    }
}
