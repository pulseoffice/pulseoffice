using Avalonia;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Controls.Text.Rich;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round234UprightCellTests
{
    private sealed class PlainTheme : ITextTheme
    {
        public uint Resolve(ColorRole role) => 0xFF000000;

        public string MajorFont => "Inter";

        public string MinorFont => "Inter";
    }

    private readonly TextFlowFrame _flow = new();

    [Fact]
    public void Vertical_forms_replace_East_Asian_punctuation_one_for_one()
    {
        var forms = new VerticalForms();

        Assert.Equal("漢︑字︒︵a︶﹁b﹂", forms.Substitute("漢、字。（a）「b」"));
        Assert.Equal("abc", forms.Substitute("abc"));
    }

    [Fact]
    public Task Each_upright_cluster_gets_a_cell_as_long_as_its_glyph_is_tall() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var text = new RichText(PlaceholderRole.Body, [new TextParagraph("漢字、")])
        {
            Frame = new TextFrame { Layout = new TextLayout { Flow = TextFlow.EastAsianVertical } },
        };
        var segment = new TextFrameComposer(new ListLabels()).Compose(text, new Rect(0, 0, 4000, 20000), new PlainTheme(), new TextFields(), 1.0).Lines[0].Segments[0];

        Assert.Equal(3, segment.Upright.Count);
        var cells = segment.Upright
            .Select(glyph => segment.Text.BuildHighlightGeometry(default, glyph.Start - segment.Start, glyph.End - glyph.Start)!.Bounds)
            .ToList();
        for (var i = 0; i < cells.Count; i++)
            Assert.True(segment.Upright[i].Glyph.WidthIncludingTrailingWhitespace <= 0 || cells[i].Width >= segment.Upright[i].Glyph.Height - 0.5);
        for (var i = 1; i < cells.Count; i++)
            Assert.True(cells[i].Left >= cells[i - 1].Right - 0.5);
    }, CancellationToken.None);

    [Fact]
    public void Vertical_turned_and_upright_shape_text_is_composed_and_edited_in_place()
    {
        Assert.True(_flow.Composes(new TextLayout { Flow = TextFlow.Vertical }));
        Assert.True(_flow.Composes(new TextLayout { Flow = TextFlow.Vertical270 }));
        Assert.True(_flow.Composes(new TextLayout { Upright = true }));
        Assert.True(_flow.Composes(new TextLayout { Flow = TextFlow.Stacked }));
        Assert.False(_flow.Composes(TextLayout.Default));
        Assert.False(_flow.Composes(new TextLayout { Columns = 2 }));

        var shape = new ShapeObject
        {
            Id = "s",
            Shape = ShapeKind.Rectangle,
            Text = "abc",
            Style = ShapeStyle.Default with { Text = ShapeStyle.Default.Text with { Layout = new TextLayout { Flow = TextFlow.Vertical270 } } },
        };
        Assert.True(new ShapeListText().Draws(shape));
    }
}
