using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class LedgerWindowCloseTests
{
    private sealed class Edit : IUndoableAction
    {
        public string Description => "edit";

        public void Undo() { }

        public void Redo() { }

        public bool TryMergeWith(IUndoableAction previous) => false;
    }

    [Fact]
    public void Closing_the_window_of_a_modified_workbook_with_no_other_window_does_not_discard_it()
    {
        var workspace = new DocumentWorkspace();
        var other = new OpenDocument(ModuleKind.Sheets, "Other", new Avalonia.Controls.Panel(), new UndoStack());
        var history = new UndoStack();
        var modified = new OpenDocument(ModuleKind.Sheets, "Budget", new Avalonia.Controls.Panel(), history);
        workspace.Open(other);
        workspace.Open(modified);
        history.Push(new Edit());
        var windows = new DocumentWindows(workspace, []);

        Assert.False(windows.HasAnotherWindow(modified));
        Assert.False(windows.CloseWindow(modified));
        Assert.Contains(modified, workspace.Documents);
    }

    [Fact]
    public void Closing_a_second_window_keeps_the_workbook_open_in_the_first()
    {
        var workspace = new DocumentWorkspace();
        var history = new UndoStack();
        var first = new OpenDocument(ModuleKind.Sheets, "Budget", new Avalonia.Controls.Panel(), history);
        var second = new OpenDocument(ModuleKind.Sheets, "Budget : 2", new Avalonia.Controls.Panel(), history);
        workspace.Open(first);
        workspace.Open(second);
        history.Push(new Edit());
        var windows = new DocumentWindows(workspace, []);

        Assert.True(windows.CloseWindow(second));
        Assert.DoesNotContain(second, workspace.Documents);
        Assert.Contains(first, workspace.Documents);
    }
}
