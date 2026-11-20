using Avalonia.Controls;
using PulseOffice.UI.Controls.References;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Shell;

public sealed class FocusedDialogs : IDialogService, IDialogCollapse
{
    private readonly IShellWindowFocus _focus;
    private readonly HashSet<IDialogCollapse> _listened = new(ReferenceEqualityComparer.Instance);

    public FocusedDialogs(IShellWindowFocus focus)
    {
        _focus = focus ?? throw new ArgumentNullException(nameof(focus));
        foreach (var scope in focus.Scopes) Listen(scope.DialogCollapse);
        focus.Entered += (_, scope) => Listen(scope.DialogCollapse);
        focus.Left += (_, scope) =>
        {
            if (_listened.Remove(scope.DialogCollapse)) scope.DialogCollapse.CollapsedChanged -= OnCollapsedChanged;
        };
    }

    public bool IsDialogOpen => _focus.Scopes.Any(scope => scope.Dialogs.IsDialogOpen);

    public bool IsCollapsed => _focus.Current.DialogCollapse.IsCollapsed;

    public string? TopDialogTitle => _focus.Current.DialogCollapse.TopDialogTitle;

    public event EventHandler? CollapsedChanged;

    public Task<TResult?> ShowAsync<TResult>(Control content, DialogOptions options, CancellationToken ct = default) =>
        _focus.Current.Dialogs.ShowAsync<TResult>(content, options, ct);

    public Task ShowAsync(Control content, DialogOptions options, CancellationToken ct = default) =>
        _focus.Current.Dialogs.ShowAsync(content, options, ct);

    public void Close(object? result)
    {
        var target = _focus.Current.Dialogs.IsDialogOpen
            ? _focus.Current.Dialogs
            : _focus.Scopes.Select(scope => scope.Dialogs).FirstOrDefault(dialogs => dialogs.IsDialogOpen);
        target?.Close(result);
    }

    public IDisposable Collapse(Control bar, Action interrupted) => _focus.Current.DialogCollapse.Collapse(bar, interrupted);

    private void Listen(IDialogCollapse collapse)
    {
        if (_listened.Add(collapse)) collapse.CollapsedChanged += OnCollapsedChanged;
    }

    private void OnCollapsedChanged(object? sender, EventArgs e) => CollapsedChanged?.Invoke(this, e);
}
