using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Pivot;

public sealed record PivotLabels(string GrandTotal, string SubtotalFormat, string Empty = "")
{
    public string Quarter { get; init; } = "Q{1} {0}";

    public System.Globalization.CultureInfo? Culture { get; init; }

    public string SubtotalOf(string item) => string.Format(
        Culture ?? System.Globalization.CultureInfo.CurrentCulture, SubtotalFormat, item);
}

public interface IPivotRenderer
{
    CellRange Render(
        PivotTable table, PivotDefinition definition, Worksheet destination,
        StyleCatalog styles, PivotLabels labels, NumberFormatCatalog? formats = null);

    CellRange Measure(PivotTable table, PivotDefinition definition);
}

public sealed class PivotRenderer : IPivotRenderer
{
    public CellRange Render(
        PivotTable table, PivotDefinition definition, Worksheet destination,
        StyleCatalog styles, PivotLabels labels, NumberFormatCatalog? formats = null)
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

                var isItem = column.Node.Kind != PivotNodeKind.GrandTotal && level < column.Node.Path.Count;
                var text = column.Node.Kind == PivotNodeKind.GrandTotal
                    ? (level == 0 ? labels.GrandTotal : string.Empty)
                    : isItem ? _items.Display(column.Node.Path[level], FieldAt(columnFields, level), labels) : string.Empty;

                CellValue? typed = isItem && column.Node.Kind == PivotNodeKind.Item && level == column.Node.Path.Count - 1
                    ? _items.Value(column.Node.Path[level], FieldAt(columnFields, level), labels)
                    : null;

                if (columnFields.Count == 0) { text = column.Field.Label; typed = null; }
                else if (multipleValues && level == headerRows - 1)
                {
                    text = text.Length == 0 ? column.Field.Label : $"{text} · {column.Field.Label}";
                    typed = null;
                }

                var address = new CellAddress(origin.Row + level, origin.Column + rowHeaderColumns + c);

                if (typed is { } item) destination.SetValue(address, item);
                else if (text.Length > 0) destination.SetValue(address, CellValue.Text(text));
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

        var rowFields = definition.In(PivotArea.Row).ToList();
        var columnPerField = definition.Layout != PivotLayout.Compact;

        for (var r = 0; r < table.Rows.Count; r++)
        {
            var node = table.Rows[r];
            var row = origin.Row + headerRows + r;

            var label = node.Kind switch
            {
                PivotNodeKind.GrandTotal => CellValue.Text(labels.GrandTotal),
                PivotNodeKind.Subtotal => CellValue.Text(labels.SubtotalOf(_items.Display(node.Label, FieldAt(rowFields, node.Depth), labels))),
                _ => _items.Value(node.Label, FieldAt(rowFields, node.Depth), labels),
            };

            var labelColumn = columnPerField
                ? origin.Column + Math.Min(node.Depth, rowHeaderColumns - 1)
                : origin.Column;

            var indent = columnPerField ? 0 : node.Depth;

            var labelAddress = new CellAddress(row, labelColumn);
            destination.SetValue(labelAddress, label);

            if (columnPerField && definition.RepeatItemLabels && node.Kind == PivotNodeKind.Item)
                for (var level = 0; level < node.Depth && level < rowHeaderColumns - 1; level++)
                    destination.SetValue(new CellAddress(row, origin.Column + level), _items.Value(node.Path[level], FieldAt(rowFields, level), labels));

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

                var isTotal = node.Kind != PivotNodeKind.Item || table.Columns[c].Node.Kind != PivotNodeKind.Item;
                var formatId = FormatIdOf(table.Columns[c].Field, formats);
                if (formatId != 0)
                    destination.SetStyle(address, styles.Intern(CellStyle.Default with
                    {
                        Font = CellFont.Default with { Bold = isTotal },
                        NumberFormatId = formatId,
                    }));
                else if (isTotal)
                    destination.SetStyle(address, totalStyle);
            }
        }

        return Measure(table, definition);
    }

    private readonly PivotItemCells _items = new();

    private const int PercentFormatId = 10;

    private readonly IReadOnlySet<ValueDisplayMode> _percentModes = new HashSet<ValueDisplayMode>
    {
        ValueDisplayMode.PercentOfTotal, ValueDisplayMode.PercentOfRow, ValueDisplayMode.PercentOfColumn,
        ValueDisplayMode.PercentOfParent, ValueDisplayMode.PercentDifferenceFrom, ValueDisplayMode.PercentRunningTotal,
    };

    private int FormatIdOf(PivotField field, NumberFormatCatalog? formats)
    {
        if (field.NumberFormat is { Length: > 0 } code && formats is not null) return formats.Register(code);
        return _percentModes.Contains(field.DisplayMode) ? PercentFormatId : 0;
    }

    private PivotField? FieldAt(IReadOnlyList<PivotField> fields, int level) =>
        level >= 0 && level < fields.Count ? fields[level] : null;

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

internal sealed class PivotItemCells
{
    private readonly PivotDateLabels _dates = new();

    public string Display(string key, PivotField? field, PivotLabels labels) =>
        key.Length == 0 ? labels.Empty : _dates.Label(key, field, labels) ?? key;

    public CellValue Value(string key, PivotField? field, PivotLabels labels)
    {
        if (key.Length == 0) return CellValue.Text(labels.Empty);
        if (_dates.Label(key, field, labels) is { } dated) return CellValue.Text(dated);
        if (field is { DateGrouping: DateGrouping.None, NumericGroupInterval: null } && PivotValues.BooleanOf(key) is { } truth)
            return CellValue.Boolean(truth);
        if (field is { DateGrouping: DateGrouping.None, NumericGroupInterval: null }
            && double.TryParse(key, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number)
            && string.Equals(number.ToString("R", System.Globalization.CultureInfo.InvariantCulture), key, StringComparison.Ordinal))
            return CellValue.Number(number);
        return CellValue.Text(key);
    }
}

public sealed class PivotDateLabels
{
    public string? Label(string key, PivotField? field, PivotLabels labels)
    {
        if (field is null) return null;
        var culture = labels.Culture ?? System.Globalization.CultureInfo.CurrentCulture;
        return field.DateGrouping switch
        {
            DateGrouping.Quarters when key.Length == 7 && key[4] == '-' && key[5] == 'T'
                && int.TryParse(key.AsSpan(0, 4), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var year)
                => string.Format(culture, labels.Quarter, year, key[6]),
            DateGrouping.Months when DateTime.TryParseExact(key, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var month)
                => month.ToString("MMM yyyy", culture),
            DateGrouping.Days when DateTime.TryParseExact(key, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var day)
                => day.ToString("d", culture),
            _ => null,
        };
    }
}

