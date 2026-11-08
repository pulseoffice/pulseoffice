using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Pivot;

public sealed record PivotLabels(string GrandTotal, string SubtotalFormat, string Empty = "")
{
    public string SubtotalOf(string item) => string.Format(
        System.Globalization.CultureInfo.CurrentCulture, SubtotalFormat, item);
}

public interface IPivotRenderer
{
    CellRange Render(
        PivotTable table, PivotDefinition definition, Worksheet destination,
        StyleCatalog styles, PivotLabels labels);

    CellRange Measure(PivotTable table, PivotDefinition definition);
}

public sealed class PivotRenderer : IPivotRenderer
{
    public CellRange Render(
        PivotTable table, PivotDefinition definition, Worksheet destination,
        StyleCatalog styles, PivotLabels labels)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(styles);
        ArgumentNullException.ThrowIfNull(labels);

        var origin = definition.Destination;
        var headerStyle = styles.Intern(CellStyle.Default with
        {
            Font = CellFont.Default with { Bold = true },
            BackgroundHex = "#EDF0F5",
        });

        var totalStyle = styles.Intern(CellStyle.Default with
        {
            Font = CellFont.Default with { Bold = true },
        });

        var columnFields = definition.In(PivotArea.Column).ToList();
        var headerRows = Math.Max(columnFields.Count, 1);

        var rowHeaderColumns = Math.Max(definition.In(PivotArea.Row).Count(), 1);
        var multipleValues = definition.In(PivotArea.Value).Count() > 1;

        for (var level = 0; level < headerRows; level++)
        {
            for (var c = 0; c < table.Columns.Count; c++)
            {
                var column = table.Columns[c];

                var text = column.Node.Kind == PivotNodeKind.GrandTotal
                    ? (level == 0 ? labels.GrandTotal : string.Empty)
                    : level < column.Node.Path.Count ? column.Node.Path[level] : string.Empty;

                if (columnFields.Count == 0) text = column.Field.Label;
                else if (multipleValues && level == headerRows - 1)
                    text = text.Length == 0 ? column.Field.Label : $"{text} · {column.Field.Label}";

                var address = new CellAddress(origin.Row + level, origin.Column + rowHeaderColumns + c);

                if (text.Length > 0) destination.SetValue(address, CellValue.Text(text));
                destination.SetStyle(address, headerStyle);
            }
        }

        for (var level = 0; level < rowHeaderColumns; level++)
        {
            var address = new CellAddress(origin.Row + headerRows - 1, origin.Column + level);
            var fields = definition.In(PivotArea.Row).ToList();

            if (level < fields.Count) destination.SetValue(address, CellValue.Text(fields[level].Label));
            destination.SetStyle(address, headerStyle);
        }

        for (var r = 0; r < table.Rows.Count; r++)
        {
            var node = table.Rows[r];
            var row = origin.Row + headerRows + r;

            var label = node.Kind switch
            {
                PivotNodeKind.GrandTotal => labels.GrandTotal,
                PivotNodeKind.Subtotal => labels.SubtotalOf(node.Label),
                _ => node.Label,
            };

            var labelColumn = definition.Layout == PivotLayout.Tabular
                ? origin.Column + Math.Min(node.Depth, rowHeaderColumns - 1)
                : origin.Column;

            var indent = definition.Layout == PivotLayout.Tabular ? 0 : node.Depth;

            var labelAddress = new CellAddress(row, labelColumn);
            destination.SetValue(labelAddress, CellValue.Text(label));

            if (node.Kind == PivotNodeKind.Item && indent > 0)
                destination.SetStyle(labelAddress, styles.Intern(CellStyle.Default with { Indent = indent }));
            else if (node.Kind != PivotNodeKind.Item)
                destination.SetStyle(labelAddress, totalStyle);

            for (var c = 0; c < table.Columns.Count; c++)
            {
                var address = new CellAddress(row, origin.Column + rowHeaderColumns + c);
                var value = table.Value(r, c);

                if (value.IsEmpty && definition.EmptyCellText is { } empty)
                    destination.SetValue(address, CellValue.Text(empty));
                else if (!value.IsEmpty)
                    destination.SetValue(address, value);

                if (node.Kind != PivotNodeKind.Item || table.Columns[c].Node.Kind != PivotNodeKind.Item)
                    destination.SetStyle(address, totalStyle);
            }
        }

        return Measure(table, definition);
    }

    public CellRange Measure(PivotTable table, PivotDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(definition);

        var origin = definition.Destination;
        var headerRows = Math.Max(definition.In(PivotArea.Column).Count(), 1);
        var rowHeaderColumns = Math.Max(definition.In(PivotArea.Row).Count(), 1);

        return new CellRange(
            origin,
            new CellAddress(
                origin.Row + headerRows + Math.Max(table.Rows.Count - 1, 0),
                origin.Column + rowHeaderColumns + Math.Max(table.Columns.Count - 1, 0)));
    }
}
