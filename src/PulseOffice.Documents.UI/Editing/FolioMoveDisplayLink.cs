using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Layout.Flow;

namespace PulseOffice.Documents.UI.Editing;

public sealed class FolioMoveDisplayLink(FolioEditor editor, IMoveDisplayOptions options)
{
    private MoveDisplay _seen = options.Current;
    private bool _linked;

    public void Link()
    {
        if (_linked) return;
        _linked = true;
        options.Changed += OnChanged;
        if (options.Current != _seen) OnChanged(this, EventArgs.Empty);
    }

    public void Unlink()
    {
        if (!_linked) return;
        _linked = false;
        options.Changed -= OnChanged;
    }

    private void OnChanged(object? sender, EventArgs e)
    {
        _seen = options.Current;
        if (editor.Document.Changes.All.Any(change => change is Insertion { MoveId: not null } or Deletion { MoveId: not null })) editor.Reflow();
    }
}
