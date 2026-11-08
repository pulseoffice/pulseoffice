using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.Foundation.Zoom;
using PulseOffice.Presentations;
using PulseOffice.Testing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckRound57DialogTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(DeckHeadlessApp));

    private static (Presentation Deck, DialogAdmissionHarness Harness) Scene()
    {
        var deck = new Presentation();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank));
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank));
        var shape = new TextElement { Text = new PulseOffice.Text.Rich.RichText(PlaceholderRole.Free, [new PulseOffice.Text.Rich.TextParagraph("x")]), Id = "shape-1", Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(1000), new Twip(500)) };
        deck.Slides[0].Add(shape);
        var actions = new DeckActions(deck, new UndoStack(), new SlideFactory(), new FakeTextCatalog());
        var context = new ActiveDeckContext();
        context.Attach(new DeckTarget(deck, actions, () => deck.Slides.Count > 0 ? deck.Slides[0] : null, () => 0, () => { },
            _ => { }, () => false, () => shape, () => [shape], _ => { }, _ => { }, () => null,
            () => Task.CompletedTask, _ => { }, () => DeckViewMode.Normal, () => null,
            () => new ViewZoom(ViewZoomMode.FitWhole, 1), _ => { }, () => 1, () => false, _ => false,
            new DeckViewOptions(), _ => { }, (_, _) => false, (_, _, _) => false, () => null, () => null, () => null));
        return (deck, new DialogAdmissionHarness(ModuleKind.Presentations, new DeckStateWitness(context)));
    }

    [Fact]
    public Task The_slide_deleted_while_the_dialog_is_open_drops_the_answer() => Session.Dispatch(async () =>
    {
        var (deck, harness) = Scene();
        using (harness)
        {
            var slide = deck.Slides[0];

            var answer = await harness.AnswerAfter(() => deck.Remove(slide));

            Assert.Null(answer);
            Assert.Contains(harness.Texts["status.dialog.stale.target"], harness.Reported);
        }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task The_selected_shape_deleted_while_the_dialog_is_open_drops_the_answer() => Session.Dispatch(async () =>
    {
        var (deck, harness) = Scene();
        using (harness)
        {
            var answer = await harness.AnswerAfter(() => deck.Slides[0].Remove("shape-1"));

            Assert.Null(answer);
        }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task The_document_made_read_only_while_the_dialog_is_open_drops_the_answer() => Session.Dispatch(async () =>
    {
        var (_, harness) = Scene();
        using (harness)
        {
            var answer = await harness.AnswerAfter(() => harness.Document.IsReadOnly = true);

            Assert.Null(answer);
        }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Another_slide_removed_keeps_the_answer() => Session.Dispatch(async () =>
    {
        var (deck, harness) = Scene();
        using (harness)
        {
            var answer = await harness.AnswerAfter(() => deck.Remove(deck.Slides[1]));

            Assert.Equal("applied", answer);
        }
    }, TestContext.Current.CancellationToken);
}
