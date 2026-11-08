namespace PulseOffice.Presentations;

public enum ShowAdvanceMode
{
    UseTimings,
    Manual,
}

public sealed record ShowSetup
{
    public bool Loop { get; init; }

    public int FromSlide { get; init; }

    public int ToSlide { get; init; }

    public ShowAdvanceMode Advance { get; init; } = ShowAdvanceMode.UseTimings;

    public bool WithoutAnimation { get; init; }

    public string PenColor { get; init; } = "#E11D2E";

    public bool HasRange => FromSlide > 0 || ToSlide > 0;

    public ShowSetup WithoutSlide(int position, int remaining)
    {
        if (!HasRange) return this;
        var from = FromSlide > 0 && position < FromSlide ? FromSlide - 1 : FromSlide;
        var to = ToSlide > 0 && position <= ToSlide ? ToSlide - 1 : ToSlide;
        from = Math.Min(from, remaining);
        to = Math.Min(to, remaining);
        var empty = ToSlide > 0 && to < Math.Max(from, 1);
        return empty ? this with { FromSlide = 0, ToSlide = 0 } : this with { FromSlide = from, ToSlide = to };
    }
}
