using PulseOffice.Foundation.Zoom;
using PulseOffice.Documents.UI.Canvas;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.UI.Controls.Zoom;

namespace PulseOffice.Documents.UI;

public sealed record FolioTarget(
    FolioEditor Editor,
    Func<ViewZoom> Zoom,
    Action<ViewZoom> SetZoom,
    Func<double> EffectiveZoom,
    Func<Task> CopyAsync,
    Func<Task> CutAsync,
    Func<Task> PasteAsync)
{
    public ZoomSubject ZoomSubject => new(Zoom, EffectiveZoom, SetZoom, ZoomLimits.Pages);
}

public interface IActiveFolioContext
{
    FolioTarget? Target { get; }

    void Attach(FolioTarget target);

    void Detach(FolioTarget target);

    event EventHandler? Changed;
}

public sealed class ActiveFolioContext : IActiveFolioContext
{
    public FolioTarget? Target { get; private set; }

    public event EventHandler? Changed;

    public void Attach(FolioTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Target = target;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Detach(FolioTarget target)
    {
        if (!ReferenceEquals(Target, target)) return;
        Target = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
