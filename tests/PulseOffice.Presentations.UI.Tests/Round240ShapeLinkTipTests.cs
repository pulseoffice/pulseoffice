using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PulseOffice.Document.Commands;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations.UI.Rendering;
using PulseOffice.Testing;
using PulseOffice.UI.Controls.Links;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Controls.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round240ShapeLinkTipTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(DeckHeadlessApp));

    [Fact]
    public Task A_link_inside_a_drawn_shape_gives_its_tip() => Session.Dispatch(() =>
    {
        var deck = new Presentation();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        var slide = new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        deck.AddExisting(slide);
        var shape = new ShapeObject
        {
            Id = "linked",
            Shape = ShapeKind.Rectangle,
            Text = "WWWWWWWWWWWWWWWW",
            Runs = [new ShapeTextRun("WWWWWWWWWWWWWWWW") { Link = "https://example.org" }],
        };
        var element = new ObjectElement
        {
            Id = deck.NextId(),
            Bounds = new(Twip.FromCentimeters(4), Twip.FromCentimeters(4), Twip.FromCentimeters(10), Twip.FromCentimeters(4)),
            ZOrder = slide.Elements.Count,
            Content = shape,
        };
        new DeckActions(deck, new UndoStack(), new SlideFactory(), new FakeTextCatalog()).InsertObject(slide, element);
        var view = new SlideView(new SlideRenderer(new DrawingObjectRenderer(), new FakeTextCatalog())) { Deck = deck, Size = deck.Size };
        view.Show(slide);
        var window = new Window { Width = 900, Height = 600, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var tips = new DeckLinkTips(new LinkTips(), new RichTextLinkHit(), new TextFrameComposer(new PulseOffice.Text.Rich.ListLabels()));

        Assert.Contains("example.org", tips.At(view, view.BoundsOf(element).Center), StringComparison.Ordinal);
        window.Close();
    }, CancellationToken.None);
}
