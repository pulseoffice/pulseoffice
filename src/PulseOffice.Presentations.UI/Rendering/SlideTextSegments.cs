using System.Globalization;
using PulseOffice.Document.Embedding;
using PulseOffice.Presentations;
using PulseOffice.Printing;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI.Rendering;

public interface ISlideTextSegments
{
    PrintTextSegments? For(PrintTag container, SlideElement element, SlideElementPlacement? placement = null);
}

public sealed class SlideTextSegments(TableGeometry geometry, PulseOffice.Drawing.Shapes.IShapeRichText shapeText) : ISlideTextSegments
{
    public SlideTextSegments() : this(new TableGeometry(), new PulseOffice.Drawing.Shapes.ShapeRichText())
    {
    }

    public PrintTextSegments? For(PrintTag container, SlideElement element, SlideElementPlacement? placement = null)
    {
        ArgumentNullException.ThrowIfNull(container);
        return element switch
        {
            TextElement text when container.Role == PrintRole.Section => Paragraphs(container, text.Text, placement),
            ObjectElement { Content: DataTableObject table } when container.Role == PrintRole.Table => Cells(container, table, placement),
            ObjectElement { Content: PulseOffice.Drawing.Shapes.ShapeObject shape } when container.Role == PrintRole.Section => Paragraphs(container, shapeText.ToRich(shape), placement),
            _ => null,
        };
    }

    private PrintTextSegments? Paragraphs(PrintTag container, RichText text, SlideElementPlacement? placement)
    {
        var segments = new List<PrintTextSegment>();
        var tags = new Dictionary<int, SlideParagraphTags>();
        PrintTag? list = null;
        for (var index = 0; index < text.Paragraphs.Count; index++)
        {
            var paragraph = text.Paragraphs[index];
            var key = container.Key + ":" + index.ToString(CultureInfo.InvariantCulture);
            if (!(paragraph.Bullet || paragraph.Numbered))
            {
                list = null;
                if (paragraph.Text.Trim().Length == 0) continue;
                var plain = new PrintTag("p:" + key, PrintRole.Paragraph, container);
                tags[index] = new SlideParagraphTags(plain, null);
                segments.Add(new PrintTextSegment(plain, paragraph.Text));
                continue;
            }
            list ??= new PrintTag("l:" + key, PrintRole.List, container);
            var item = new PrintTag("li:" + key, PrintRole.ListItem, list);
            var body = new PrintTag("lb:" + key, PrintRole.ListBody, item);
            var label = new PrintTag("lbl:" + key, PrintRole.ListLabel, item);
            tags[index] = new SlideParagraphTags(body, label);
            segments.Add(new PrintTextSegment(body, paragraph.Text) { Label = label });
        }
        if (segments.Count == 0) return null;
        return new PrintTextSegments(segments, AdvanceOnMiss: false)
        {
            Locator = placement is { Lines.Count: > 0 } placed ? new SlideParagraphLocator(placed, tags) : null,
        };
    }

    private PrintTextSegments? Cells(PrintTag container, DataTableObject table, SlideElementPlacement? placement)
    {
        var snapshot = table.Snapshot;
        var index = new TableCellIndex(table);
        var segments = new List<PrintTextSegment>();
        var cells = new Dictionary<(int Row, int Column), PrintTag>();
        var row = 0;
        if (table.Style.HeaderRow && snapshot.Columns.Count > 0)
        {
            var header = Row(container, row++);
            for (var column = 0; column < snapshot.Columns.Count; column++)
            {
                if (index.Cover(TableCellFormat.HeaderRow, column) is not null) continue;
                var scope = table.Style.FirstColumn && column == 0 ? PrintHeaderScope.Both : PrintHeaderScope.Column;
                var tag = Cell(header, column, PrintRole.TableHeader, scope, index.Origin(TableCellFormat.HeaderRow, column));
                cells[(TableCellFormat.HeaderRow, column)] = tag;
                segments.Add(new PrintTextSegment(tag, snapshot.Columns[column].Name));
            }
        }
        for (var data = 0; data < snapshot.Rows.Count; data++)
        {
            var tr = Row(container, row++);
            var values = snapshot.Rows[data];
            for (var column = 0; column < values.Count; column++)
            {
                if (index.Cover(data, column) is not null) continue;
                var role = table.Style.FirstColumn && column == 0 ? PrintRole.TableHeader : PrintRole.TableCell;
                var tag = Cell(tr, column, role, PrintHeaderScope.Row, index.Origin(data, column));
                cells[(data, column)] = tag;
                segments.Add(new PrintTextSegment(tag, Convert.ToString(values[column], CultureInfo.CurrentCulture) ?? string.Empty));
            }
        }
        if (segments.Count == 0) return null;
        return new PrintTextSegments(segments, AdvanceOnMiss: true)
        {
            Locator = placement is null ? null : new SlideTableCellLocator(table, placement, cells, geometry),
        };
    }

    private PrintTag Row(PrintTag table, int row) =>
        new(table.Key + ":r" + row.ToString(CultureInfo.InvariantCulture), PrintRole.TableRow, table);

    private PrintTag Cell(PrintTag row, int column, PrintRole role, PrintHeaderScope scope, TableMerge? merge) =>
        new(row.Key + ":c" + column.ToString(CultureInfo.InvariantCulture), role, row)
        {
            Scope = scope,
            ColumnSpan = Math.Max(1, merge?.ColumnSpan ?? 1),
            RowSpan = Math.Max(1, merge?.RowSpan ?? 1),
        };
}
