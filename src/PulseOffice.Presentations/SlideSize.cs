using PulseOffice.Foundation.Units;

namespace PulseOffice.Presentations;

public enum SlideAspect
{
    Widescreen,
    Standard,
}

public readonly record struct SlideSize(Twip Width, Twip Height)
{
    public static readonly SlideSize Widescreen =
        new(Twip.FromCentimeters(33.87), Twip.FromCentimeters(19.05));

    public static readonly SlideSize Standard =
        new(Twip.FromCentimeters(25.4), Twip.FromCentimeters(19.05));

    public static SlideSize For(SlideAspect aspect) =>
        aspect == SlideAspect.Standard ? Standard : Widescreen;

    public double AspectRatio => Height.Value == 0 ? 1 : (double)Width.Value / Height.Value;

    public DocumentSize AsDocumentSize => new(Width, Height);
}
