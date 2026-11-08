using System.Runtime.CompilerServices;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Layout.Queries;
using PulseOffice.Documents.Layout.Tree;

namespace PulseOffice.Documents.UI.Editing;

public sealed record PlacedLine(int Page, LineBox Line);

public sealed record LineScope(IReadOnlySet<string> Paragraphs, bool AcrossPages, StoryKind? SharedKind)
{
    public bool Admits(LineBox line) => Paragraphs.Contains(line.ParagraphId) || SharedKind is { } kind && line.Story == kind;
}

public interface ILayoutLines
{
    PlacedLine? LineOf(DocumentLayout layout, DocumentPosition position);

    PlacedLine? Below(DocumentLayout layout, PlacedLine from, double x, LineScope scope);

    PlacedLine? Above(DocumentLayout layout, PlacedLine from, double x, LineScope scope);

    double TurnOf(DocumentLayout layout, PlacedLine line);
}

public interface ILineCells
{
    CellBox? CellOf(PageBox page, LineBox line);

    TurnedBox? TurnOf(PageBox page, LineBox line);
}

public sealed class LineCells : ILineCells
{
    private readonly ConditionalWeakTable<PageBox, Dictionary<LineBox, (CellBox Cell, TurnedBox? Turn)>> _cells = [];

    public CellBox? CellOf(PageBox page, LineBox line) =>
        _cells.GetValue(page, Build).TryGetValue(line, out var placed) ? placed.Cell : null;

    public TurnedBox? TurnOf(PageBox page, LineBox line) =>
        _cells.GetValue(page, Build).TryGetValue(line, out var placed) ? placed.Turn : null;

    private Dictionary<LineBox, (CellBox, TurnedBox?)> Build(PageBox page)
    {
        var map = new Dictionary<LineBox, (CellBox, TurnedBox?)>(ReferenceEqualityComparer.Instance);
        foreach (var table in page.Tables) Add(map, table);
        return map;
    }

    private void Add(Dictionary<LineBox, (CellBox, TurnedBox?)> map, TableBox table)
    {
        foreach (var cell in table.Cells)
        {
            foreach (var line in cell.Content.OfType<LineBox>()) map[line] = (cell, null);
            foreach (var turn in cell.Content.OfType<TurnedBox>())
                foreach (var line in turn.Lines) map[line] = (cell, turn);
            foreach (var nested in cell.Content.OfType<TableBox>()) Add(map, nested);
        }
    }
}

public sealed class LayoutLines(ILayoutQueries queries, ILineCells cells) : ILayoutLines
{
    private const double Epsilon = 0.01;

    public PlacedLine? LineOf(DocumentLayout layout, DocumentPosition position)
    {
        if (queries.Caret(layout, position) is not { } caret) return null;
        var line = layout.Pages[caret.Page].AllLines
            .Where(candidate => candidate.ParagraphId == position.ParagraphId)
            .OrderBy(candidate => Math.Abs(candidate.Top - caret.Y))
            .FirstOrDefault();
        return line is null ? null : new PlacedLine(caret.Page, line);
    }

    public PlacedLine? Below(DocumentLayout layout, PlacedLine from, double x, LineScope scope) => Neighbour(layout, from, x, scope, down: true);

    public PlacedLine? Above(DocumentLayout layout, PlacedLine from, double x, LineScope scope) => Neighbour(layout, from, x, scope, down: false);

    public double TurnOf(DocumentLayout layout, PlacedLine line) => cells.TurnOf(layout.Pages[line.Page], line.Line)?.RotationDegrees ?? 0;

    private PlacedLine? Neighbour(DocumentLayout layout, PlacedLine from, double x, LineScope scope, bool down)
    {
        var step = down ? 1 : -1;
        var home = cells.CellOf(layout.Pages[from.Page], from.Line);
        var turn = cells.TurnOf(layout.Pages[from.Page], from.Line);
        if (turn is not null) return Lying(from, turn, x, down);
        var last = scope.AcrossPages ? layout.Pages.Count - 1 : from.Page;
        var first = scope.AcrossPages ? 0 : from.Page;
        for (var page = from.Page; page >= first && page <= last; page += step)
        {
            var box = layout.Pages[page];
            var lines = box.AllLines.Where(line => SameFlow(line.Story, from.Line.Story) && scope.Admits(line) && cells.TurnOf(box, line) is null);
            if (page == from.Page)
                lines = down ? lines.Where(line => line.Top > from.Line.Top + Epsilon) : lines.Where(line => line.Top < from.Line.Top - Epsilon);
            var pool = lines.Where(line => Reachable(home, cells.CellOf(box, line))).ToList();
            if (pool.Count == 0) continue;
            if (home is not null && page == from.Page)
            {
                var inside = pool.Where(line => ReferenceEquals(cells.CellOf(box, line), home)).ToList();
                if (inside.Count > 0) pool = inside;
            }
            var row = down ? pool.Min(line => line.Top) : pool.Max(line => line.Top);
            var chosen = pool.Where(line => Math.Abs(line.Top - row) <= 2)
                .OrderBy(line => x < line.Left ? line.Left - x : x > line.Left + line.Line.Width ? x - line.Left - line.Line.Width : 0)
                .First();
            return new PlacedLine(page, chosen);
        }
        return null;
    }

    private PlacedLine? Lying(PlacedLine from, TurnedBox turn, double x, bool down)
    {
        var pool = turn.Lines.Where(line => down ? line.Top > from.Line.Top + Epsilon : line.Top < from.Line.Top - Epsilon).ToList();
        if (pool.Count == 0) return null;
        var row = down ? pool.Min(line => line.Top) : pool.Max(line => line.Top);
        var chosen = pool.Where(line => Math.Abs(line.Top - row) <= 2)
            .OrderBy(line => x < line.Left ? line.Left - x : x > line.Left + line.Line.Width ? x - line.Left - line.Line.Width : 0)
            .First();
        return new PlacedLine(from.Page, chosen);
    }

    private bool SameFlow(StoryKind first, StoryKind second) => first == second || Body(first) && Body(second);

    private bool Body(StoryKind kind) => kind is StoryKind.Body or StoryKind.Cell;

    private bool Reachable(CellBox? home, CellBox? cell)
    {
        if (home is null || cell is null) return true;
        if (ReferenceEquals(home, cell)) return true;
        if (cell.TableId != home.TableId) return true;
        if (cell.Row == home.Row) return false;
        return cell.Bounds.X < home.Bounds.Right - Epsilon && home.Bounds.X < cell.Bounds.Right - Epsilon;
    }
}
