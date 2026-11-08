namespace PulseOffice.Sheets;

public sealed record FrozenPanes(int Rows, int Columns)
{
    public static readonly FrozenPanes None = new(0, 0);

    public bool IsFrozen => Rows > 0 || Columns > 0;
}
