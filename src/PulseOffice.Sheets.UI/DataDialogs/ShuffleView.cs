using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.Sheets.Addressing;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Dialogs;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed record ShuffleChoice(string Area);

public sealed record ShufflePreview(IReadOnlyList<IReadOnlyList<SketchCell>> Before, IReadOnlyList<IReadOnlyList<SketchCell>> After, int Cells);

public interface IShufflePreviewer
{
    ShufflePreview? Preview(string area);
}

public sealed class ShuffleView : LedgerDialogView
{
    private readonly IShufflePreviewer _previewer;
    private readonly TextBlock _summary;

    public ShuffleView(LedgerDialogKit kit, IShufflePreviewer previewer, string area, IReferenceSource ranges)
        : base(kit, "shuffle.explanation")
    {
        _previewer = previewer ?? throw new ArgumentNullException(nameof(previewer));
        Area = new RangeFieldControl(new RangeField("area", kit["shuffle.range"], area, ranges), kit.Texts, kit.Metrics);
        Before = new CellGridSketch(kit, 52);
        After = new CellGridSketch(kit, 52);
        _summary = kit.Note();
        Layout.Preview = PreviewPane("shuffle.preview", 230,
            kit.Note(kit["shuffle.before"]), Before, kit.Note(kit["shuffle.after"]), After, _summary);
        var where = Layout.AddSection((string?)null);
        where.Add(kit["shuffle.range"], Area);
        Finish("shuffle.confirm", where);
        Area.TextChanged += (_, _) => Refresh();
        Refresh();
    }

    public RangeFieldControl Area { get; }

    public CellGridSketch Before { get; }

    public CellGridSketch After { get; }

    public ShuffleChoice Choice => new(Area.Text);

    protected override object? Result() => Choice;

    private void Refresh()
    {
        var preview = Area.IsValid ? _previewer.Preview(Area.Text) : null;
        Before.Show(preview?.Before ?? []);
        After.Show(preview?.After ?? []);
        _summary.Text = preview is null ? string.Empty : Kit.Texts.Format("shuffle.summary", preview.Cells);
        Report(preview is null ? Kit["status.analysis.badReference"] : preview.Cells < 2 ? Kit["shuffle.tooSmall"] : null);
    }
}

public sealed class SheetShufflePreviewer(
    ISheetRandomFill random, Dialogs.ISheetReferenceReader reader, ISketchCells cells, Workbook workbook, int sheetIndex) : IShufflePreviewer
{
    private const int PreviewSeed = 7;

    public ShufflePreview? Preview(string area)
    {
        if (reader.ReadLocal(workbook, area ?? string.Empty, sheetIndex, Dialogs.ReferenceShape.Range) is not { } range) return null;
        var sheet = workbook.Sheets[sheetIndex];
        var shown = new CellRange(range.Start, new CellAddress(
            Math.Min(range.End.Row, range.Start.Row + SketchCells.Rows - 1),
            Math.Min(range.End.Column, range.Start.Column + SketchCells.Columns - 1)));
        var after = random.Shuffle(sheet, shown, PreviewSeed).ToDictionary(entry => entry.Address, entry => entry.Value);
        var before = cells.Read(sheet, shown, false);
        var moved = new List<IReadOnlyList<SketchCell>>();
        for (var row = shown.Start.Row; row <= shown.End.Row; row++)
        {
            var line = new List<SketchCell>();
            for (var column = shown.Start.Column; column <= shown.End.Column; column++)
            {
                var address = new CellAddress(row, column);
                var value = after.TryGetValue(address, out var placed) ? placed : sheet[address].Value;
                line.Add(cells.Cell(value, marked: !value.Equals(sheet[address].Value)));
            }

            moved.Add(line);
        }

        return new ShufflePreview(before, moved, (int)Math.Min(int.MaxValue, range.CellCount));
    }
}
