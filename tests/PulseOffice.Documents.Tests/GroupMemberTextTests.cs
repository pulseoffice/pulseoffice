using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Drawing.Shapes;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class GroupMemberTextTests
{
    private readonly IGroupMemberText _text = new GroupMemberText();

    [Fact]
    public void I_membri_con_una_storia_non_disegnano_il_proprio_testo_piano_perche_la_storia_la_impagina_il_layout()
    {
        var inner = new ShapeGroupObject
        {
            Id = "inner",
            Children = [new GroupChild(new ShapeObject { Id = "nested", Shape = ShapeKind.Ellipse, Text = "Seconda" }, 0, 0, 1, 1)],
        };
        var group = new ShapeGroupObject
        {
            Id = "g",
            Children =
            [
                new GroupChild(new ShapeObject { Id = "member", Shape = ShapeKind.Rectangle, Text = "Forte" }, 0, 0, 0.5, 1),
                new GroupChild(new ShapeObject { Id = "plain", Shape = ShapeKind.Rectangle, Text = "Solo testo" }, 0, 0, 0.5, 1),
                new GroupChild(inner, 0.5, 0, 0.5, 1),
            ],
        };
        var stories = new Dictionary<string, BlockContainer>
        {
            ["member"] = new([new ParagraphBlock("p1", "Forte")]),
            ["nested"] = new([new ParagraphBlock("p2", "Seconda")]),
        };

        var painted = Assert.IsType<ShapeGroupObject>(_text.Painted(new EmbeddedContent(group, null, stories)));

        Assert.Null(Assert.IsType<ShapeObject>(painted.Children[0].Content).Text);
        Assert.Equal("Solo testo", Assert.IsType<ShapeObject>(painted.Children[1].Content).Text);
        Assert.Null(Assert.IsType<ShapeObject>(Assert.IsType<ShapeGroupObject>(painted.Children[2].Content).Children[0].Content).Text);
        Assert.Same(group, _text.Painted(new EmbeddedContent(group)));
    }
}
