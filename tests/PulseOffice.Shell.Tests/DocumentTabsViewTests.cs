using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class DocumentTabsViewTests
{
    [Fact]
    public void Il_pulsante_di_chiusura_di_una_scheda_ha_un_nome_accessibile_col_titolo()
    {
        var workspace = new DocumentWorkspace();
        workspace.Open(new OpenDocument(ModuleKind.Documents, "lettera", new Panel(), new PulseOffice.Document.Commands.UndoStack()));

        var view = new DocumentTabsView(workspace, new TextCatalog(UiLanguage.Italian));

        var close = Assert.Single(view.GetLogicalDescendants().OfType<Button>());
        Assert.Equal("Chiudi «lettera»", AutomationProperties.GetName(close));
    }

    [Fact]
    public void Switching_the_language_retranslates_the_tab_close_button()
    {
        var workspace = new DocumentWorkspace();
        workspace.Open(new OpenDocument(ModuleKind.Documents, "lettera", new Panel(), new PulseOffice.Document.Commands.UndoStack()));
        var texts = new TextCatalog(UiLanguage.Italian);
        var view = new DocumentTabsView(workspace, texts);

        texts.Use(UiLanguage.English);

        var close = Assert.Single(view.GetLogicalDescendants().OfType<Button>());
        Assert.Equal(texts.Format("document.closeNamed", "lettera"), AutomationProperties.GetName(close));
        Assert.NotEqual("Chiudi «lettera»", AutomationProperties.GetName(close));
    }
}
