using Avalonia.Controls;
using PulseOffice.Localization;
using PulseOffice.UI.Icons;

namespace PulseOffice.UI.Controls.References;

public interface IDialogCollapse
{
    bool IsCollapsed { get; }

    string? TopDialogTitle { get; }

    IDisposable Collapse(Control bar, Action interrupted);

    event EventHandler? CollapsedChanged;
}

public interface IReferencePickListener
{
    void SelectionChanged(string reference);

    void Commit();

    void Cancel();
}

public interface IReferencePickTarget
{
    IDisposable Begin(string current, IReferencePickListener listener);
}

public sealed record ReferencePickRequest(string Label, string Current, Func<string, bool> IsValid);

public interface IReferencePicker
{
    IconGeometry? Icon { get; }

    Task<string?> PickAsync(ReferencePickRequest request);
}

public interface IReferencePickers
{
    IReferencePicker For(IReferencePickTarget target);
}

public sealed class ReferencePickers(IDialogCollapse collapse, ITextCatalog texts, IIconCatalog icons) : IReferencePickers
{
    public IReferencePicker For(IReferencePickTarget target) => new ReferencePicker(collapse, texts, icons, target);
}

public sealed class ReferencePicker(
    IDialogCollapse collapse, ITextCatalog texts, IIconCatalog icons, IReferencePickTarget target) : IReferencePicker
{
    private bool _picking;

    public IconGeometry? Icon => icons.Find("ui.pickRange");

    public async Task<string?> PickAsync(ReferencePickRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_picking || collapse.IsCollapsed || collapse.TopDialogTitle is null) return null;

        _picking = true;
        var bar = new ReferencePickBar(collapse.TopDialogTitle ?? string.Empty, request, texts, icons);
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        bar.Finished += (_, result) => completion.TrySetResult(result);

        try
        {
            using (collapse.Collapse(bar, () => completion.TrySetResult(null)))
            using (target.Begin(request.Current, bar))
            {
                bar.FocusEntry();
                return await completion.Task.ConfigureAwait(true);
            }
        }
        finally
        {
            _picking = false;
        }
    }
}
