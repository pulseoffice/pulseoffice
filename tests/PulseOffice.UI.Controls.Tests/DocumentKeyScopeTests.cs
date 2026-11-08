using PulseOffice.Testing;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class DocumentKeyScopeTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private sealed class Surface : Border, IDocumentSurface
    {
        public Surface() => Focusable = true;
    }

    private sealed record Scene(Window Window, Surface Surface, ComboBox Panel, List<(Key Key, bool Allowed, bool FromSurface)> Seen);

    private Scene Open()
    {
        var scope = new DocumentKeyScope();
        var surface = new Surface { Width = 100, Height = 100 };
        var panel = new ComboBox { ItemsSource = new[] { "Dissolvenza", "Spinta" } };
        var root = new StackPanel { Children = { surface, panel } };
        var window = new Window { Width = 300, Height = 300, Content = root };
        var seen = new List<(Key, bool, bool)>();
        window.AddHandler(InputElement.KeyDownEvent, (_, e) => seen.Add((e.Key, scope.Allows(e, window), scope.FromSurface(e.Source, surface))), Avalonia.Interactivity.RoutingStrategies.Tunnel);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new Scene(window, surface, panel, seen);
    }

    [Fact]
    public Task CancellareConIlFuocoSuUnPannelloNonRaggiungeIlDocumento() => Session.Dispatch(() =>
    {
        var scene = Open();
        scene.Panel.Focus();
        Dispatcher.UIThread.RunJobs();

        scene.Window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);
        scene.Window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);

        Assert.All(scene.Seen, entry => Assert.False(entry.Allowed || entry.FromSurface));
    }, CancellationToken.None);

    [Fact]
    public Task CancellareSullaSuperficieDelDocumentoArrivaAlDocumento() => Session.Dispatch(() =>
    {
        var scene = Open();
        scene.Surface.Focus();
        Dispatcher.UIThread.RunJobs();

        scene.Window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);

        var entry = Assert.Single(scene.Seen);
        Assert.True(entry.Allowed && entry.FromSurface);
    }, CancellationToken.None);

    [Fact]
    public Task LeScorciatoieConModificatoreValgonoOvunque() => Session.Dispatch(() =>
    {
        var scene = Open();
        scene.Panel.Focus();
        Dispatcher.UIThread.RunJobs();

        scene.Window.KeyPress(Key.S, RawInputModifiers.Control, PhysicalKey.S, null);

        Assert.True(Assert.Single(scene.Seen).Allowed);
    }, CancellationToken.None);
}
