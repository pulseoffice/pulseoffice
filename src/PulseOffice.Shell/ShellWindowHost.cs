using Avalonia;

namespace PulseOffice.Shell;

public interface IWorkspaceSet
{
    IReadOnlyList<IDocumentWorkspace> Workspaces { get; }

    IEnumerable<OpenDocument> Documents { get; }

    IDocumentWorkspace? WorkspaceOf(OpenDocument document);

    void Add(IDocumentWorkspace workspace);

    void Remove(IDocumentWorkspace workspace);

    event EventHandler? Changed;
}

public sealed class WorkspaceSet : IWorkspaceSet
{
    private readonly List<IDocumentWorkspace> _workspaces = [];

    public WorkspaceSet(IDocumentWorkspace primary)
    {
        ArgumentNullException.ThrowIfNull(primary);
        _workspaces.Add(primary);
    }

    public IReadOnlyList<IDocumentWorkspace> Workspaces => _workspaces;

    public IEnumerable<OpenDocument> Documents => _workspaces.SelectMany(workspace => workspace.Documents);

    public event EventHandler? Changed;

    public IDocumentWorkspace? WorkspaceOf(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return _workspaces.FirstOrDefault(workspace => workspace.Documents.Contains(document));
    }

    public void Add(IDocumentWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (_workspaces.Contains(workspace)) return;
        _workspaces.Add(workspace);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(IDocumentWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (_workspaces.Remove(workspace)) Changed?.Invoke(this, EventArgs.Empty);
    }
}

public interface IShellWindowHandle
{
    IDocumentWorkspace Workspace { get; }

    void Show(PixelPoint? position);

    void Activate();

    void Close();

    bool Contains(PixelPoint screenPoint);

    int TabIndexAt(PixelPoint screenPoint);

    event EventHandler? Activated;

    event EventHandler? Closed;
}

public interface IShellWindowFactory
{
    IShellWindowHandle Create();
}

public interface IShellWindowHost
{
    IShellWindowHandle Primary { get; }

    IReadOnlyList<IShellWindowHandle> Windows { get; }

    IShellWindowHandle Focused { get; }

    IShellWindowHandle? WindowOf(OpenDocument document);

    IShellWindowHandle? WindowAt(PixelPoint screenPoint);

    IShellWindowHandle? TearOff(OpenDocument document, PixelPoint position);

    bool MoveTo(OpenDocument document, IShellWindowHandle target, int index);

    bool DropAt(OpenDocument document, PixelPoint screenPoint);
}

public sealed class ShellWindowHost : IShellWindowHost
{
    private readonly IWorkspaceSet _workspaces;
    private readonly IShellWindowFactory _factory;
    private readonly IQuitTarget _quit;
    private readonly IShellWindowFocus? _focus;
    private readonly List<IShellWindowHandle> _windows = [];

    public ShellWindowHost(IShellWindowHandle primary, IWorkspaceSet workspaces, IShellWindowFactory factory, IQuitTarget quit)
        : this(primary, workspaces, factory, quit, null)
    {
    }

    public ShellWindowHost(
        IShellWindowHandle primary, IWorkspaceSet workspaces, IShellWindowFactory factory, IQuitTarget quit, IShellWindowFocus? focus)
    {
        _focus = focus;
        ArgumentNullException.ThrowIfNull(primary);
        _workspaces = workspaces ?? throw new ArgumentNullException(nameof(workspaces));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _quit = quit ?? throw new ArgumentNullException(nameof(quit));
        Primary = primary;
        Focused = primary;
        Track(primary);
    }

    public IShellWindowHandle Primary { get; private set; }

    public IReadOnlyList<IShellWindowHandle> Windows => _windows;

    public IShellWindowHandle Focused { get; private set; }

    public IShellWindowHandle? WindowOf(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return _windows.FirstOrDefault(window => window.Workspace.Documents.Contains(document));
    }

    public IShellWindowHandle? WindowAt(PixelPoint screenPoint) =>
        _windows.FirstOrDefault(window => ReferenceEquals(window, Focused) && window.Contains(screenPoint))
        ?? _windows.FirstOrDefault(window => window.Contains(screenPoint));

    public IShellWindowHandle? TearOff(OpenDocument document, PixelPoint position)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (WindowOf(document) is not { } source) return null;
        if (source.Workspace.Documents.Count < 2) return null;

        var target = _factory.Create();
        if (!source.Workspace.Detach(document))
        {
            target.Close();
            return null;
        }

        Track(target);
        target.Workspace.Open(document);
        target.Show(position);
        target.Activate();
        SetFocused(target);
        return target;
    }

    public bool MoveTo(OpenDocument document, IShellWindowHandle target, int index)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(target);
        if (!_windows.Contains(target) || WindowOf(document) is not { } source) return false;

        if (ReferenceEquals(source, target))
        {
            if (index >= 0) source.Workspace.Move(document, index);
            source.Workspace.Activate(document);
            return true;
        }

        if (!source.Workspace.Detach(document)) return false;
        target.Workspace.Open(document);
        if (index >= 0) target.Workspace.Move(document, index);
        target.Activate();
        SetFocused(target);
        if (source.Workspace.Documents.Count == 0 && !ReferenceEquals(source, Primary)) source.Close();
        return true;
    }

    public bool DropAt(OpenDocument document, PixelPoint screenPoint)
    {
        ArgumentNullException.ThrowIfNull(document);
        var source = WindowOf(document);
        var target = _windows.FirstOrDefault(window => !ReferenceEquals(window, source) && window.Contains(screenPoint));
        if (target is not null) return MoveTo(document, target, target.TabIndexAt(screenPoint));
        return TearOff(document, screenPoint) is not null;
    }

    private void SetFocused(IShellWindowHandle window)
    {
        Focused = window;
        if (window is IShellWindowScopeOwner owner) _focus?.Focus(owner.Scope);
    }

    private void Track(IShellWindowHandle window)
    {
        _windows.Add(window);
        _workspaces.Add(window.Workspace);
        if (window is IShellWindowScopeOwner owner) _focus?.Enter(owner.Scope);
        window.Activated += (_, _) =>
        {
            if (_windows.Contains(window)) SetFocused(window);
        };
        window.Closed += (_, _) => Forget(window);
        window.Workspace.DocumentClosed += (_, _) => CloseWhenEmptied(window);
    }

    private void CloseWhenEmptied(IShellWindowHandle window)
    {
        if (window.Workspace.Documents.Count > 0 || ReferenceEquals(window, Primary) || !_windows.Contains(window)) return;
        window.Close();
    }

    private void Forget(IShellWindowHandle window)
    {
        if (!_windows.Remove(window)) return;
        if (_windows.Count == 0)
        {
            _quit.Quit();
            return;
        }

        foreach (var document in window.Workspace.Documents.ToList()) window.Workspace.Close(document);
        _workspaces.Remove(window.Workspace);
        if (window is IShellWindowScopeOwner owner) _focus?.Leave(owner.Scope);
        if (ReferenceEquals(Primary, window)) Primary = _windows[0];
        if (ReferenceEquals(Focused, window)) SetFocused(Primary);
    }
}
