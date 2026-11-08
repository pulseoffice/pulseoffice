using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Modules;

namespace PulseOffice.Document.Forms;

public sealed record FormControlObject : EmbeddedObject
{
    public override EmbeddedObjectKind Kind => EmbeddedObjectKind.FormControl;

    public required FormControlKind ControlKind { get; init; }

    public FormControlProperties Properties { get; init; } = new();

    public override IReadOnlyCollection<ModuleKind> EditableIn =>
    [ModuleKind.Sheets, ModuleKind.Documents, ModuleKind.Presentations];
}
