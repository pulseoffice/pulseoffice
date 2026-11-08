namespace PulseOffice.UI.Commands;

public interface IRibbonDisplayMap
{
    RibbonDisplay DisplayOf(string commandId);
}

public sealed class RibbonDisplayMap(IReadOnlyList<(string Match, RibbonDisplay Display)> rules) : IRibbonDisplayMap
{
    public static readonly RibbonDisplayMap None = new([]);

    public RibbonDisplay DisplayOf(string commandId)
    {
        ArgumentNullException.ThrowIfNull(commandId);

        foreach (var (match, display) in rules)
            if (Matches(match, commandId)) return display;

        return RibbonDisplay.Auto;
    }

    private bool Matches(string match, string id) =>
        match.EndsWith('*') ? id.StartsWith(match[..^1], StringComparison.Ordinal)
        : match.EndsWith('.') ? id.StartsWith(match, StringComparison.Ordinal)
        : string.Equals(match, id, StringComparison.Ordinal);
}
