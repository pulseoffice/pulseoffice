using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseOffice.Platform;
using PulseOffice.UI.Controls;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class CaptionButtonsTests(HeadlessDesktopSession headless)
{
    private sealed class DrawnChrome(bool draws) : IWindowChrome
    {
        public MenuPlacement MenuPlacement => MenuPlacement.InTitleBar;

        public double LeadingInset => 8;

        public double TrailingInset => 0;

        public bool ShowsTitleText => true;

        public bool DrawsCaptionButtons => draws;
    }

    [Fact]
    public Task Su_Windows_e_Linux_la_barra_del_titolo_ha_tre_pulsanti_di_sistema_senza_schermo_intero() => headless.Session.Dispatch(() =>
    {
        var texts = new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.Italian);
        var bar = new TitleBar(new DrawnChrome(true), texts);
        var window = new Window { Width = 900, Height = 120, Content = new StackPanel { Children = { bar } } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var buttons = bar.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("caption")).ToList();
        Assert.Equal(
            [WindowDecorationsElementRole.MinimizeButton, WindowDecorationsElementRole.MaximizeButton, WindowDecorationsElementRole.CloseButton],
            buttons.Select(WindowDecorationProperties.GetElementRole));
        Assert.All(buttons, button => Assert.Equal(46, button.Bounds.Width, 1));
        Assert.All(buttons, button => Assert.Equal(default, button.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>().First().BorderThickness));
        Assert.All(buttons, button => Assert.Equal(bar.Bounds.Height, button.Bounds.Height, 1));
        Assert.Equal(window.Bounds.Width, buttons[^1].TranslatePoint(new Point(46, 0), window)!.Value.X, 1);
        Assert.Equal(["Riduci a icona", "Ingrandisci", "Chiudi"], buttons.Select(Avalonia.Automation.AutomationProperties.GetName));

        buttons[1].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(WindowState.Maximized, window.WindowState);
        Assert.Equal("Ripristina", Avalonia.Automation.AutomationProperties.GetName(buttons[1]));
        buttons[1].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(WindowState.Normal, window.WindowState);
        buttons[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(WindowState.Minimized, window.WindowState);
        var closed = false;
        window.Closed += (_, _) => closed = true;
        buttons[2].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.True(closed);

    }, CancellationToken.None);

    [Fact]
    public Task Su_macOS_restano_i_semafori_di_sistema() => headless.Session.Dispatch(() =>
    {
        var bar = new TitleBar(new DrawnChrome(false), new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.Italian));
        Assert.Empty(bar.GetLogicalDescendantsOfCaptions());
    }, CancellationToken.None);

    [Fact]
    public Task Le_decorazioni_tengono_bordo_e_ombra_e_tolgono_titolo_e_pulsanti_di_Avalonia() => headless.Session.Dispatch(() =>
    {
        Assert.True(Application.Current!.TryGetResource(typeof(WindowDrawnDecorations), null, out var theme));
        var standard = (IWindowDrawnDecorationsTemplate)((Avalonia.Styling.ControlTheme)theme!).Setters
            .OfType<Avalonia.Styling.Setter>().Single(setter => setter.Property == WindowDrawnDecorations.TemplateProperty).Value!;

        var content = new BorderOnlyDecorationsTemplate(standard).Build().Result;

        Assert.Null(content.Overlay);
        Assert.Null(content.FullscreenPopover);
        Assert.NotNull(content.Underlay);
    }, CancellationToken.None);
}

internal static class TitleBarCaptionLookup
{
    public static IEnumerable<CaptionButtons> GetLogicalDescendantsOfCaptions(this TitleBar bar) =>
        Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(bar).OfType<CaptionButtons>();
}
