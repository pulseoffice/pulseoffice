using PulseOffice.Testing;
using Avalonia.Automation;
using Avalonia.Headless;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class SteppedDialogTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    [Fact]
    public Task Il_dialogo_a_passi_inoltra_risultato_piede_e_larghezza() => Session.Dispatch(() =>
    {
        var texts = new TextCatalog(UiLanguage.Italian);
        var form = new FormPromptView("Spiegazione", [new TextField("a", "A", "uno")], texts);
        var strip = new StepStrip(["Sorgente", "Tabella"], 1, DesignTokens.For(InterfaceDensity.Compact), "Passo {0} di {1}: {2}");
        var stepped = new SteppedDialog(strip, form);
        object? result = null;
        stepped.Completed += (_, value) => result = value;

        Assert.Same(form.Footer, stepped.Footer);
        Assert.Equal(form.PreferredWidth, stepped.PreferredWidth);
        Assert.Equal("Passo 2 di 2: Tabella", AutomationProperties.GetName(strip));

        ((Avalonia.Controls.Button)Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants((Avalonia.Controls.Control)form.Footer!).OfType<Avalonia.Controls.Button>().Last())
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.NotNull(result);
        return Task.CompletedTask;
    }, CancellationToken.None);
}
