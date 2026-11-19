using System.Globalization;
using PulseOffice.Document.Embedding;
using PulseOffice.Presentations;
using PulseOffice.Printing;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI.Rendering;

public interface ISlideTextSegments
{
    PrintTextSegments? For(PrintTag container, SlideElement element);
}

public sealed class SlideTextSegments : ISlideTextSegments
{
    public PrintTextSegments? For(PrintTag container, SlideElement element)
    {
        ArgumentNullException.ThrowIfNull(container);
        return element switch
        {
            TextElement text when container.Role == PrintRole.Section => Paragraphs(container, text.Text),
            ObjectElement { Content: DataTableObject table } when container.Role == PrintRole.Table => Cells(container, table),
            _ => null,
        };
    }

    private PrintTextSegments? Paragraphs(PrintTag container, RichText text)
    {
        var segments = new List<PrintTextSegment>();
        PrintTag? list = null;
        for (var index = 0; index < text.Paragraphs.Count; index++)
        {
            var paragraph = text.Paragraphs[index];
            var key = container.Key + ":" + index.ToString(CultureInfo.InvariantCulture);
            if (!(paragraph.Bullet || paragraph.Numbered))
            {
                list = null;
                if (paragraph.Text.Trim().Length > 0) segments.Add(new PrintTextSegment(new PrintTag("p:" + key, PrintRole.Paragraph, container), paragraph.Text));
                continue;
            }
            list ??= new PrintTag("l:" + key, PrintRole.List, container);
            var item = new PrintTag("li:" + key, PrintRole.ListItem, list);
            segments.Add(new PrintTextSegment(new PrintTag("lb:" + key, PrintRole.ListBody, item), paragraph.Text)
            {
                Label = new PrintTag("lbl:" + key, PrintRole.ListLabel, item),
            });
        }
        return segments.Count == 0 ? null : new PrintTextSegments(segments, AdvanceOnMiss: false);
    }

    private PrintTextSegments? Cells(PrintTag container, DataTableObject table)
    {
        var snapshot = table.Snapshot;
        var segments = new List<PrintTextSegment>();
        var row = 0;
        if (table.Style.HeaderRow && snapshot.Columns.Count > 0)
        {
            var header = Row(container, row++);
            for (var column = 0; column < snapshot.Columns.Count; column++)
                segments.Add(new PrintTextSegment(Cell(header, column, PrintRole.TableHeader, PrintHeaderScope.Column, null), snapshot.Columns[column].Name));
        }
        for (var data = 0; data < snapshot.Rows.Count; data++)
        {
            var tr = Row(container, row++);
            var values = snapshot.Rows[data];
            for (var column = 0; column < values.Count; column++)
            {
                if (table.Merges.Any(merge => merge.Covers(data, column))) continue;
                var role = table.Style.FirstColumn && column == 0 ? PrintRole.TableHeader : PrintRole.TableCell;
                var origin = table.Merges.FirstOrDefault(span => span.IsOrigin(data, column));
                segments.Add(new PrintTextSegment(Cell(tr, column, role, PrintHeaderScope.Row, origin), Convert.ToString(values[column], CultureInfo.CurrentCulture) ?? string.Empty));
            }
        }
        return segments.Count == 0 ? null : new PrintTextSegments(segments, AdvanceOnMiss: true);
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
