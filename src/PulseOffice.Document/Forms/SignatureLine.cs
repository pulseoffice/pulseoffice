using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Document.Forms;

public sealed record SignatureLineObject : EmbeddedObject
{
    public override EmbeddedObjectKind Kind => EmbeddedObjectKind.FormControl;

    public string? SignerName { get; init; }

    public string? SignerTitle { get; init; }

    public string? SignerEmail { get; init; }

    public string? Instructions { get; init; }

    public bool AllowComments { get; init; } = true;

    public bool ShowDate { get; init; } = true;

    public string? SignedBy { get; init; }

    public DateTimeOffset? SignedOn { get; init; }

    public bool IsSigned => SignedBy is { Length: > 0 };

    public static readonly DocumentSize StandardSize =
        new(Twip.FromCentimeters(7), Twip.FromCentimeters(2.4));

    public override IReadOnlyCollection<ModuleKind> EditableIn =>
    [ModuleKind.Sheets, ModuleKind.Documents, ModuleKind.Presentations];
}
