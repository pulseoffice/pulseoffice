namespace PulseOffice.Drawing.Assets;

public sealed class QrMask(int number, Func<int, int, bool> inverts)
{
    public int Number { get; } = number;

    public bool Inverts(int x, int y) => inverts(x, y);

    public static IReadOnlyList<QrMask> All { get; } =
    [
        new(0, (x, y) => (x + y) % 2 == 0),
        new(1, (_, y) => y % 2 == 0),
        new(2, (x, _) => x % 3 == 0),
        new(3, (x, y) => (x + y) % 3 == 0),
        new(4, (x, y) => ((x / 3) + (y / 2)) % 2 == 0),
        new(5, (x, y) => ((x * y) % 2) + ((x * y) % 3) == 0),
        new(6, (x, y) => (((x * y) % 2) + ((x * y) % 3)) % 2 == 0),
        new(7, (x, y) => (((x + y) % 2) + ((x * y) % 3)) % 2 == 0),
    ];
}
