using PulseOffice.Documents.Editing;

namespace PulseOffice.Documents.UI.Editing;

public sealed class FolioFormatBrush(FolioEditor editor)
{
    public FormatSample? Sample { get; private set; }

    public event EventHandler? Changed;

    public void Arm(FormatSample? sample)
    {
        Sample = sample;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Paint()
    {
        if (Sample is not { } sample) return false;
        var collapsed = !editor.HasSelection;
        var range = collapsed ? editor.CharacterRange() : editor.Selection;
        Arm(null);
        if (editor.RefusesEdit(range)) return false;
        return editor.Change(() => editor.Services.Brush.Paint(editor.Document, editor.History, range, sample, collapsed));
    }
}
