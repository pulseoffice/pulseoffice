using Avalonia.Threading;
using PulseOffice.Foundation.Modules;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI;
using PulseOffice.Testing;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class Round189DeckPdfHiddenSlidesTests(HeadlessDesktopSession headless)
{
    private readonly UiThreadSession _session = headless.Session;

    [Fact]
    public Task Pdf_export_includes_hidden_slides_only_when_asked() => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        suite.Activate(ModuleKind.Presentations);
        var target = suite.Deck;
        var hidden = target.Actions.AddSlide(SlideLayout.TitleOnly, 0)!;
        target.Actions.SetHidden(hidden, true);
        Dispatcher.UIThread.RunJobs();
        var publisher = suite.Service<IDeckPublisher>();
        var count = target.Deck.Slides.Count;

        Assert.Equal(count - 1, publisher.BuildExportable(target.Deck, DeckExportLayout.Slides).PageCount);
        Assert.Equal(count, publisher.BuildExportable(target.Deck, DeckExportLayout.Slides, includeHidden: true).PageCount);
        Assert.Equal(count, publisher.BuildExportable(target.Deck, DeckExportLayout.Notes, includeHidden: true).PageCount);
    }, CancellationToken.None);
}
