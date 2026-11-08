using PulseOffice.Foundation.Zoom;
using PulseOffice.Document.Commands;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class ActiveDeckContextTests
{
    private static DeckTarget Target()
    {
        var deck = new Presentation();
        var actions = new DeckActions(deck, new UndoStack(), new SlideFactory(), new FakeTextCatalog());

        return new DeckTarget(deck, actions, () => null, () => 0, () => { },
            _ => { }, () => false, () => null, () => [], _ => { }, _ => { }, () => null,
            () => Task.CompletedTask, _ => { }, () => DeckViewMode.Normal, () => null,
            () => new ViewZoom(ViewZoomMode.FitWhole, 1), _ => { }, () => 1, () => false, _ => false,
            new DeckViewOptions(), _ => { }, (_, _) => false, (_, _, _) => false, () => null, () => null, () => null);
    }

    [Fact]
    public void RiannunciareLaVistaAttivaAvvisaRibbonEMenu()
    {
        var context = new ActiveDeckContext();
        var target = Target();
        context.Attach(target);
        var raised = 0;
        context.Changed += (_, _) => raised++;

        context.Reannounce(target);

        Assert.Equal(1, raised);
        Assert.Same(target, context.Target);
    }

    [Fact]
    public void UnaVistaInSecondoPianoNonRubaIlContesto()
    {
        var context = new ActiveDeckContext();
        var active = Target();
        var background = Target();
        context.Attach(active);
        var raised = 0;
        context.Changed += (_, _) => raised++;

        context.Reannounce(background);

        Assert.Equal(0, raised);
        Assert.Same(active, context.Target);
    }
}
