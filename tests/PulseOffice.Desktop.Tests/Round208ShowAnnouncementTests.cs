using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI;
using PulseOffice.Testing;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class Round208ShowAnnouncementTests(HeadlessDesktopSession headless)
{
    private readonly UiThreadSession _session = headless.Session;

    [Fact]
    public Task Blank_screens_typed_numbers_slides_and_the_end_are_announced() => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        suite.Activate(ModuleKind.Presentations);
        var target = suite.Deck;
        target.Actions.AddSlide(SlideLayout.Blank, 0);
        target.Actions.AddSlide(SlideLayout.Blank, 1);
        Dispatcher.UIThread.RunJobs();
        var texts = suite.Service<ITextCatalog>();
        var renderer = new SlideRenderer(suite.Service<PulseOffice.UI.Controls.Shapes.IDrawingObjectRenderer>(), texts);
        var window = new PresentationWindow(target.Deck, suite.Service<IAnimationScheduler>(), renderer,
            new PresentationOptions(0) { WithoutAnimation = true, Playlist = new ShowPlaylist().Main(target.Deck), EndOfShowText = "end", Texts = texts });
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var total = window.SlideTotal;
        try
        {
            Assert.Equal(texts.Format("deck.present.announce.slide", 1, total), window.Announcement);

            Press(window, Key.B);
            Assert.Equal(texts["deck.present.announce.black"], window.Announcement);
            Press(window, Key.W);
            Assert.Equal(texts["deck.present.announce.white"], window.Announcement);
            Press(window, Key.Space);
            Assert.Equal(texts.Format("deck.present.announce.slide", 1, total), window.Announcement);

            Press(window, Key.NumPad2);
            Assert.Equal(texts.Format("deck.present.announce.typed", "2"), window.Announcement);
            Press(window, Key.Enter);
            Assert.Equal(texts.Format("deck.present.announce.slide", 2, total), window.Announcement);

            Press(window, Key.End);
            Press(window, Key.Right);
            Assert.Equal(ShowCurtain.End, window.Screen);
            Assert.Equal("end", window.Announcement);
        }
        finally
        {
            if (window.IsVisible) window.Close();
        }
    }, CancellationToken.None);

    private static void Press(Avalonia.Controls.Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();
    }
}
