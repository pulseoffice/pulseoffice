namespace PulseOffice.Foundation.Units;

public readonly record struct DocumentPoint(Twip X, Twip Y)
{
    public static readonly DocumentPoint Origin = new(Twip.Zero, Twip.Zero);
}

public readonly record struct DocumentSize(Twip Width, Twip Height)
{
    public static readonly DocumentSize Empty = new(Twip.Zero, Twip.Zero);
    public bool IsEmpty => Width.Value <= 0 || Height.Value <= 0;
}

public readonly record struct DocumentRect(Twip Left, Twip Top, Twip Width, Twip Height)
{
    public Twip Right => Left + Width;
    public Twip Bottom => Top + Height;
    public bool IsEmpty => Width.Value <= 0 || Height.Value <= 0;

    public bool Contains(DocumentPoint p) =>
        p.X.Value >= Left.Value && p.X.Value < Right.Value &&
        p.Y.Value >= Top.Value && p.Y.Value < Bottom.Value;

    public bool IntersectsWith(DocumentRect o) =>
        Left.Value < o.Right.Value && Right.Value > o.Left.Value &&
        Top.Value < o.Bottom.Value && Bottom.Value > o.Top.Value;
}

public readonly record struct Margins(Twip Left, Twip Top, Twip Right, Twip Bottom)
{
    public static readonly Margins Zero = new(Twip.Zero, Twip.Zero, Twip.Zero, Twip.Zero);
    public static Margins Uniform(Twip all) => new(all, all, all, all);
    public Twip Horizontal => Left + Right;
    public Twip Vertical => Top + Bottom;
}
