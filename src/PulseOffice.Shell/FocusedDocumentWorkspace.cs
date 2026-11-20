using PulseOffice.UI.Commands;

namespace PulseOffice.Shell;

public sealed class FocusedDocumentWorkspace : IDocumentWorkspace
{
    private readonly IShellWindowFocus _focus;
    private readonly HashSet<IDocumentWorkspace> _listened = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<OpenDocument> _inTransit = new(ReferenceEqualityComparer.Instance);

    public FocusedDocumentWorkspace(IShellWindowFocus focus)
    {
        _focus = focus ?? throw new ArgumentNullException(nameof(focus));
        foreach (var scope in focus.Scopes) Listen(scope.Workspace);
        focus.Entered += (_, scope) => Listen(scope.Workspace);
        focus.Left += (_, scope) => Forget(scope.Workspace);
        focus.FocusChanged += (_, _) => Refocus();
    }

    private IDocumentWorkspace Current => _focus.Current.Workspace;

    public IReadOnlyList<OpenDocument> Documents =>
        _focus.Scopes.Count == 1
            ? Current.Documents
            : _focus.Scopes.SelectMany(scope => scope.Workspace.Documents).ToList();

    public OpenDocument? Active => Current.Active;

    public CommandContext Context => Current.Context;

    public event EventHandler? ContextChanged;
    public event EventHandler<OpenDocument>? ActiveChanged;
    public event EventHandler<OpenDocument>? ActiveUpdated;
    public event EventHandler<OpenDocument>? DocumentOpened;
    public event EventHandler<OpenDocument>? DocumentClosed;
    public event EventHandler<OpenDocument>? DocumentMoved;
    public event EventHandler<OpenDocument>? DocumentDetached;

    public IDocumentWorkspace? OwnerOf(OpenDocument document) =>
        _focus.Scopes.Select(scope => scope.Workspace).FirstOrDefault(workspace => workspace.Documents.Contains(document));

    public void Open(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        (OwnerOf(document) ?? Current).Open(document);
    }

    public void Close(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        OwnerOf(document)?.Close(document);
    }

    public void Activate(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        OwnerOf(document)?.Activate(document);
    }

    public void Move(OpenDocument document, int index)
    {
        ArgumentNullException.ThrowIfNull(document);
        OwnerOf(document)?.Move(document, index);
    }

    public bool Detach(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return OwnerOf(document)?.Detach(document) ?? false;
    }

    public IDisposable Hold(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return (OwnerOf(document) ?? throw new InvalidOperationException("Only an open document can be held.")).Hold(document);
    }

    public void UpdateContext(Func<CommandContext, CommandContext> update) => Current.UpdateContext(update);

    public void UpdateContext(Avalonia.Controls.Control source, Func<CommandContext, CommandContext> update)
    {
        ArgumentNullException.ThrowIfNull(source);
        (WorkspaceShowing(source) ?? Current).UpdateContext(update);
    }

    private IDocumentWorkspace? WorkspaceShowing(Avalonia.Controls.Control source)
    {
        var lineage = Avalonia.VisualTree.VisualExtensions.GetSelfAndVisualAncestors(source).ToList();
        bool Shows(OpenDocument document) => ReferenceEquals(document.View, source) || lineage.Contains(document.View);
        var workspaces = _focus.Scopes.Select(scope => scope.Workspace).ToList();
        return workspaces.FirstOrDefault(workspace => workspace.Active is { } active && Shows(active))
            ?? workspaces.FirstOrDefault(workspace => workspace.Documents.Any(Shows));
    }

    private void Listen(IDocumentWorkspace workspace)
    {
        if (!_listened.Add(workspace)) return;
        workspace.ContextChanged += OnContextChanged;
        workspace.ActiveChanged += OnActiveChanged;
        workspace.ActiveUpdated += OnActiveUpdated;
        workspace.DocumentOpened += OnOpened;
        workspace.DocumentClosed += OnClosed;
        workspace.DocumentMoved += OnMoved;
        workspace.DocumentDetached += OnDetached;
    }

    private void Forget(IDocumentWorkspace workspace)
    {
        if (!_listened.Remove(workspace)) return;
        workspace.ContextChanged -= OnContextChanged;
        workspace.ActiveChanged -= OnActiveChanged;
        workspace.ActiveUpdated -= OnActiveUpdated;
        workspace.DocumentOpened -= OnOpened;
        workspace.DocumentClosed -= OnClosed;
        workspace.DocumentMoved -= OnMoved;
        workspace.DocumentDetached -= OnDetached;
    }

    private void Refocus()
    {
        ContextChanged?.Invoke(this, EventArgs.Empty);
        if (Active is { } active) ActiveChanged?.Invoke(this, active);
    }

    private bool IsCurrent(object? sender) => ReferenceEquals(sender, Current);

    private void OnContextChanged(object? sender, EventArgs e)
    {
        if (IsCurrent(sender)) ContextChanged?.Invoke(this, e);
    }

    private void OnActiveChanged(object? sender, OpenDocument document)
    {
        if (IsCurrent(sender)) ActiveChanged?.Invoke(this, document);
    }

    private void OnActiveUpdated(object? sender, OpenDocument document)
    {
        if (IsCurrent(sender)) ActiveUpdated?.Invoke(this, document);
    }

    private void OnOpened(object? sender, OpenDocument document)
    {
        if (_inTransit.Remove(document)) DocumentMoved?.Invoke(this, document);
        else DocumentOpened?.Invoke(this, document);
    }

    private void OnClosed(object? sender, OpenDocument document)
    {
        _inTransit.Remove(document);
        DocumentClosed?.Invoke(this, document);
    }

    private void OnMoved(object? sender, OpenDocument document) => DocumentMoved?.Invoke(this, document);

    private void OnDetached(object? sender, OpenDocument document)
    {
        _inTransit.Add(document);
        DocumentDetached?.Invoke(this, document);
    }
}
