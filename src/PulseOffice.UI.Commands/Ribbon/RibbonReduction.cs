namespace PulseOffice.UI.Commands;

public enum RibbonGroupLevel { Full, Medium, Compact, Partial, Collapsed }

public sealed record RibbonGroupState(RibbonGroupLevel Level, int Visible, int Count, int Minimum)
{
    public static RibbonGroupState Initial(int count, int minimum) =>
        new(RibbonGroupLevel.Full, count, count, Math.Min(count, minimum));

    public bool CanTrim => Level is RibbonGroupLevel.Compact or RibbonGroupLevel.Partial && Visible > Minimum;
}

public sealed record RibbonReductionStep(int Index, RibbonGroupState State);

public interface IRibbonReductionPlanner
{
    RibbonReductionStep? Next(IReadOnlyList<RibbonGroupState> groups);
}

public sealed class RibbonReductionPlanner : IRibbonReductionPlanner
{
    public RibbonReductionStep? Next(IReadOnlyList<RibbonGroupState> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        return Promote(groups, RibbonGroupLevel.Full, RibbonGroupLevel.Medium)
            ?? Promote(groups, RibbonGroupLevel.Medium, RibbonGroupLevel.Compact)
            ?? Trim(groups)
            ?? Collapse(groups);
    }

    private RibbonReductionStep? Promote(
        IReadOnlyList<RibbonGroupState> groups, RibbonGroupLevel from, RibbonGroupLevel to)
    {
        for (var index = groups.Count - 1; index >= 0; index--)
            if (groups[index].Level == from)
                return new RibbonReductionStep(index, groups[index] with { Level = to });

        return null;
    }

    private RibbonReductionStep? Trim(IReadOnlyList<RibbonGroupState> groups)
    {
        var chosen = -1;

        for (var index = 0; index < groups.Count; index++)
            if (groups[index].CanTrim && (chosen < 0 || groups[index].Visible >= groups[chosen].Visible))
                chosen = index;

        return chosen < 0
            ? null
            : new RibbonReductionStep(chosen, groups[chosen] with
            {
                Level = RibbonGroupLevel.Partial,
                Visible = groups[chosen].Visible - 1,
            });
    }

    private RibbonReductionStep? Collapse(IReadOnlyList<RibbonGroupState> groups)
    {
        for (var index = groups.Count - 1; index >= 0; index--)
            if (groups[index].Level != RibbonGroupLevel.Collapsed)
                return new RibbonReductionStep(index, groups[index] with { Level = RibbonGroupLevel.Collapsed });

        return null;
    }
}
