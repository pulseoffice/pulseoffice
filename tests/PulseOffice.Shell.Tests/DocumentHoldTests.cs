using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class DocumentHoldTests
{
    private static OpenDocument Document(string title, UndoStack? history = null) =>
        new(ModuleKind.Sheets, title, new Avalonia.Controls.Panel(), history ?? new UndoStack());

    [Fact]
    public void Switching_tab_while_a_document_is_held_waits_for_the_release()
    {
        var workspace = new DocumentWorkspace();
        var first = Document("A");
        var second = Document("B");
        workspace.Open(first);
        workspace.Open(second);

        var hold = workspace.Hold(first);
        workspace.Activate(second);
        Assert.Same(first, workspace.Active);

        hold.Dispose();
        Assert.Same(second, workspace.Active);
    }

    [Fact]
    public void Closing_the_held_document_happens_after_the_release()
    {
        var workspace = new DocumentWorkspace();
        var first = Document("A");
        var second = Document("B");
        workspace.Open(first);
        workspace.Open(second);

        using (workspace.Hold(first))
        {
            workspace.Close(first);
            Assert.Contains(first, workspace.Documents);
            Assert.Same(first, workspace.Active);
        }

        Assert.DoesNotContain(first, workspace.Documents);
        Assert.Same(second, workspace.Active);
    }

    private sealed class CountingDialogs : PulseOffice.UI.Controls.Services.IDialogService
    {
        public int Shown { get; private set; }

        public bool IsDialogOpen => false;

        public Task<TResult?> ShowAsync<TResult>(Avalonia.Controls.Control content, PulseOffice.UI.Controls.Services.DialogOptions options, CancellationToken ct = default)
        {
            Shown++;
            return Task.FromResult(default(TResult));
        }

        public Task ShowAsync(Avalonia.Controls.Control content, PulseOffice.UI.Controls.Services.DialogOptions options, CancellationToken ct = default) => Task.CompletedTask;

        public void Close(object? result)
        {
        }
    }

    private sealed class Edit : IUndoableAction
    {
        public string Description => "edit";

        public void Undo() { }

        public void Redo() { }

        public bool TryMergeWith(IUndoableAction previous) => false;
    }

    [Fact]
    public async Task Closing_one_of_two_windows_of_the_same_document_does_not_ask_to_save()
    {
        var workspace = new DocumentWorkspace();
        var history = new UndoStack();
        var first = Document("A", history);
        var second = Document("A : 2", history);
        workspace.Open(first);
        workspace.Open(second);
        history.Push(new Edit());
        var dialogs = new CountingDialogs();
        var guard = new UnsavedChangesGuard(workspace, [], dialogs, new FakeTextCatalog());

        Assert.True(await guard.ConfirmCloseAsync([second], TestContext.Current.CancellationToken));
        Assert.Equal(0, dialogs.Shown);

        Assert.False(await guard.ConfirmCloseAsync([first, second], TestContext.Current.CancellationToken));
        Assert.Equal(1, dialogs.Shown);
    }

    [Fact]
    public void A_document_opened_during_a_hold_becomes_active_after_it()
    {
        var workspace = new DocumentWorkspace();
        var first = Document("A");
        workspace.Open(first);
        var opened = Document("B");

        using (workspace.Hold(first))
        {
            workspace.Open(opened);
            Assert.Same(first, workspace.Active);
        }

        Assert.Same(opened, workspace.Active);
    }
}
