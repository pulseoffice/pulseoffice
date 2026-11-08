using Avalonia.Controls;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.UI.Controls.Prompts;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class DocumentDialogsTests
{
    private static readonly TextCatalog Texts = new(UiLanguage.Italian);

    private static Window Show(Control content)
    {
        var window = new Window { Content = content, Width = 700, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [Fact]
    public Task Le_versioni_sono_un_elenco_dalla_piu_recente_e_le_azioni_puntano_alla_versione_giusta() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var view = new VersionsView(
        [
            new VersionRow("Versione 1", DateTimeOffset.Now.AddDays(-2), "Anna", "Bozza"),
            new VersionRow("Versione 2", DateTimeOffset.Now, "Luca", "Revisione"),
        ], Texts);
        var window = Show(view);
        object? result = null;
        view.Completed += (_, value) => result = value;

        Assert.Equal(0, view.List.SelectedIndex);
        view.Act(VersionActionKind.Open);
        Assert.Equal(new VersionAction(VersionActionKind.Open, 1, string.Empty), result);

        view.List.SelectedIndex = 1;
        view.Act(VersionActionKind.Delete);
        Assert.Equal(new VersionAction(VersionActionKind.Delete, 0, string.Empty), result);

        view.Comment = "Finale";
        view.Act(VersionActionKind.Save);
        Assert.Equal(new VersionAction(VersionActionKind.Save, -1, "Finale"), result);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Le_firme_mostrano_lo_stato_e_la_firma_usa_il_certificato_scelto() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var loads = 0;
        var view = new SignaturesView(
            [new SignatureRow("Anna Rossi", "CA", DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1), SignatureHealth.Broken, "il documento è cambiato dopo la firma")],
            () => { loads++; return ["Anna Rossi", "Luca Bianchi"]; }, () => Task.FromResult<string?>("/tmp/firma.pfx"), Texts);
        var window = Show(view);
        object? result = null;
        view.Completed += (_, value) => result = value;

        Assert.Equal(0, loads);
        Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(view).OfType<Avalonia.Controls.ComboBox>().Single().IsDropDownOpen = true;
        Assert.Equal(1, loads);

        view.Sign();
        Assert.Equal(new SignatureRequest(SignatureRequestKind.Sign, 0, string.Empty, string.Empty), result);

        Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(view).OfType<Avalonia.Controls.RadioButton>().Last().IsChecked = true;
        Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(view).OfType<Avalonia.Controls.TextBox>().First().Text = "/tmp/firma.pfx";
        view.Sign();
        Assert.Equal(new SignatureRequest(SignatureRequestKind.Sign, -1, "/tmp/firma.pfx", string.Empty), result);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Le_proprieta_personalizzate_si_modificano_come_righe() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var field = new CustomPropertiesField("custom", "Personalizzate", [new CustomPropertyItem("Cliente", "ACME", 0), new CustomPropertyItem(" ", "x", 1)], ["Testo", "Numero"]);
        var editor = field.Create(Texts, PulseOffice.UI.Theming.DesignTokens.For(PulseOffice.UI.Theming.InterfaceDensity.Compact));
        var window = Show(editor.Editor);

        var rows = Assert.IsAssignableFrom<IEnumerable<CustomPropertyItem>>(editor.Read());
        Assert.Equal([new CustomPropertyItem("Cliente", "ACME", 0)], rows);
        Assert.Equal("1 KB", new InfoField("size", "Dimensione", "1 KB").Create(Texts, PulseOffice.UI.Theming.DesignTokens.For(PulseOffice.UI.Theming.InterfaceDensity.Compact)).Read());
        window.Close();
    }, CancellationToken.None);

}
