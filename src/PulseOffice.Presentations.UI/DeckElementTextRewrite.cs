using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public interface IDeckElementTextRewrite
{
    SlideElement Rewrite(SlideElement element, Func<RichText, RichText> rewrite);
}

public sealed class DeckElementTextRewrite(IShapeRichText shapes, IDeckTableKeys tables, ISlideTextSources sources) : IDeckElementTextRewrite
{
    public DeckElementTextRewrite() : this(
        new ShapeRichText(),
        new DeckTableKeys(new TableStructure(new TableSpans(), new ColumnWidths())),
        new SlideTextSources())
    {
    }

    public SlideElement Rewrite(SlideElement element, Func<RichText, RichText> rewrite)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(rewrite);
        return element switch
        {
            TextElement text => Text(text, rewrite),
            ObjectElement { Content: ShapeObject shape } when !string.IsNullOrEmpty(shape.Text) => Shape((ObjectElement)element, shape, rewrite),
            ObjectElement { Content: DataTableObject table } => Table((ObjectElement)element, table, rewrite),
            _ => element,
        };
    }

    private SlideElement Text(TextElement element, Func<RichText, RichText> rewrite)
    {
        var after = rewrite(element.Text);
        return after.PlainText == element.Text.PlainText ? element : element with { Text = after };
    }

    private SlideElement Shape(ObjectElement element, ShapeObject shape, Func<RichText, RichText> rewrite)
    {
        var before = shapes.ToRich(shape);
        var after = rewrite(before);
        return after.PlainText == before.PlainText ? element : element with { Content = shapes.FromRich(shape, after) };
    }

    private SlideElement Table(ObjectElement element, DataTableObject table, Func<RichText, RichText> rewrite)
    {
        var updated = table;
        foreach (var cell in sources.Of(element))
        {
            if (!cell.IsCell) continue;
            var after = rewrite(cell.Text).PlainText;
            if (after == cell.Text.PlainText) continue;
            updated = tables.Write(updated, new TableCellRange(cell.Row, cell.Column, cell.Row, cell.Column), after);
        }
        return ReferenceEquals(updated, table) ? element : element with { Content = updated };
    }
}
