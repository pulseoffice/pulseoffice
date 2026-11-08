using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Document.Embedding;

public sealed record FormulaObject : EmbeddedObject
{
    public override EmbeddedObjectKind Kind => EmbeddedObjectKind.Formula;

    public required string Expression { get; init; }

    public double FontSize { get; init; } = 14;

    public string ColorHex { get; init; } = "#0F172A";

    public static readonly DocumentSize StandardSize =
        new(Twip.FromCentimeters(4), Twip.FromCentimeters(1.5));

    public override IReadOnlyCollection<ModuleKind> EditableIn =>
    [ModuleKind.Sheets, ModuleKind.Documents, ModuleKind.Presentations];
}
