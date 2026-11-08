using PulseOffice.Document.Commands;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Drawing.Shapes.Geometry;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckShapeAdjustmentTests
{
    [Fact]
    public void Regolare_una_forma_si_annulla_e_si_ripete()
    {
        var deck = new Presentation();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        var history = new UndoStack();
        var actions = new DeckActions(deck, history, new SlideFactory(), new FakeTextCatalog());
        var slide = new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        deck.AddExisting(slide);
        var element = new ObjectElement
        {
            Id = "forma",
            Bounds = new DocumentRect(new Twip(100), new Twip(100), new Twip(2000), new Twip(1000)),
            Content = new ShapeObject { Id = "forma", Shape = ShapeKind.RoundedRectangle },
        };
        slide.Add(element);
        var adjusted = new ShapeAdjustHandles().Drag((ShapeObject)element.Content, 0, new ShapePoint(0.2, 0), 2000, 1000);

        actions.AdjustShape(slide, element, adjusted);

        Assert.Equal(adjusted, ((ObjectElement)slide.Find("forma")!).Content);
        history.Undo();
        Assert.Null(((ShapeObject)((ObjectElement)slide.Find("forma")!).Content).Computed);
        history.Redo();
        Assert.IsType<PresetGeometry>(((ShapeObject)((ObjectElement)slide.Find("forma")!).Content).Computed);
    }
}
