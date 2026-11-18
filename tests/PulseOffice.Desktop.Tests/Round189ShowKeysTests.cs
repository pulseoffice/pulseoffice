using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using PulseOffice.Foundation.Modules;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI;
using PulseOffice.Testing;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class Round189ShowKeysTests(HeadlessDesktopSession headless)
{
    private readonly UiThreadSession _session = headless.Session;

    [Fact]
    public Task Show_keys_blank_the_screen_jump_by_number_and_end_on_a_black_screen() => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        suite.Activate(ModuleKind.Presentations);
        var target = suite.Deck;
        target.Actions.AddSlide(SlideLayout.Blank, 0);
        var hidden = target.Actions.AddSlide(SlideLayout.Blank, 1)!;
        target.Actions.SetHidden(hidden, true);
        Dispatcher.UIThread.RunJobs();
        var renderer = new SlideRenderer(suite.Service<PulseOffice.UI.Controls.Shapes.IDrawingObjectRenderer>(), suite.Service<PulseOffice.Localization.ITextCatalog>());
        var window = new PresentationWindow(target.Deck, suite.Service<IAnimationScheduler>(), renderer,
            new PresentationOptions(0) { WithoutAnimation = true, Playlist = new ShowPlaylist().Main(target.Deck), EndOfShowText = "end" });
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var closed = false;
        window.Closed += (_, _) => closed = true;
        try
        {
            Press(window, Key.B);
            Assert.Equal(ShowCurtain.Black, window.Screen);
            Press(window, Key.W);
            Assert.Equal(ShowCurtain.White, window.Screen);
            Press(window, Key.Space);
            Assert.Equal(ShowCurtain.Slide, window.Screen);
            Assert.Equal(1, window.SlideNumber);

            Press(window, Key.D0 + (target.Deck.IndexOf(hidden) + 1));
            Press(window, Key.Enter);
            Assert.Same(hidden, window.CurrentSlide);

            Press(window, Key.End);
            Press(window, Key.Right);
            Assert.Equal(ShowCurtain.End, window.Screen);
            Assert.False(closed);
            window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(closed);
        }
        finally
        {
            if (!closed) window.Close();
        }
    }, CancellationToken.None);

    private static void Press(Avalonia.Controls.Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();
    }
}
