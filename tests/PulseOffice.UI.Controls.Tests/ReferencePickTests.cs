using PulseOffice.Testing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.References;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class ReferencePickTests
{
    private sealed class FakeTarget : IReferencePickTarget
    {
        public IReferencePickListener? Listener { get; private set; }

        public string? Started { get; private set; }

        public bool Ended { get; private set; }

        public IDisposable Begin(string current, IReferencePickListener listener)
        {
            Started = current;
            Listener = listener;
            return new End(() => Ended = true);
        }

        private sealed class End(Action end) : IDisposable
        {
            public void Dispose() => end();
        }
    }

    private sealed class Source(IReferencePicker picker) : IReferenceSource
    {
        public bool IsValid(string text) => text.StartsWith('A') || text.StartsWith('B');

        public IReferencePicker? Picker { get; } = picker;
    }

    private sealed record Scene(Window Window, DialogHost Host, FakeTarget Target, IReferencePicker Picker, FormPromptView Form, Task<Dictionary<string, object>?> Result);

    private static Scene Open()
    {
        var texts = new TextCatalog(UiLanguage.English);
        var icons = new IconCatalog();
        var host = new DialogHost();
        host.Use(icons, texts);
        var window = new Window { Width = 1280, Height = 900, Content = host };
        window.Show();

        var target = new FakeTarget();
        var picker = new ReferencePickers(host, texts, icons).For(target);
        var form = new FormPromptView(string.Empty, [new RangeField("area", "Area", "A1", new Source(picker))], texts);
        var result = host.ShowAsync<Dictionary<string, object>>(form, new DialogOptions { TitleKey = "command.ledger.data.consolidate.title" });

        Pump(window);
        return new Scene(window, host, target, picker, form, result);
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Window window, Control target)
    {
        var centre = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseMove(centre);
        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        Pump(window);
    }

    private static ReferencePickBar? Bar(Window window) =>
        window.GetVisualDescendants().OfType<ReferencePickBar>().FirstOrDefault();

    [Fact]
    public Task SenzaUnaFinestraApertaLaSceltaNonParteENonSolleva() => Session.Dispatch(() =>
    {
        var texts = new TextCatalog(UiLanguage.English);
        var icons = new IconCatalog();
        var host = new DialogHost();
        host.Use(icons, texts);
        var window = new Window { Width = 800, Height = 600, Content = host };
        window.Show();
        var target = new FakeTarget();
        var picker = new ReferencePickers(host, texts, icons).For(target);

        var pick = picker.PickAsync(new ReferencePickRequest("Area", "A1", _ => true));
        Pump(window);

        Assert.True(pick.IsCompletedSuccessfully);
        Assert.Null(pick.Result);
        Assert.Null(target.Started);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task IlPulsanteRitiraLaFinestraEMostraLaBarra() => Session.Dispatch(() =>
    {
        var scene = Open();

        Click(scene.Window, scene.Window.GetVisualDescendants().OfType<ReferencePickButton>().Single());

        Assert.True(scene.Host.IsCollapsed);
        Assert.True(scene.Host.IsDialogOpen);
        Assert.False(scene.Form.IsEffectivelyVisible);
        Assert.NotNull(Bar(scene.Window));
        Assert.Equal("A1", scene.Target.Started);
    }, CancellationToken.None);

    [Fact]
    public Task LaSelezioneAggiornaLaBarraEConfermandoTornaNelCampo() => Session.Dispatch(() =>
    {
        var scene = Open();

        Click(scene.Window, scene.Window.GetVisualDescendants().OfType<ReferencePickButton>().Single());
        scene.Target.Listener!.SelectionChanged("B2:C4");
        Pump(scene.Window);

        Assert.Equal("B2:C4", Bar(scene.Window)!.Text);

        scene.Target.Listener.Commit();
        Pump(scene.Window);

        Assert.False(scene.Host.IsCollapsed);
        Assert.True(scene.Target.Ended);
        Assert.Null(Bar(scene.Window));
        Assert.True(scene.Form.IsEffectivelyVisible);
        Assert.Equal("B2:C4", scene.Form.Values["area"]);
    }, CancellationToken.None);

    [Fact]
    public Task AnnullareLasciaIlCampoComEra() => Session.Dispatch(() =>
    {
        var scene = Open();

        Click(scene.Window, scene.Window.GetVisualDescendants().OfType<ReferencePickButton>().Single());
        scene.Target.Listener!.SelectionChanged("B9");
        scene.Target.Listener.Cancel();
        Pump(scene.Window);

        Assert.False(scene.Host.IsCollapsed);
        Assert.Equal("A1", scene.Form.Values["area"]);
    }, CancellationToken.None);

    [Fact]
    public Task EscDuranteLaSceltaNonChiudeLaFinestraRitirata() => Session.Dispatch(() =>
    {
        var scene = Open();

        Click(scene.Window, scene.Window.GetVisualDescendants().OfType<ReferencePickButton>().Single());
        scene.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Pump(scene.Window);

        Assert.True(scene.Host.IsDialogOpen);
        Assert.False(scene.Result.IsCompleted);
    }, CancellationToken.None);

    [Fact]
    public Task UnRiferimentoNonValidoNonSiConferma() => Session.Dispatch(() =>
    {
        var scene = Open();

        Click(scene.Window, scene.Window.GetVisualDescendants().OfType<ReferencePickButton>().Single());
        scene.Target.Listener!.SelectionChanged("Z9");
        scene.Target.Listener.Commit();
        Pump(scene.Window);

        Assert.True(scene.Host.IsCollapsed);
        scene.Target.Listener.Cancel();
        Pump(scene.Window);
    }, CancellationToken.None);

    [Fact]
    public Task ChiudereLaFinestraDuranteLaSceltaTerminaLaScelta() => Session.Dispatch(() =>
    {
        var scene = Open();
        var pick = scene.Picker.PickAsync(new ReferencePickRequest("Area", "A1", _ => true));
        Pump(scene.Window);

        scene.Host.Close(null);
        Pump(scene.Window);

        Assert.True(pick.IsCompleted);
        Assert.Null(pick.Result);
        Assert.False(scene.Host.IsCollapsed);
    }, CancellationToken.None);

    private static UiThreadSession Session => MacroEventsViewTests.Session;
}
