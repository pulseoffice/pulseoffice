using System.Globalization;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations;

public sealed record SlideTextSource(string ElementId, RichText Text)
{
    public int Row { get; init; }

    public int Column { get; init; } = -1;

    public bool IsCell => Column >= 0;
}

public interface ISlideTextSources
{
    IReadOnlyList<SlideTextSource> Of(SlideElement element);
}

public sealed class SlideTextSources(IShapeRichText shapes) : ISlideTextSources
{
    public SlideTextSources() : this(new ShapeRichText())
    {
    }

    public IReadOnlyList<SlideTextSource> Of(SlideElement element) => element switch
    {
        TextElement text => [new SlideTextSource(text.Id, text.Text)],
        ObjectElement { Content: ShapeObject shape } when !string.IsNullOrEmpty(shape.Text) =>
            [new SlideTextSource(element.Id, shapes.ToRich(shape))],
        ObjectElement { Content: DataTableObject table } => Cells(element.Id, table),
        _ => [],
    };

    private List<SlideTextSource> Cells(string elementId, DataTableObject table)
    {
        var index = new TableCellIndex(table);
        var cells = new List<SlideTextSource>();
        var firstRow = table.Style.HeaderRow ? TableCellFormat.HeaderRow : 0;
        for (var row = firstRow; row < table.Snapshot.Rows.Count; row++)
            for (var column = 0; column < table.Snapshot.Columns.Count; column++)
            {
                if (index.Cover(row, column) is not null) continue;
                var text = TextAt(table, row, column);
                if (string.IsNullOrEmpty(text)) continue;
                cells.Add(new SlideTextSource(elementId, RichText.Empty(PlaceholderRole.Body).WithPlainText(text)) { Row = row, Column = column });
            }
        return cells;
    }

    private string TextAt(DataTableObject table, int row, int column)
    {
        if (row == TableCellFormat.HeaderRow) return table.Snapshot.Columns[column].Name;
        var values = table.Snapshot.Rows[row];
        return column < values.Count ? Convert.ToString(values[column], CultureInfo.CurrentCulture) ?? string.Empty : string.Empty;
    }
}
