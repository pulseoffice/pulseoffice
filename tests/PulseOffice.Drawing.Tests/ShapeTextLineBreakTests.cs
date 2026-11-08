using PulseOffice.Drawing.Shapes;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class ShapeTextLineBreakTests
{
    [Fact]
    public void Un_a_capo_resta_nel_paragrafo_e_nel_suo_punto()
    {
        var letters = new ListNumberFormat { Style = ListNumberStyle.UpperLetter };
        var shape = new ShapeObject
        {
            Id = "Forma",
            Shape = ShapeKind.Rectangle,
            Text = "Uno" + ShapeTextRun.LineBreak + "dopo\nDue",
            Lists = [new ShapeListItem { Numbered = true, NumberFormat = letters }, new ShapeListItem { Numbered = true, NumberFormat = letters }],
        };
        var rich = new ShapeRichText();

        var text = rich.ToRich(shape);

        Assert.Equal(2, text.Paragraphs.Count);
        Assert.Equal("Uno" + ShapeTextRun.LineBreak + "dopo", text.Paragraphs[0].Text);
        Assert.Equal(["A.", "B."], new ListLabels().For(text.Paragraphs));
        var back = rich.FromRich(shape, text);
        Assert.Equal(shape.Text, back.Text);
        Assert.Equal(shape.Lists, back.Lists);
    }
}
