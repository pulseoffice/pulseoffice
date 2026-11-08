namespace PulseOffice.Presentations;

public sealed record PageBox(double X, double Y, double Width, double Height)
{
    public PageBox Clamped()
    {
        var width = Math.Clamp(Width, 0.05, 1);
        var height = Math.Clamp(Height, 0.05, 1);
        return new PageBox(Math.Clamp(X, 0, 1 - width), Math.Clamp(Y, 0, 1 - height), width, height);
    }
}

public sealed record NotesMaster
{
    public PageBox SlideBox { get; init; } = new(0.125, 0.08, 0.75, 0.36);

    public PageBox NotesBox { get; init; } = new(0.1, 0.48, 0.8, 0.42);

    public double FontSize { get; init => field = double.IsFinite(value) ? Math.Clamp(value, 1, 4000) : 12; } = 12;

    public string Header { get; init; } = string.Empty;

    public string Footer { get; init; } = string.Empty;

    public bool PageNumber { get; init; } = true;
}

public sealed record HandoutMaster
{
    public static IReadOnlyList<int> Choices { get; } = [1, 2, 3, 4, 6, 9];

    public int SlidesPerPage { get; init; } = 6;

    public string Header { get; init; } = string.Empty;

    public string Footer { get; init; } = string.Empty;

    public bool PageNumber { get; init; } = true;
}

public interface IHandoutLayout
{
    (int Columns, int Rows) Grid(int slidesPerPage);

    IReadOnlyList<PageBox> Cells(int slidesPerPage);

    int Nearest(int slidesPerPage);
}

public sealed class HandoutLayout : IHandoutLayout
{
    private const double Margin = 0.08;
    private const double Gap = 0.03;

    public (int Columns, int Rows) Grid(int slidesPerPage) => Nearest(slidesPerPage) switch
    {
        1 => (1, 1),
        2 => (1, 2),
        3 => (1, 3),
        4 => (2, 2),
        6 => (2, 3),
        _ => (3, 3),
    };

    public int Nearest(int slidesPerPage) =>
        HandoutMaster.Choices.OrderBy(choice => Math.Abs(choice - slidesPerPage)).ThenBy(choice => choice).First();

    public IReadOnlyList<PageBox> Cells(int slidesPerPage)
    {
        var (columns, rows) = Grid(slidesPerPage);
        var width = (1 - 2 * Margin - Gap * (columns - 1)) / columns;
        var height = (1 - 2 * Margin - Gap * (rows - 1)) / rows;

        return
        [
            .. Enumerable.Range(0, columns * rows).Select(cell => new PageBox(
                Margin + cell % columns * (width + Gap),
                Margin + cell / columns * (height + Gap),
                width,
                height)),
        ];
    }
}
