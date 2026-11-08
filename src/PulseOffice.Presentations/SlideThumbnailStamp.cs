using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations;

public sealed class SlideThumbnailStamp : IEquatable<SlideThumbnailStamp>
{
    private readonly object?[] _parts;

    private SlideThumbnailStamp(object?[] parts) => _parts = parts;

    public static SlideThumbnailStamp Of(Presentation deck, Slide slide, bool current)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(slide);
        object?[] header =
        [
            slide, current, deck.IndexOf(slide), HasFields(slide) ? deck.Slides.Count : -1, deck.Size, deck.HeaderFooter, deck.FileName,
            deck.MasterOf(slide), deck.ThemeOf(slide), slide.Layout, slide.Background, slide.BackgroundImage,
            slide.Name, slide.Hidden, slide.ShowMasterBackground, slide.ShowMasterObjects, slide.Elements.Count,
        ];
        return new SlideThumbnailStamp([.. header, .. slide.Elements]);
    }

    private static bool HasFields(Slide slide) =>
        slide.Elements.OfType<TextElement>()
            .Any(text => text.Text.Paragraphs.Any(paragraph => paragraph.Runs.Any(run => run.Field != TextFieldKind.None)));

    public bool Equals(SlideThumbnailStamp? other) =>
        other is not null && _parts.Length == other._parts.Length
        && _parts.Zip(other._parts).All(pair => Same(pair.First, pair.Second));

    private bool Same(object? left, object? right) =>
        ReferenceEquals(left, right)
        || left is ValueType or string or HeaderFooterSettings && Equals(left, right);

    public override bool Equals(object? obj) => Equals(obj as SlideThumbnailStamp);

    public override int GetHashCode() => _parts.Length;
}
