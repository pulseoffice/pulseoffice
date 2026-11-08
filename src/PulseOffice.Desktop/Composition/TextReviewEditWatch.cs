using PulseOffice.Shell;
using PulseOffice.UI.Controls.Spelling;

namespace PulseOffice.Desktop.Composition;

public interface ITextReviewEditWatch
{
    void Start();
}

public sealed class TextReviewEditWatch(IDocumentWorkspace workspace, ITextReviewSession session) : ITextReviewEditWatch
{
    private OpenDocument? _watched;
    private bool _started;

    public void Start()
    {
        if (_started) return;
        _started = true;
        workspace.ActiveChanged += OnActiveChanged;
        workspace.DocumentClosed += OnClosed;
        Watch(workspace.Active);
    }

    private void OnActiveChanged(object? sender, OpenDocument document)
    {
        if (!ReferenceEquals(document, _watched) && session.Pending is not null) session.Dismiss();
        Watch(document);
    }

    private void OnClosed(object? sender, OpenDocument document)
    {
        if (!ReferenceEquals(document, _watched)) return;
        if (session.Pending is not null) session.Dismiss();
        Watch(workspace.Active);
    }

    private void Watch(OpenDocument? document)
    {
        if (ReferenceEquals(document, _watched)) return;
        if (_watched is not null) _watched.History.Changed -= OnEdited;
        _watched = document;
        if (_watched is not null) _watched.History.Changed += OnEdited;
    }

    private void OnEdited(object? sender, EventArgs e)
    {
        if (session.Pending is not null) session.Invalidate();
    }
}
