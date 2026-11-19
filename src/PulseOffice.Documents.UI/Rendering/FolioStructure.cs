using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Objects;
using PulseOffice.Printing;
using LayoutGlyphRun = PulseOffice.Documents.Layout.Tree.GlyphRun;

namespace PulseOffice.Documents.UI.Rendering;

public sealed record FolioStructure(
    IReadOnlyDictionary<string, PrintTag> Paragraphs,
    IReadOnlyDictionary<string, PrintTag> Labels,
    IReadOnlyDictionary<(string Table, int Row, int Column), PrintTag> Cells,
    IReadOnlyDictionary<string, PrintTag> Figures)
{
    public static readonly FolioStructure Empty = new(
        new Dictionary<string, PrintTag>(), new Dictionary<string, PrintTag>(),
        new Dictionary<(string, int, int), PrintTag>(), new Dictionary<string, PrintTag>());
}

public interface IFolioStructureIndex
{
    FolioStructure Index(TextDocument document);

    ILayoutVisitor Tagger(IPageCanvas canvas, FolioStructure structure);
}

public sealed class FolioStructureIndex(IDocumentOutline outline) : IFolioStructureIndex
{
    public FolioStructure Index(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var headings = outline.Headings(document).ToDictionary(entry => entry.Paragraph.Id, entry => entry.Level, StringComparer.Ordinal);
        var builder = new Builder(headings);
        builder.Walk(document.Body, null);
        foreach (var item in document.Objects.All)
        {
            if (item.Content is EmbeddedContent embedded)
            {
                var value = embedded.Value;
                builder.Figures[item.Id] = new PrintTag("fig:" + item.Id, PrintRole.Figure)
                {
                    Alternate = FirstText(value.Description, value.Title, item.Name, value.Name),
                };
            }
            if (item.Content.Story is { } story) builder.Walk(story, null);
        }
        return new FolioStructure(builder.Paragraphs, builder.Labels, builder.Cells, builder.Figures);
    }

    public ILayoutVisitor Tagger(IPageCanvas canvas, FolioStructure structure) => new StructureTagger(canvas, structure);

    private string? FirstText(params string?[] candidates) => candidates.FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

    private sealed class Builder(IReadOnlyDictionary<string, int> headings)
    {
        public Dictionary<string, PrintTag> Paragraphs { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, PrintTag> Labels { get; } = new(StringComparer.Ordinal);

        public Dictionary<(string Table, int Row, int Column), PrintTag> Cells { get; } = new();

        public Dictionary<string, PrintTag> Figures { get; } = new(StringComparer.Ordinal);

        public void Walk(BlockContainer container, PrintTag? parent)
        {
            PrintTag? list = null;
            string? identity = null;
            foreach (var block in container.Blocks)
            {
                switch (block)
                {
                    case ParagraphBlock paragraph when !headings.ContainsKey(paragraph.Id) && ListIdentity(paragraph) is { } current:
                        if (list is null || !string.Equals(identity, current, StringComparison.Ordinal))
                            list = new PrintTag("l:" + paragraph.Id, PrintRole.List, parent);
                        identity = current;
                        var item = new PrintTag("li:" + paragraph.Id, PrintRole.ListItem, list);
                        Labels[paragraph.Id] = new PrintTag("lbl:" + paragraph.Id, PrintRole.ListLabel, item);
                        Paragraphs[paragraph.Id] = new PrintTag("lb:" + paragraph.Id, PrintRole.ListBody, item);
                        break;
                    case ParagraphBlock paragraph:
                        list = null;
                        Paragraphs[paragraph.Id] = headings.TryGetValue(paragraph.Id, out var level)
                            ? new PrintTag("h:" + paragraph.Id, PrintRole.Heading, parent) { Level = level }
                            : new PrintTag("p:" + paragraph.Id, PrintRole.Paragraph, parent);
                        break;
                    case SectionBlock section:
                        list = null;
                        Walk(section.Content, parent);
                        break;
                    case TableBlock table:
                        list = null;
                        Table(table, parent);
                        break;
                }
            }
        }

        private void Table(TableBlock table, PrintTag? parent)
        {
            var tag = new PrintTag("t:" + table.Id, PrintRole.Table, parent);
            for (var row = 0; row < table.Rows.Count; row++)
            {
                var tr = new PrintTag($"tr:{table.Id}:{row}", PrintRole.TableRow, tag);
                for (var column = 0; column < table.Rows[row].Cells.Count; column++)
                {
                    var cell = table.Rows[row].Cells[column];
                    if (cell.Covered) continue;
                    var cellTag = new PrintTag($"c:{table.Id}:{row}:{column}", row < table.HeaderRows ? PrintRole.TableHeader : PrintRole.TableCell, tr);
                    Cells[(table.Id, row, column)] = cellTag;
                    Walk(cell.Content, cellTag);
                }
            }
        }

        private string? ListIdentity(ParagraphBlock paragraph) =>
            paragraph.Content is { ListSuppressed: false, ListStyleName: { Length: > 0 } style } content ? content.ListId ?? style : null;
    }

