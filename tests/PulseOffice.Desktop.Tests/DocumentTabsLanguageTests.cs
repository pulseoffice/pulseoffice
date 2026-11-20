using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Shell;
using PulseOffice.Testing;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class DocumentTabsLanguageTests(HeadlessDesktopSession headless)
{
    private UiThreadSession Session => headless.Session;

    [Fact]
    public Task An_attached_tab_strip_retranslates_its_close_button_and_a_closed_one_lets_go() => Session.Dispatch(() =>
    {
        var workspace = new DocumentWorkspace();
        workspace.Open(new OpenDocument(ModuleKind.Documents, "lettera", new Panel(), new PulseOffice.Document.Commands.UndoStack()));
        var texts = new TextCatalog(UiLanguage.Italian);
        var view = new DocumentTabsView(workspace, texts);
        var window = new Window { Width = 400, Height = 80, Content = view };
        window.Show();

        texts.Use(UiLanguage.English);
        var close = Assert.Single(view.GetLogicalDescendants().OfType<Button>());
        Assert.Equal(texts.Format("document.closeNamed", "lettera"), AutomationProperties.GetName(close));
        Assert.NotEqual("Chiudi «lettera»", AutomationProperties.GetName(close));

        window.Close();
        var english = AutomationProperties.GetName(Assert.Single(view.GetLogicalDescendants().OfType<Button>()));
        texts.Use(UiLanguage.Italian);
        Assert.Equal(english, AutomationProperties.GetName(Assert.Single(view.GetLogicalDescendants().OfType<Button>())));
    }, TestContext.Current.CancellationToken);
}
