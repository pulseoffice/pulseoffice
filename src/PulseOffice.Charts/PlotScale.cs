namespace PulseOffice.Charts;

public sealed record PlotScale(double Minimum, double Maximum, double From, double To)
{
    public double Range => Maximum - Minimum;

    public double Span => To - From;

    public double Position(double value) =>
        Maximum / 2 - Minimum / 2 is var half && half > 0
            ? From + Span * ((value / 2 - Minimum / 2) / half)
            : From;

    public double Value(double position) =>
        Span == 0 ? Minimum : Minimum + Range * ((position - From) / Span);

    public static PlotScale Nice(double minimum, double maximum, double from, double to)
    {
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || minimum > maximum)
            return new PlotScale(0, 1, from, to);

        if (Math.Abs(maximum - minimum) < 1e-12)
        {
            var padding = Math.Abs(maximum) < 1e-12 ? 1 : Math.Abs(maximum) * 0.1;
            return new PlotScale(minimum - padding, maximum + padding, from, to);
        }

        if (!double.IsFinite(maximum - minimum))
            return new PlotScale(minimum, maximum, from, to);

        var step = Step(maximum - minimum);
        if (!double.IsFinite(step) || step <= 0)
            return new PlotScale(minimum, maximum, from, to);

        var lower = Math.Floor(minimum / step) * step;
        var upper = Math.Ceiling(maximum / step) * step;

        if (minimum >= 0 && lower < 0) lower = 0;

        return new PlotScale(lower, upper, from, to);
    }

    public IReadOnlyList<double> Ticks(int count = 5)
    {
        if (count < 2 || !(Maximum > Minimum)) return [Minimum];

        var last = count - 1;

        return [.. Enumerable.Range(0, count).Select(index =>
            index == last ? Maximum : Minimum * ((double)(last - index) / last) + Maximum * ((double)index / last))];
    }

    public IReadOnlyList<double> Divisions(int most = 10)
    {
        if (!(Maximum > Minimum) || !double.IsFinite(Range)) return [Minimum];

        most = Math.Max(1, most);
        var step = Step(Range);
        while (Range / step > most) step *= 2;
        if (!double.IsFinite(step) || step <= 0) return Ticks();

        var first = Math.Ceiling(Minimum / step - 1e-9) * step;
        var ticks = new List<double>();
        for (var tick = first; tick <= Maximum + step * 1e-9 && ticks.Count <= most + 1; tick += step)
            ticks.Add(Math.Abs(tick) < step * 1e-9 ? 0 : tick);
        return ticks.Count < 2 ? Ticks() : ticks;
    }

    private static double Step(double range)
    {
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(range)));
        var normalised = range / magnitude;

        var factor = normalised switch
        {
            <= 1.5 => 0.2,
            <= 3 => 0.5,
            <= 7 => 1,
            _ => 2,
        };

        return factor * magnitude;
    }
}

public sealed record CategoryScale(int Count, double From, double To, bool Reversed = false)
{
    public double Width => Count <= 0 ? To - From : (To - From) / Count;

    public double Centre(int index) => Start(index) + Width * 0.5;

    public double Start(int index) => From + Width * (Reversed ? Count - 1 - index : index);

    public (double Start, double Width) Slot(int index, int series, int seriesCount)
    {
        if (seriesCount <= 0) return (Start(index), Width);

        var inner = Width / (seriesCount + 0.5);
        var padding = inner / 4;

        return (Start(index) + padding + inner * series, inner * 0.9);
    }
}
