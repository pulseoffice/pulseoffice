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
}
