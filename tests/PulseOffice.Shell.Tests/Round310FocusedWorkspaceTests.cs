using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using Avalonia.Controls;
using PulseOffice.UI.Controls.References;
using PulseOffice.UI.Controls.Services;
using Xunit;
using PulseOffice.UI.Commands;

namespace PulseOffice.Shell.Tests;

public sealed class Round310FocusedWorkspaceTests
{
    private static OpenDocument Tab(string title) =>
        new(ModuleKind.Sheets, title, new Avalonia.Controls.Panel(), new UndoStack());

    private static ShellWindowScope Scope()
    {
        var dialogs = new NoDialogs();
        return new ShellWindowScope(new DocumentWorkspace(), dialogs, dialogs);
    }

    private sealed class NoDialogs : IDialogService, IDialogCollapse
    {
        public bool IsDialogOpen => false;
        public bool IsCollapsed => false;
        public string? TopDialogTitle => null;

        public event EventHandler? CollapsedChanged
        {
            add { }
            remove { }
        }

        public Task<TResult?> ShowAsync<TResult>(Control content, DialogOptions options, CancellationToken ct = default) =>
            Task.FromResult<TResult?>(default);

        public Task ShowAsync(Control content, DialogOptions options, CancellationToken ct = default) => Task.CompletedTask;

        public void Close(object? result)
        {
        }

        public IDisposable Collapse(Control bar, Action interrupted) => throw new NotSupportedException();
    }

    private readonly ShellWindowScope _first = Scope();
    private readonly ShellWindowScope _second = Scope();
    private readonly ShellWindowFocus _focus;
    private readonly FocusedDocumentWorkspace _workspace;

    public Round310FocusedWorkspaceTests()
    {
        _focus = new ShellWindowFocus(_first);
        _workspace = new FocusedDocumentWorkspace(_focus);
        _focus.Enter(_second);
    }

    [Fact]
    public void Commands_see_the_focused_window_and_quit_sees_every_window()
    {
        var a = Tab("a");
        var b = Tab("b");
        _workspace.Open(a);
        _focus.Focus(_second);
        _workspace.Open(b);

        Assert.Same(b, _workspace.Active);
        Assert.Equal(new[] { b }, _second.Workspace.Documents);
        Assert.Equal(2, _workspace.Documents.Count);

        var activated = new List<OpenDocument>();
        _workspace.ActiveChanged += (_, document) => activated.Add(document);
        _focus.Focus(_first);
        Assert.Same(a, _workspace.Active);
        Assert.Equal(new[] { a }, activated);

        _workspace.Close(b);
        Assert.Empty(_second.Workspace.Documents);
    }

    [Fact]
    public void A_document_moved_between_windows_is_reported_as_moved_not_opened()
    {
        var a = Tab("a");
        _workspace.Open(a);
        var opened = 0;
        var moved = 0;
        _workspace.DocumentOpened += (_, _) => opened++;
        _workspace.DocumentMoved += (_, _) => moved++;

        Assert.True(_first.Workspace.Detach(a));
        _second.Workspace.Open(a);

        Assert.Equal((0, 1), (opened, moved));
    }

    [Fact]
    public void Events_of_an_unfocused_window_do_not_reach_the_context_and_a_left_window_is_forgotten()
    {
        var changes = 0;
        _workspace.ContextChanged += (_, _) => changes++;
        _second.Workspace.Open(Tab("b"));
        Assert.Equal(0, changes);

        _focus.Focus(_second);
        _focus.Leave(_second);

        Assert.Same(_first, _focus.Current);
        Assert.Single(_focus.Scopes);
        Assert.Empty(_workspace.Documents);
    }

    [Fact]
    public void A_view_in_an_unfocused_window_updates_its_own_window_context()
    {
        var inner = new Avalonia.Controls.Border();
        var view = new Avalonia.Controls.Panel { Children = { inner } };
        var background = new OpenDocument(ModuleKind.Sheets, "b", view, new UndoStack());
        _second.Workspace.Open(background);
        _first.Workspace.Open(Tab("a"));
        var before = _first.Workspace.Context;

        _workspace.UpdateContext(inner, context => context with { Selection = SelectionKind.CellRange });

        Assert.Equal(before, _first.Workspace.Context);
        Assert.Equal(SelectionKind.CellRange, _second.Workspace.Context.Selection);
    }
}
