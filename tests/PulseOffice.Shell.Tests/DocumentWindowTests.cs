using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class DocumentWindowTests
{
    private sealed class Edit : IUndoableAction
    {
        public string Description => "edit";

        public void Undo() { }

        public void Redo() { }

        public bool TryMergeWith(IUndoableAction previous) => false;
    }

    private static OpenDocument Saved(out UndoStack history)
    {
        history = new UndoStack();
        var document = new OpenDocument(ModuleKind.Sheets, "Budget", new Avalonia.Controls.Panel(), history);
        document.MarkSaved("/tmp/Budget.ods", "Budget.ods");
        return document;
    }

    [Fact]
    public void A_second_window_of_a_modified_document_is_modified_too()
    {
        var first = Saved(out var history);
        history.Push(new Edit());

        var second = first.CreateWindow(new Avalonia.Controls.Panel(), "Budget : 2");

        Assert.True(second.IsModified);
    }

    [Fact]
    public void A_second_window_keeps_the_path_and_the_read_only_state()
    {
        var first = Saved(out _);
        first.IsReadOnly = true;

        var second = first.CreateWindow(new Avalonia.Controls.Panel(), "Budget : 2");

        Assert.Equal(first.Path, second.Path);
        Assert.True(second.IsReadOnly);
    }

    [Fact]
    public void Saving_from_one_window_marks_the_other_saved()
    {
        var first = Saved(out var history);
        var second = first.CreateWindow(new Avalonia.Controls.Panel(), "Budget : 2");
        history.Push(new Edit());
        var notified = false;
        first.Changed += (_, _) => notified = true;

        second.MarkSaved("/tmp/Other.ods", "Other.ods");

        Assert.False(first.IsModified);
        Assert.Equal("/tmp/Other.ods", first.Path);
        Assert.True(notified);
    }

    [Fact]
    public void Making_one_window_read_only_makes_every_window_read_only()
    {
        var first = Saved(out _);
        var second = first.CreateWindow(new Avalonia.Controls.Panel(), "Budget : 2");

        first.IsReadOnly = true;

        Assert.True(second.IsReadOnly);
    }
}
