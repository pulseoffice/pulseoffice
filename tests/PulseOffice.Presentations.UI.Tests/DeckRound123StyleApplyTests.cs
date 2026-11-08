using PulseOffice.Document.Commands;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckRound123StyleApplyTests
{
    private static readonly ShapeStyle Green = ShapeStyle.Default with { Fill = ShapeFill.Default with { ColorHex = "#16A34A" } };
    private static readonly DocumentRect Box = new(new Twip(100), new Twip(100), new Twip(1000), new Twip(1000));

    [Fact]
    public void Applying_a_style_restyles_only_the_selected_shape_not_its_identical_copy()
    {
        var deck = new Presentation();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        var actions = new DeckActions(deck, new UndoStack(), new SlideFactory(), new FakeTextCatalog());
        deck.GraphicStyles.Set(new GraphicStyle("Accent", Green));
        var slide = new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        deck.AddExisting(slide);
        var original = new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle };
        var copy = new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle };
        slide.Add(new ObjectElement { Id = "a", Bounds = Box, Content = original });
        slide.Add(new ObjectElement { Id = "b", Bounds = Box, Content = copy });

        actions.ApplyGraphicStyle([original], deck.GraphicStyles.Find("Accent")!);

        Assert.Equal("Accent", ((ShapeObject)((ObjectElement)slide.Find("a")!).Content).StyleName);
        Assert.Null(((ShapeObject)((ObjectElement)slide.Find("b")!).Content).StyleName);
    }
}
