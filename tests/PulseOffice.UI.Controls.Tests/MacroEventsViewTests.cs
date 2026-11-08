using PulseOffice.Testing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Document.Forms;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Macros;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class MacroEventsViewTests
{
    public sealed class TestApp : Application
    {
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
            Styles.Add(new PulseTheme());
            Palette.Apply(this, dark: false);
            Resources["PulseAccent"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#16A34A"));
        }
    }

    private sealed class RecordingEditor : IMacroEditorGateway
    {
        public List<string?> Opened { get; } = [];

        public Task OpenAsync(string? macro, CancellationToken ct)
        {
            Opened.Add(macro);
            return Task.CompletedTask;
        }
    }

    internal static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(TestApp));

    private sealed record Scene(Window Window, DialogHost Host, RecordingEditor Editor, MacroLibrary Library, Task<IReadOnlyDictionary<string, string?>?> Result);

    private static Scene Open(MacroLibrary library)
    {
        var texts = new TextCatalog(UiLanguage.English);
        var host = new DialogHost();
        host.Use(new IconCatalog(), texts);
        var window = new Window { Width = 1280, Height = 900, Content = host };
        window.Show();

        var previous = host.ShowAsync(new TextBlock { Text = "palette" }, new DialogOptions { TitleKey = "palette" });
        Pump(window);
        host.Close(null);
        Pump(window);

        var editor = new RecordingEditor();
        var dialog = new MacroEventsDialog(host, new MacroBindingRules(), new IconCatalog(), texts, NullLogger<MacroEventsDialog>.Instance);

        var result = dialog.ShowAsync(
            new MacroEventsRequest(
                [new MacroEventSlot("Activated", "sheet.event.activated", "sheet.event.activated.description", "Sheet1_Activated")],
                new Dictionary<string, string?>(),
                new MacroEventsContext(library, editor),
                "command.ledger.sheet.events.title"),
            CancellationToken.None);

        Pump(window);
        return new Scene(window, host, editor, library, result);
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static Button Find(Window window, string label) =>
        window.GetVisualDescendants().OfType<Button>()
            .First(button => button.IsEffectivelyVisible && button.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label)
                || (button.Content as string) == label);

    private static void Click(Window window, Control target)
    {
        var centre = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseMove(centre);
        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        Pump(window);
    }

    [Fact]
    public Task IlClicSuCreaUnaMacroApreLEditor() => Session.Dispatch(() =>
    {
        var scene = Open(new MacroLibrary());

        Click(scene.Window, Find(scene.Window, "Create a macro"));

        Assert.Single(scene.Editor.Opened);
    }, CancellationToken.None);

    [Fact]
    public Task IlClicSuNuovaCreaEAssegnaLaMacro() => Session.Dispatch(() =>
    {
        var library = new MacroLibrary();
        library.Store(new DocumentMacro("Existing", MacroLanguage.JavaScript, "app.Log(1);"));
        var scene = Open(library);

        Click(scene.Window, Find(scene.Window, "New…"));

        Assert.Equal(["Sheet1_Activated"], scene.Editor.Opened);
        Assert.NotNull(library.Find("Sheet1_Activated"));
    }, CancellationToken.None);

    [Fact]
    public Task IlClicSuAnnullaChiudeLaFinestra() => Session.Dispatch(() =>
    {
        var scene = Open(new MacroLibrary());

        Click(scene.Window, Find(scene.Window, "Cancel"));

        Assert.True(scene.Result.IsCompleted);
        Assert.False(scene.Host.IsDialogOpen);
    }, CancellationToken.None);

    [Fact]
    public Task LaTendinaSiApreEPortaDatiNonControlli() => Session.Dispatch(() =>
    {
        var library = new MacroLibrary();
        library.Store(new DocumentMacro("Existing", MacroLanguage.JavaScript, "app.Log(1);"));
        var scene = Open(library);
        var combo = scene.Window.GetVisualDescendants().OfType<ComboBox>().Single();

        Assert.Empty(combo.Items.OfType<Control>());

        Click(scene.Window, combo);

        Assert.True(combo.IsDropDownOpen);
    }, CancellationToken.None);

    [Fact]
    public Task ApplicaESpentoFincheNonCambiaNulla() => Session.Dispatch(() =>
    {
        var scene = Open(new MacroLibrary());

        Assert.False(Find(scene.Window, "Apply").IsEffectivelyEnabled);
    }, CancellationToken.None);
}
