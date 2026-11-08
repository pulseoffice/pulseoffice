namespace PulseOffice.Documents.Layout.Lines;

public readonly record struct LineGeometry(double Height, double Ascent);

public abstract record LineSpacingRule
{
    public static readonly LineSpacingRule Single = new ProportionalSpacing(1.0);

    public abstract LineGeometry Apply(double ascent, double descent);
}

public sealed record ProportionalSpacing(double Factor) : LineSpacingRule
{
    public override LineGeometry Apply(double ascent, double descent)
    {
        var factor = Factor > 0 && double.IsFinite(Factor) ? PulseOffice.Text.Rich.LineSpacingFactor.Bounded(Factor) : 1.0;
        return new LineGeometry((ascent + descent) * factor, ascent * factor);
    }
}

public sealed record AtLeastSpacing(double Minimum) : LineSpacingRule
{
    public override LineGeometry Apply(double ascent, double descent)
    {
        var natural = ascent + descent;
        var height = double.IsFinite(Minimum) ? Math.Max(natural, Minimum) : natural;
        return new LineGeometry(height, ascent + height - natural);
    }
}

public sealed record ExactSpacing(double Height) : LineSpacingRule
{
    public override LineGeometry Apply(double ascent, double descent) =>
        double.IsFinite(Height) && Height > 0 ? new(Height, ascent + Height - (ascent + descent)) : new(ascent + descent, ascent);
}

public sealed record LeadingSpacing(double Extra) : LineSpacingRule
{
    public override LineGeometry Apply(double ascent, double descent)
    {
        if (!double.IsFinite(Extra)) return new(ascent + descent, ascent);
        var height = Math.Max(0, ascent + descent + Extra);
        return new(height, ascent + height - (ascent + descent));
    }
}
