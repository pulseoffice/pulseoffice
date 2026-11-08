using PulseOffice.Foundation.Units;

namespace PulseOffice.Presentations;

public enum SlideSizePreset { Widescreen, Widescreen1610, Standard, A4, Letter, A3, Banner, Custom }

public interface ISlideResize
{
    SlideSize For(SlideSizePreset preset);

    SlideSizePreset Recognize(SlideSize size);

    SlideSize Rotated(SlideSize size);

    bool IsPortrait(SlideSize size);

    DocumentRect Fit(DocumentRect bounds, SlideSize from, SlideSize to);

    SlideMaster Fit(SlideMaster master, SlideSize from, SlideSize to);

    DocumentRect Stretch(DocumentRect bounds, SlideSize from, SlideSize to);
}

public sealed class SlideResize : ISlideResize
{
    private const int Tolerance = 20;

    private readonly IReadOnlyDictionary<SlideSizePreset, SlideSize> _presets = new Dictionary<SlideSizePreset, SlideSize>
    {
        [SlideSizePreset.Widescreen] = SlideSize.Widescreen,
        [SlideSizePreset.Widescreen1610] = new(Twip.FromCentimeters(30.48), Twip.FromCentimeters(19.05)),
        [SlideSizePreset.Standard] = SlideSize.Standard,
        [SlideSizePreset.A4] = new(Twip.FromCentimeters(27.517), Twip.FromCentimeters(19.05)),
        [SlideSizePreset.Letter] = new(Twip.FromCentimeters(25.4), Twip.FromCentimeters(19.05)),
        [SlideSizePreset.A3] = new(Twip.FromCentimeters(39.688), Twip.FromCentimeters(27.517)),
        [SlideSizePreset.Banner] = new(Twip.FromCentimeters(20.32), Twip.FromCentimeters(2.54)),
    };

    public SlideSize For(SlideSizePreset preset) => _presets.GetValueOrDefault(preset, SlideSize.Widescreen);

    public SlideSizePreset Recognize(SlideSize size) =>
        _presets.Where(pair => Near(pair.Value, size) || Near(Rotated(pair.Value), size))
            .Select(pair => (SlideSizePreset?)pair.Key)
            .FirstOrDefault() ?? SlideSizePreset.Custom;

    public SlideSize Rotated(SlideSize size) => new(size.Height, size.Width);

    public bool IsPortrait(SlideSize size) => size.Height.Value > size.Width.Value;

    public DocumentRect Fit(DocumentRect bounds, SlideSize from, SlideSize to) =>
        from.Width.Value <= 0 || from.Height.Value <= 0 ? bounds : new UniformFit(from, to).Apply(bounds);

    public SlideMaster Fit(SlideMaster master, SlideSize from, SlideSize to) =>
        master with
        {
            Objects = [.. master.Objects.Select(element => element with { Bounds = Stretch(element.Bounds, from, to) })],
            Layouts = [.. master.Layouts.Select(layout => layout with
            {
                Placeholders = [.. layout.Placeholders.Select(p => p with { Bounds = Fit(p.Bounds, from, to) })],
                Objects = [.. layout.Objects.Select(element => element with { Bounds = Stretch(element.Bounds, from, to) })],
            })],
        };

    public DocumentRect Stretch(DocumentRect bounds, SlideSize from, SlideSize to)
    {
        if (from.Width.Value <= 0 || from.Height.Value <= 0) return bounds;
        var x = (double)to.Width.Value / from.Width.Value;
        var y = (double)to.Height.Value / from.Height.Value;
        return new DocumentRect(
            new Twip((int)Math.Round(bounds.Left.Value * x)),
            new Twip((int)Math.Round(bounds.Top.Value * y)),
            new Twip((int)Math.Round(bounds.Width.Value * x)),
            new Twip((int)Math.Round(bounds.Height.Value * y)));
    }

    private bool Near(SlideSize a, SlideSize b) =>
        Math.Abs(a.Width.Value - b.Width.Value) <= Tolerance && Math.Abs(a.Height.Value - b.Height.Value) <= Tolerance;

    private sealed class UniformFit
    {
        private readonly double _factor;
        private readonly double _offsetX;
        private readonly double _offsetY;

        public UniformFit(SlideSize from, SlideSize to)
        {
            _factor = Math.Min((double)to.Width.Value / from.Width.Value, (double)to.Height.Value / from.Height.Value);
            _offsetX = (to.Width.Value - from.Width.Value * _factor) / 2;
            _offsetY = (to.Height.Value - from.Height.Value * _factor) / 2;
        }

        public DocumentRect Apply(DocumentRect bounds) => new(
            Twips(bounds.Left.Value * _factor + _offsetX),
            Twips(bounds.Top.Value * _factor + _offsetY),
            Twips(bounds.Width.Value * _factor),
            Twips(bounds.Height.Value * _factor));

        private Twip Twips(double value) => new((int)Math.Round(value));
    }
}
