namespace PulseOffice.UI.Icons;

public sealed record IconGeometry(string Path)
{
    public const double Grid = 24;

    public bool Filled { get; init; }
}
