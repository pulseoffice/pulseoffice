namespace PulseOffice.UI.Controls;

public readonly record struct NumericBounds
{
    public const double Limit = 1e15;

    public NumericBounds(double minimum, double maximum, double step = 1)
    {
        var low = Finite(minimum, -Limit);
        var high = Finite(maximum, Limit);
        if (low > high) (low, high) = (high, low);

        Minimum = low;
        Maximum = high;
        Step = double.IsFinite(step) && step > 0 ? Math.Min(step, Limit) : 1;
    }

    public double Minimum { get; }

    public double Maximum { get; }

    public double Step { get; }

    public double Clamp(double value) =>
        double.IsNaN(value) ? Math.Clamp(0, Minimum, Maximum) : Math.Clamp(value, Minimum, Maximum);

    public decimal ClampDecimal(double value) => (decimal)Clamp(value);

    public double Read(decimal? value, double fallback) =>
        value is { } chosen ? Clamp((double)chosen) : Clamp(fallback);

    private static double Finite(double value, double limit) =>
        double.IsNaN(value) ? limit : Math.Clamp(value, -Limit, Limit);
}
