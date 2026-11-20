using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class Round295PerDocumentCloseTests
{
    private sealed class ScriptedDialogs(params CloseDecision[] answers) : IDialogService
    {
        private int _next;

        public int Shown => _next;

        public bool IsDialogOpen => false;

        public Task<TResult?> ShowAsync<TResult>(Avalonia.Controls.Control content, DialogOptions options, CancellationToken ct = default) =>
            Task.FromResult((TResult?)(object)answers[_next++]);

        public Task ShowAsync(Avalonia.Controls.Control content, DialogOptions options, CancellationToken ct = default) => Task.CompletedTask;

        public void Close(object? result)
        {
        }
    }

    private sealed class Saver : IDocumentSaver
    {
        public List<string> Saved { get; } = [];

        public bool CanSave(OpenDocument document) => true;

        public Task<bool> SaveAsync(OpenDocument document, CancellationToken ct = default)
        {
            Saved.Add(document.Title);
            document.MarkSaved("/tmp/" + document.Title + ".ods", document.Title + ".ods");
            return Task.FromResult(true);
        }
    }

    private sealed class Edit : IUndoableAction
    {
        public string Description => "edit";

        public void Undo() { }

        public void Redo() { }

        public bool TryMergeWith(IUndoableAction previous) => false;
    }

    private static (DocumentWorkspace Workspace, Saver Saver) Three()
    {
        var workspace = new DocumentWorkspace();
        foreach (var title in new[] { "A", "B", "C" })
        {
            var history = new UndoStack();
            workspace.Open(new OpenDocument(ModuleKind.Sheets, title, new Avalonia.Controls.Panel(), history));
            history.Push(new Edit());
        }

        return (workspace, new Saver());
    }

    [Fact]
    public async Task Each_document_is_asked_in_turn()
    {
        var (workspace, saver) = Three();
        var dialogs = new ScriptedDialogs(CloseDecision.Save, CloseDecision.Discard, CloseDecision.Save);

        Assert.True(await new UnsavedChangesGuard(workspace, [saver], dialogs, new FakeTextCatalog()).ConfirmCloseAllAsync(TestContext.Current.CancellationToken));
        Assert.Equal(3, dialogs.Shown);
        Assert.Equal(["A", "C"], saver.Saved);
    }

    [Fact]
    public async Task Save_all_saves_the_rest_without_asking_again()
    {
        var (workspace, saver) = Three();
        var dialogs = new ScriptedDialogs(CloseDecision.Discard, CloseDecision.SaveAll);

        Assert.True(await new UnsavedChangesGuard(workspace, [saver], dialogs, new FakeTextCatalog()).ConfirmCloseAllAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, dialogs.Shown);
        Assert.Equal(["B", "C"], saver.Saved);
    }

    [Fact]
    public async Task Cancel_on_a_later_document_saves_nothing()
    {
        var (workspace, saver) = Three();
        var dialogs = new ScriptedDialogs(CloseDecision.Save, CloseDecision.Cancel);

        Assert.False(await new UnsavedChangesGuard(workspace, [saver], dialogs, new FakeTextCatalog()).ConfirmCloseAllAsync(TestContext.Current.CancellationToken));
        Assert.Empty(saver.Saved);
    }
}
