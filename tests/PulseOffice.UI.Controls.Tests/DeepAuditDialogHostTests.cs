using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class DeepAuditDialogHostTests
{
    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task IlDialogoChiusoNonSeguePiuLeDimensioniDellOspite() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var host = new DialogHost();
        host.Use(new IconCatalog(), new TextCatalog(UiLanguage.English));
        var window = new Window { Width = 1000, Height = 700, Content = host };
        window.Show();

        var content = new TextBlock { Text = "x" };
        var result = host.ShowAsync<object>(content, new DialogOptions { TitleKey = "command.ledger.data.consolidate.title", PreferredWidth = 900 });
        Pump(window);
        var frame = content.GetVisualAncestors().OfType<Border>().First(border => border.ClipToBounds && border.BoxShadow.Count > 0);
        var opened = frame.Width;

        host.Close(null);
        Pump(window);
        Assert.True(result.IsCompleted);
        window.Width = 500;
        Pump(window);

        Assert.Equal(500, host.Bounds.Width);
        Assert.Equal(900, opened);
        Assert.Equal(opened, frame.Width);
        window.Close();
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task IlDialogoSottostanteTornaInCimaNellaLinguaNuova() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var texts = new TextCatalog(UiLanguage.English);
        var host = new DialogHost();
        host.Use(new IconCatalog(), texts);
        var window = new Window { Width = 1000, Height = 700, Content = host };
        window.Show();

        var lower = new TextBlock { Text = "a" };
        _ = host.ShowAsync<object>(lower, new DialogOptions { TitleKey = "prompt.cancel" });
        Pump(window);
        _ = host.ShowAsync<object>(new TextBlock { Text = "b" }, new DialogOptions { TitleKey = "prompt.confirm" });
        Pump(window);

        texts.Use(UiLanguage.Italian);
        host.Close(null);
        Pump(window);

        var shown = host.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible)
            .Select(block => block.Text).ToList();
        Assert.Contains("Annulla", shown);
        Assert.Equal("Annulla", host.TopDialogTitle);
        window.Close();
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task IlPulsanteDiChiusuraHaUnNome() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var host = new DialogHost();
        host.Use(new IconCatalog(), new TextCatalog(UiLanguage.English));
        var window = new Window { Width = 1000, Height = 700, Content = host };
        window.Show();

        var content = new TextBlock { Text = "x" };
        _ = host.ShowAsync<object>(content, new DialogOptions { TitleKey = "prompt.cancel" });
        Pump(window);

        var close = host.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("dialogClose"));
        Assert.Equal("Close", Avalonia.Automation.AutomationProperties.GetName(close));
        window.Close();
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task DueSchedeConLaStessaChiaveSiConfermanoSenzaEccezione() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var texts = new TextCatalog(UiLanguage.English);
        var forms = new TabbedForms(new DialogHost(), texts);
        var (dialog, _) = forms.Build(
        [
            new FormTab("A", string.Empty, [new TextField("name", "Name", "first")]),
            new FormTab("B", string.Empty, [new TextField("name", "Name", "second")]),
        ]);
        object? collected = null;
        dialog.Completed += (_, result) => collected = result;

        var confirm = ((DialogFooter)dialog.Footer!).GetLogicalDescendants().OfType<Button>().Single(button => button.IsDefault);
        confirm.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        var values = Assert.IsType<Dictionary<string, object>>(collected);
        Assert.Equal("second", values["name"]);
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task LaFinestraPortaTitoloEDescrizioneAccessibili() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var host = new DialogHost();
        host.Use(new IconCatalog(), new TextCatalog(UiLanguage.English));
        var window = new Window { Width = 1000, Height = 700, Content = host };
        window.Show();

        var content = new TextBlock { Text = "x" };
        _ = host.ShowAsync<object>(content, new DialogOptions { TitleKey = "prompt.cancel", AccessibleDescription = "about" });
        Pump(window);

        var frame = content.GetVisualAncestors().OfType<Border>().First(border => border.ClipToBounds && border.BoxShadow.Count > 0);
        Assert.Equal("Cancel", Avalonia.Automation.AutomationProperties.GetName(frame));
        Assert.Equal("about", Avalonia.Automation.AutomationProperties.GetHelpText(frame));
        window.Close();
        return Task.CompletedTask;
    }, CancellationToken.None);
}
