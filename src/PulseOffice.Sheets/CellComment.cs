using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public sealed record CellComment
{
    public required string Text { get; init; }

    public string? Author { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public bool IsAlwaysVisible { get; init; }
}

public sealed record OutlineSummary(bool Below = true, bool Right = true)
{
    public static OutlineSummary Default { get; } = new();
}

public sealed record OutlineGroup
{
    public required int First { get; init; }

    public required int Last { get; init; }

    public int Level { get; init; } = 1;

    public bool IsCollapsed { get; init; }

    public bool Contains(int index) => index >= First && index <= Last;

    public int Count => Last - First + 1;
}
