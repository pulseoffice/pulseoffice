using PulseOffice.Document.Commands;
using PulseOffice.Presentations;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round245SlideDragSessionTests
{
    [Fact]
    public void A_drag_resolves_to_select_move_or_empty_section_drop()
    {
        var deck = new Presentation();
        var factory = new SlideFactory();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        for (var i = 0; i < 3; i++) deck.AddExisting(factory.Create(deck.NextId(), deck.Master, SlideLayout.Blank));
        var actions = new DeckActions(deck, new UndoStack(), factory, new FakeTextCatalog());
        var headers = new DeckSectionHeaders(actions, new FakeTextCatalog(), DesignTokens.For(InterfaceDensity.Compact));
        var session = new SlideDragSession(headers, deck, actions, horizontal: false);
        var empty = new EmptySection("Later", "s1", null);

        Assert.Equal(SlideDropKind.None, session.Resolve(null, 2).Kind);
        session.Begin(deck.Slides[1], new Avalonia.Point(10, 10), new Avalonia.Size(4, 4));
        Assert.Equal(SlideDropKind.Select, session.Resolve(null, 0).Kind);
        Assert.False(session.Arm(new Avalonia.Point(11, 11)));
        Assert.Equal(SlideDropKind.Select, session.Resolve(null, 0).Kind);
        Assert.True(session.Arm(new Avalonia.Point(10, 13)));

        Assert.Equal(SlideDropKind.Select, session.Resolve(null, 1).Kind);
        Assert.Equal(SlideDropKind.Select, session.Resolve(null, -1).Kind);
        Assert.Equal(new SlideDrop(SlideDropKind.Move, 0), session.Resolve(null, 0));
        Assert.Equal(new SlideDrop(SlideDropKind.IntoSection, Section: empty), session.Resolve(empty, 0));

        session.Cancel();
        Assert.Equal(-1, session.From);
        Assert.False(session.Dragging);
    }
}
