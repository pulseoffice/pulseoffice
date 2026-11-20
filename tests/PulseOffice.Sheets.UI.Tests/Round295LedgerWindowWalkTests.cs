using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class Round295LedgerWindowWalkTests
{
    private sealed class Windows : IDocumentViewSource
    {
        public ModuleKind Module => ModuleKind.Sheets;

        public OpenDocument? CreateSecondView(OpenDocument document, string title) =>
            document.CreateWindow(new Avalonia.Controls.Panel(), title);
    }

    [Fact]
    public void New_windows_are_numbered_from_the_first_and_share_one_history()
    {
        var workspace = new DocumentWorkspace();
        var history = new UndoStack();
        var first = new OpenDocument(ModuleKind.Sheets, "Budget", new Avalonia.Controls.Panel(), history);
        workspace.Open(first);
        var windows = new DocumentWindows(workspace, [new Windows()]);

        windows.OpenAnother(workspace.Active!);
        windows.OpenAnother(workspace.Active!);
        workspace.Close(workspace.Documents.Single(document => document.Title == "Budget : 2"));
        windows.OpenAnother(workspace.Active!);

        Assert.Equal(["Budget", "Budget : 3", "Budget : 4"], workspace.Documents.Select(document => document.Title));
        Assert.All(workspace.Documents, document => Assert.Same(history, document.History));
    }
}
