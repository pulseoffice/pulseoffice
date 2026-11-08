using PulseOffice.Document.Forms;
using PulseOffice.UI.Controls.Forms;

namespace PulseOffice.Shell;

public sealed class FormDesignModeOnOpen(IDocumentWorkspace workspace, IFormDesignMode design)
{
    private bool _started;

    public void Start()
    {
        if (_started) return;
        _started = true;

        workspace.DocumentOpened += (_, document) => Apply(document);
    }

    public void Apply(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Model is IFormDocument { FormSettings.OpenInDesignMode: true }) design.Set(true);
    }
}
