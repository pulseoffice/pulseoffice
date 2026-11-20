using PulseOffice.UI.Controls.References;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Shell;

public sealed class ShellWindowScope(
    IDocumentWorkspace workspace, IDialogService dialogs, IDialogCollapse dialogCollapse, ISidePanelHost? sidePanel = null)
{
    public IDocumentWorkspace Workspace { get; } = workspace ?? throw new ArgumentNullException(nameof(workspace));

    public IDialogService Dialogs { get; } = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

    public IDialogCollapse DialogCollapse { get; } = dialogCollapse ?? throw new ArgumentNullException(nameof(dialogCollapse));

    public ISidePanelHost? SidePanel { get; } = sidePanel;
}

public interface IShellWindowScopeOwner
{
    ShellWindowScope Scope { get; }
}

public interface IShellWindowFocus
{
    ShellWindowScope Current { get; }

    IReadOnlyList<ShellWindowScope> Scopes { get; }

    void Enter(ShellWindowScope scope);

    void Focus(ShellWindowScope scope);

    void Leave(ShellWindowScope scope);

    event EventHandler? FocusChanged;

    event EventHandler<ShellWindowScope>? Entered;

    event EventHandler<ShellWindowScope>? Left;
}

public sealed class ShellWindowFocus : IShellWindowFocus
{
    private readonly List<ShellWindowScope> _scopes = [];

    public ShellWindowFocus(ShellWindowScope primary)
    {
        ArgumentNullException.ThrowIfNull(primary);
        _scopes.Add(primary);
        Current = primary;
    }

    public ShellWindowScope Current { get; private set; }

    public IReadOnlyList<ShellWindowScope> Scopes => _scopes;

    public event EventHandler? FocusChanged;

    public event EventHandler<ShellWindowScope>? Entered;

    public event EventHandler<ShellWindowScope>? Left;

    public void Enter(ShellWindowScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (Find(scope) is not null) return;
        _scopes.Add(scope);
        Entered?.Invoke(this, scope);
    }

    public void Focus(ShellWindowScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (Find(scope) is not { } known || ReferenceEquals(known, Current)) return;
        Current = known;
        FocusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Leave(ShellWindowScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (Find(scope) is not { } known || _scopes.Count == 1) return;
        _scopes.Remove(known);
        Left?.Invoke(this, known);
        if (!ReferenceEquals(known, Current)) return;
        Current = _scopes[0];
        FocusChanged?.Invoke(this, EventArgs.Empty);
    }

    private ShellWindowScope? Find(ShellWindowScope scope) =>
        _scopes.FirstOrDefault(known => ReferenceEquals(known.Workspace, scope.Workspace));
}
