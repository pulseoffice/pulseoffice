using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Modules;

namespace PulseOffice.Drawing.Scenes;

public sealed record Scene3DObject : EmbeddedObject, IDrawingObject
{
    public const string OdfFormat = "odf-dr3d-scene";

    public TResult Accept<TResult>(IDrawingObjectVisitor<TResult> visitor) => visitor.VisitScene(this);

    public string Format { get; init; } = OdfFormat;

    public required string Markup { get; init; }

    public IReadOnlyList<string> Styles { get; init; } = [];

    public override EmbeddedObjectKind Kind => EmbeddedObjectKind.Shape;

    public override IReadOnlyCollection<ModuleKind> EditableIn =>
        [ModuleKind.Documents, ModuleKind.Sheets, ModuleKind.Presentations];

    public bool Equals(Scene3DObject? other) =>
        other is not null && base.Equals(other)
        && string.Equals(Format, other.Format, StringComparison.Ordinal)
        && string.Equals(Markup, other.Markup, StringComparison.Ordinal)
        && Styles.SequenceEqual(other.Styles, StringComparer.Ordinal);

    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Markup.Length, Styles.Count);
}