    private sealed class StructureTagger(IPageCanvas canvas, FolioStructure structure) : ILayoutVisitor
    {
        private PrintTag? _line;

        public void VisitPage(PageBox page) => canvas.Tag(null);

        public void VisitLine(LineBox line)
        {
            _line = line.Story switch
            {
                StoryKind.Body or StoryKind.Cell or StoryKind.Frame => structure.Paragraphs.GetValueOrDefault(line.ParagraphId),
                StoryKind.Footnote or StoryKind.Endnote => new PrintTag("note:" + (line.StoryId ?? line.ParagraphId), PrintRole.Note),
                _ => null,
            };
            canvas.Tag(_line);
        }

        public void VisitRun(LayoutGlyphRun run, LineBox line)
        {
            if (_line is null) return;
            if (run.Kind == GlyphRunKind.Label && structure.Labels.TryGetValue(line.ParagraphId, out var label)) canvas.Tag(label);
            else if (run.Style?.Hyperlink is { Length: > 0 } target)
                canvas.Tag(new PrintTag("a:" + _line.Key + ":" + target, PrintRole.Link, _line));
            else canvas.Tag(_line);
        }

        public void VisitTable(TableBox table) => canvas.Tag(null);

        public void VisitCell(CellBox cell)
        {
            if (structure.Cells.TryGetValue((cell.TableId, cell.Row, cell.Column), out var tag)) canvas.Tag(tag);
            canvas.Tag(null);
        }

        public void VisitObject(ObjectBox item) => canvas.Tag(structure.Figures.GetValueOrDefault(item.ObjectId));

        public void VisitDecoration(DecorationBox decoration) => canvas.Tag(null);

        public void VisitWatermark(WatermarkBox watermark) => canvas.Tag(null);
    }
}

public sealed class LayoutVisitorPair(ILayoutVisitor first, ILayoutVisitor second) : ILayoutVisitor
{
    public void VisitPage(PageBox page)
    {
        first.VisitPage(page);
        second.VisitPage(page);
    }

    public void VisitLine(LineBox line)
    {
        first.VisitLine(line);
        second.VisitLine(line);
    }

    public void VisitRun(LayoutGlyphRun run, LineBox line)
    {
        first.VisitRun(run, line);
        second.VisitRun(run, line);
    }

    public void VisitArea(AreaBox area)
    {
        first.VisitArea(area);
        second.VisitArea(area);
    }

    public void VisitTable(TableBox table)
    {
        first.VisitTable(table);
        second.VisitTable(table);
    }

    public void VisitCell(CellBox cell)
    {
        first.VisitCell(cell);
        second.VisitCell(cell);
    }

    public void VisitObject(ObjectBox item)
    {
        first.VisitObject(item);
        second.VisitObject(item);
    }

    public void LeaveObject(ObjectBox item)
    {
        second.LeaveObject(item);
        first.LeaveObject(item);
    }

    public void VisitTurn(TurnedBox turn)
    {
        first.VisitTurn(turn);
        second.VisitTurn(turn);
    }

    public void LeaveTurn(TurnedBox turn)
    {
        second.LeaveTurn(turn);
        first.LeaveTurn(turn);
    }

    public void VisitDecoration(DecorationBox decoration)
    {
        first.VisitDecoration(decoration);
        second.VisitDecoration(decoration);
    }

    public void VisitWatermark(WatermarkBox watermark)
    {
        first.VisitWatermark(watermark);
        second.VisitWatermark(watermark);
    }
}
