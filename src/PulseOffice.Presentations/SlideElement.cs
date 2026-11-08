using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations;

public abstract record SlideElement
{
    public required string Id { get; init; }

    public DocumentRect Bounds { get; init; }

    public int ZOrder { get; init; }

    public double RotationDegrees { get; init; }

    public bool Hidden { get; init; }

    public abstract TResult Accept<TResult>(ISlideElementVisitor<TResult> visitor);
}

public interface ISlideElementVisitor<out TResult>
{
    TResult VisitText(TextElement text);

    TResult VisitObject(ObjectElement element);

    TResult VisitMedia(MediaElement media);
}

public sealed record TextElement : SlideElement
{
    public override TResult Accept<TResult>(ISlideElementVisitor<TResult> visitor) => visitor.VisitText(this);

    public required RichText Text { get; init; }

    public TextBackdrop? Backdrop { get; init; }
}

public sealed record TextBackdrop(PulseOffice.Drawing.Shapes.ShapeKind Shape, PulseOffice.Drawing.Shapes.ShapeFill Fill, PulseOffice.Drawing.Shapes.ShapeLine Line)
{
    public PulseOffice.Drawing.Shapes.ShapeObject AsShape(string id) => new()
    {
        Id = id,
        Shape = Shape,
        Style = PulseOffice.Drawing.Shapes.ShapeStyle.Default with { Fill = Fill, Line = Line },
    };

    public PulseOffice.Drawing.Freeform.FreeformPath? Outline { get; init; }

    public PulseOffice.Document.Embedding.EmbeddedObject AsDrawing(string id) => Outline is { } outline
        ? new PulseOffice.Drawing.Freeform.FreeformObject
        {
            Id = id,
            Path = outline with { Filled = Fill.Kind != PulseOffice.Drawing.Shapes.ShapeFillKind.None },
            Style = PulseOffice.Drawing.Shapes.ShapeStyle.Default with { Fill = Fill, Line = Line },
        }
        : AsShape(id);
}

public sealed record ObjectElement : SlideElement
{
    public override TResult Accept<TResult>(ISlideElementVisitor<TResult> visitor) => visitor.VisitObject(this);

    public required EmbeddedObject Content { get; init; }
}

public enum MediaKind
{
    Audio,
    Video,
}

public sealed record MediaElement : SlideElement
{
    public override TResult Accept<TResult>(ISlideElementVisitor<TResult> visitor) => visitor.VisitMedia(this);

    public required MediaKind Kind { get; init; }

    public required byte[] Data { get; init; }

    public required string MimeType { get; init; }

    public string? FileName { get; init; }

    public byte[]? PosterData { get; init; }

    public string? LinkedPath { get; init; }

    public bool IsLinked => !string.IsNullOrEmpty(LinkedPath);

    public bool Loop { get; init; }

    public double Volume { get; init; } = 1;

    public bool Muted { get; init; }

    public bool HasPlaybackSettings => Loop || Muted || Volume < 0.9995;
}
