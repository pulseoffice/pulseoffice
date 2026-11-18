namespace PulseOffice.Sheets;

public readonly record struct PrintTitleBand
{
    public PrintTitleBand(int start, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        Start = count > 0 ? start : 0;
        Count = Math.Max(count, 0);
    }

    public int Start { get; }

    public int Count { get; }

    public bool IsEmpty => Count == 0;

    public int End => Start + Count - 1;

    public PrintTitleBand WithCount(int count) => new(Start, count);
}
