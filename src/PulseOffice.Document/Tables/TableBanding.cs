namespace PulseOffice.Document.Tables;

public sealed record TableBanding
{
    public static readonly TableBanding Standard = new();

    public static readonly TableBanding None = new() { BandedRows = false };

    public bool BandedRows { get; init; } = true;

    public bool BandedColumns { get; init; }

    public bool FirstColumn { get; init; }

    public bool LastColumn { get; init; }

    public bool LastRow { get; init; }

    public bool BandsRow(int bodyRow) => BandedRows && bodyRow >= 0 && bodyRow % 2 == 1;

    public bool BandsColumn(int column) => BandedColumns && column % 2 == 1;

    public bool Emphasizes(int row, int column, int rows, int columns) =>
        (FirstColumn && column == 0) || (LastColumn && column == columns - 1) || (LastRow && row == rows - 1);
}
