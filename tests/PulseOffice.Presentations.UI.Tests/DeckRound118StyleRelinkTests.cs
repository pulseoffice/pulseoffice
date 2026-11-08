using PulseOffice.Document.Commands;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckRound118StyleRelinkTests
{
    private static readonly ShapeStyle Green = ShapeStyle.Default with { Fill = ShapeFill.Default with { ColorHex = "#16A34A" } };
    private static readonly DocumentRect Box = new(new Twip(100), new Twip(100), new Twip(1000), new Twip(1000));

    private readonly Presentation _deck = new();
    private readonly UndoStack _history = new();
    private readonly DeckActions _actions;

    public DeckRound118StyleRelinkTests()
    {
        _deck.Master = new MasterFactory().CreateStandard(_deck.Theme);
        _actions = new DeckActions(_deck, _history, new SlideFactory(), new FakeTextCatalog());
        _deck.GraphicStyles.Set(new GraphicStyle("Accent", Green));
    }

    private static ShapeObject Shape(string id) => new() { Id = id, Shape = ShapeKind.Rectangle, Style = Green, StyleName = "Accent" };

    private static ObjectElement Element(string id, PulseOffice.Document.Embedding.EmbeddedObject content) => new() { Id = id, Bounds = Box, Content = content };

    private void Rename() => _actions.ChangeGraphicStyles(catalog => catalog.Rename("Accent", "Highlight"),
        styled => styled.StyleName == "Accent" ? styled.With(styled.Style, "Highlight") : null, "styles.rename.title");

    [Fact]
    public void Renaming_a_style_relinks_shapes_inside_nested_groups_and_undo_restores_them()
    {
        var slide = new SlideFactory().Create(_deck.NextId(), _deck.Master, SlideLayout.Blank);
        _deck.AddExisting(slide);
        var nested = new ShapeGroupObject { Id = "g2", Children = [new GroupChild(Shape("deep"), 0, 0, 1, 1)] };
        var group = new ShapeGroupObject { Id = "g", Children = [new GroupChild(Shape("inner"), 0, 0, 0.5, 0.5), new GroupChild(nested, 0.5, 0.5, 0.5, 0.5)] };
        slide.Add(Element("g", group));

        Rename();

        var renamed = (ShapeGroupObject)((ObjectElement)slide.Find("g")!).Content;
        Assert.Equal("Highlight", ((ShapeObject)renamed.Children[0].Content).StyleName);
        Assert.Equal("Highlight", ((ShapeObject)((ShapeGroupObject)renamed.Children[1].Content).Children[0].Content).StyleName);

        _history.Undo();
        Assert.Same(group, ((ObjectElement)slide.Find("g")!).Content);
    }

    [Fact]
    public void Renaming_a_style_relinks_master_and_layout_objects_and_undo_restores_them()
    {
        var before = _deck.Master;
        _deck.Master = before with
        {
            Objects = [Element("m1", Shape("m1"))],
            Layouts = [.. before.Layouts.Select((layout, index) => index == 0 ? layout with { Objects = [Element("l1", Shape("l1"))] } : layout)],
        };
        var seeded = _deck.Master;

        Rename();

        Assert.Equal("Highlight", ((ShapeObject)((ObjectElement)_deck.Master.Objects[0]).Content).StyleName);
        Assert.Equal("Highlight", ((ShapeObject)((ObjectElement)_deck.Master.Layouts[0].Objects[0]).Content).StyleName);

        _history.Undo();
        Assert.Same(seeded, _deck.Master);
    }
}
