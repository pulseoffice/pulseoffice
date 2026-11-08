using System.Globalization;
using PulseOffice.Document.Tables;
using PulseOffice.Document.Borders;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing.Tables;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Borders;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Icons;

namespace PulseOffice.Documents.UI.Commands;

public sealed record CellSplit(int Parts, bool Horizontally);

public sealed record RowHeightChoice(double? Points, bool Minimum);

public sealed record TablePropertiesChoice(
    string? Name,
    int HeaderRows,
    bool Repeat,
    BorderFrame BordersBefore,
    BorderFrame BordersAfter,
    bool ResetBorders,
    bool ChangeBackground,
    uint? Background,
    CellVerticalAlign Align)
{
    public TableAlignment Placement { get; init; }

    public PulseOffice.Foundation.Units.Twip Indent { get; init; }

    public CellTextDirection Direction { get; init; }
}

public interface IFolioTableDialogs
{
    Task<CellSplit?> SplitAsync(CancellationToken ct);

    Task<(int Count, bool After)?> InsertAsync(bool rows, CancellationToken ct);

    Task<bool?> SplitTableAsync(bool hasHeading, CancellationToken ct);

    Task<RowHeightChoice?> RowHeightAsync(TableRow row, CancellationToken ct);

    Task<double?> ColumnWidthAsync(double current, CancellationToken ct);

    Task<IReadOnlyList<TableSortKey>?> SortAsync(TableBlock table, TableCellRange range, CancellationToken ct);

    Task<string?> FormulaAsync(string? current, CancellationToken ct);

    Task<TextTableOptions?> TextToTableAsync(IReadOnlyList<string> sample, CancellationToken ct);

    Task<TextTableOptions?> TableToTextAsync(CancellationToken ct);

    Task<TablePropertiesChoice?> PropertiesAsync(TableSelection selection, TableCell cell, double textWidth, CancellationToken ct);
}

public sealed class FolioTableDialogs(
    IFolioForms forms, ITabbedForms tabbed, ITextCatalog texts, ITableGrid grid, ITableBorderFraming framing, IBorderEditors borders,
    IDialogService dialogs, ITableTextSampler sampler, PulseOffice.UI.Controls.Sorting.ISortLevelsDialog sortLevels, IIconCatalog? icons = null) : IFolioTableDialogs
{
    private readonly ColorFieldValue _colors = new();
    private readonly CellVerticalAlign[] _aligns = [CellVerticalAlign.Top, CellVerticalAlign.Middle, CellVerticalAlign.Bottom];

    private readonly CellTextDirection[] _directions = [CellTextDirection.Horizontal, CellTextDirection.TopToBottom, CellTextDirection.BottomToTop];
    private readonly TableAlignment[] _placements = [TableAlignment.Left, TableAlignment.Center, TableAlignment.Right];
    private readonly TableTextSeparator[] _separators = [TableTextSeparator.Tab, TableTextSeparator.Semicolon, TableTextSeparator.Paragraph, TableTextSeparator.Custom];

    public async Task<CellSplit?> SplitAsync(CancellationToken ct)
    {
        FormField[] fields =
        [
            new NumberField("parts", texts["folio.table.split.parts"], 2, 2, 32) { Integer = true },
            new ChoiceField("direction", texts["folio.table.split.direction"], [texts["folio.table.split.horizontal"], texts["folio.table.split.vertical"]], 1),
        ];
        if (await PreviewedAsync("command.folio.table.split.title", "folio.table.split.explanation", fields, SplitRows, _ => false, ct).ConfigureAwait(true) is not { } values) return null;
        return new CellSplit((int)Math.Round(values.Number("parts", 2)), values.Choice("direction", 2) == 0);
    }

    public async Task<(int Count, bool After)?> InsertAsync(bool rows, CancellationToken ct)
    {
        var kind = rows ? "rows" : "columns";
        FormField[] fields =
        [
            new NumberField("count", texts["folio.table.insert.count"], 1, 1, 99) { Integer = true },
            new ChoiceField("position", texts["folio.table.insert.position"], [texts[$"folio.table.insert.{kind}.before"], texts[$"folio.table.insert.{kind}.after"]], 1),
        ];
        if (await forms.AskAsync($"command.folio.table.insert{(rows ? "Rows" : "Columns")}.title", "folio.table.insert.explanation", fields, ct).ConfigureAwait(true) is not { } values) return null;
        return ((int)Math.Round(values.Number("count", 1)), values.Choice("position", 2) == 1);
    }

    public async Task<bool?> SplitTableAsync(bool hasHeading, CancellationToken ct)
    {
        FormField[] fields = [new CheckField("heading", texts["folio.table.splitTable.heading"], hasHeading)];
        return await forms.AskAsync("command.folio.table.splitTable.title", "folio.table.splitTable.explanation", fields, ct).ConfigureAwait(true) is { } values
            ? values.Flag("heading")
            : null;
    }

    public async Task<RowHeightChoice?> RowHeightAsync(TableRow row, CancellationToken ct)
    {
        FormField[] fields =
        [
            new NumberField("height", texts["folio.table.rowHeight.height"], row.Height?.ToPoints() ?? 18, 2, 1000, 0.5) { Unit = "pt" },
            new CheckField("fit", texts["folio.table.rowHeight.fit"], row.Height is null || row.MinimumHeight),
        ];
        if (await forms.AskAsync("command.folio.table.rowHeight.title", "folio.table.rowHeight.explanation", fields, ct).ConfigureAwait(true) is not { } values) return null;
        return new RowHeightChoice(values.Number("height", 18), values.Flag("fit"));
    }

    public async Task<double?> ColumnWidthAsync(double current, CancellationToken ct)
    {
        FormField[] fields = [new NumberField("width", texts["folio.table.columnWidth.width"], Math.Round(current, 1), 14, 2000, 0.5) { Unit = "pt" }];
        return await forms.AskAsync("command.folio.table.columnWidth.title", "folio.table.columnWidth.explanation", fields, ct).ConfigureAwait(true) is { } values
            ? values.Number("width", current)
            : null;
    }

    public async Task<IReadOnlyList<TableSortKey>?> SortAsync(TableBlock table, TableCellRange range, CancellationToken ct)
    {
        var columns = Enumerable.Range(range.Left, range.ColumnCount).Select(column => Column(table, column)).ToList();
        var letters = Enumerable.Range(range.Left, range.ColumnCount).Select(Letters).ToList();
        var choice = await sortLevels.AskAsync(new PulseOffice.UI.Controls.Sorting.SortLevelsRequest("command.folio.table.sort.title", columns, letters)
        {
            OffersHeaderChoice = false,
            OffersCase = false,
            ExplanationKey = "folio.table.sort.explanation",
            Initial = [new PulseOffice.UI.Controls.Sorting.SortLevel(0)],
        }, ct).ConfigureAwait(true);
        if (choice is null) return null;
        return [.. choice.Levels.Select(level => new TableSortKey(range.Left + level.Column, level.Descending,
            Numeric(table, range, range.Left + level.Column) ? TableSortKind.Numeric : TableSortKind.Alphanumeric))];
    }

    public async Task<string?> FormulaAsync(string? current, CancellationToken ct)
    {
        FormField[] fields = [new TextField("formula", texts["folio.table.formula.field"], current is null ? "=SUM(ABOVE)" : "=" + current, "=SUM(A1:A3)")];
        return await forms.AskAsync("command.folio.table.formula.title", "folio.table.formula.explanation", fields, ct, 520).ConfigureAwait(true) is { } values
            ? values.Text("formula")
            : null;
    }

    public async Task<TextTableOptions?> TextToTableAsync(IReadOnlyList<string> sample, CancellationToken ct)
    {
        FormField[] fields =
        [
            new ChoiceField("separator", texts["folio.table.convert.separator"], [.. _separators.Select(Separator)], 0),
            new TextField("custom", texts["folio.table.convert.custom"], ",") { ShownWhen = current => current.Choice("separator", _separators.Length) == _separators.Length - 1 },
            new CheckField("heading", texts["folio.table.convert.heading"], false),
        ];
        IReadOnlyList<IReadOnlyList<string>> Rows(IReadOnlyDictionary<string, object> current) =>
            sampler.Split(sample, _separators[current.Choice("separator", _separators.Length)], Custom(current.Text("custom")), 5);
        if (await PreviewedAsync("command.folio.table.textToTable.title", "folio.table.textToTable.explanation", fields, Rows, current => current.Flag("heading"), ct).ConfigureAwait(true) is not { } values) return null;
        return new TextTableOptions(_separators[values.Choice("separator", _separators.Length)], Custom(values.Text("custom")), values.Flag("heading"));
    }

    public async Task<TextTableOptions?> TableToTextAsync(CancellationToken ct)
    {
        FormField[] fields =
        [
            new ChoiceField("separator", texts["folio.table.convert.separator"], [.. _separators.Select(Separator)], 0),
            new TextField("custom", texts["folio.table.convert.custom"], ","),
        ];
        if (await forms.AskAsync("command.folio.table.toText.title", "folio.table.toText.explanation", fields, ct).ConfigureAwait(true) is not { } values) return null;
        return new TextTableOptions(_separators[values.Choice("separator", _separators.Length)], Custom(values.Text("custom")));
    }

    public async Task<TablePropertiesChoice?> PropertiesAsync(TableSelection selection, TableCell cell, double textWidth, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var table = selection.Table;
        var frame = framing.FrameOf(selection);
        FormTab[] tabs =
        [
            new(texts["folio.table.properties.table"], texts["folio.table.properties.table.explanation"],
            [
                new TextField("name", texts["folio.table.properties.name"], table.Name ?? string.Empty),
                new NumberField("header", texts["folio.table.properties.headerRows"], table.HeaderRows, 0, table.Rows.Count) { Integer = true },
                new CheckField("repeat", texts["command.folio.table.headingRepeat.title"], table.RepeatHeaderRows),
                new IconChoiceField("placement", texts["folio.table.properties.placement"],
                    [.. _placements.Select(placement => new IconChoice(placement.ToString(), $"folio.table.place{placement}", texts[$"folio.table.properties.place.{placement.ToString().ToLowerInvariant()}"]))], icons,
                    Math.Max(0, Array.IndexOf(_placements, table.Alignment))) { ShowLabels = true, Columns = 1 },
                new NumberField("indent", texts["folio.table.properties.indent"], Math.Round(table.Indent.ToCentimeters(), 2), -10, 50, 0.1) { Unit = "cm", ShownWhen = current => Placement(current) == TableAlignment.Left },
            ])
            {
                LivePreview = feed => PlacementPreview(feed, table, textWidth),
            },
            new(texts["folio.table.properties.borders"], texts["folio.table.properties.borders.explanation"],
            [
                new BorderEditorField("borders", texts["folio.table.properties.borders"], borders, framing.Scope(selection), frame),
                new CheckField("reset", texts["folio.table.properties.resetBorders"], false),
            ]),
            new(texts["folio.table.properties.background"], texts["folio.table.properties.background.explanation"],
            [
                new CheckField("change", texts["folio.table.properties.changeBackground"], false),
                new ColorField("fill", texts["folio.table.properties.fill"], _colors.Hex(cell.Background)) { ShownWhen = current => current.Flag("change") },
                new IconChoiceField("align", texts["folio.table.properties.align"],
                    [.. _aligns.Select(align => new IconChoice(align.ToString(), $"folio.table.align{align}", texts[$"command.folio.table.align{align}.title"]))], icons,
                    Math.Max(0, Array.IndexOf(_aligns, cell.VerticalAlign))) { ShowLabels = true },
                new IconChoiceField("direction", texts["folio.table.properties.direction"],
                    [.. _directions.Select(direction => new IconChoice(direction.ToString(), $"folio.table.direction{direction}", texts[$"folio.table.properties.direction.{direction.ToString().ToLowerInvariant()}"]))], icons,
                    Math.Max(0, Array.IndexOf(_directions, cell.TextDirection))) { ShowLabels = true },
            ]),
        ];
        if (await tabbed.AskAsync("command.folio.table.properties.title", tabs, ct).ConfigureAwait(true) is not { } values) return null;
        return new TablePropertiesChoice(
            values.Text("name"),
            (int)Math.Round(values.Number("header", table.HeaderRows)),
            values.Flag("repeat"),
            frame,
            values.TryGetValue("borders", out var chosen) && chosen is BorderFrame after ? after : frame,
            values.Flag("reset"),
            values.Flag("change"),
            _colors.Argb(values.Text("fill")),
            _aligns[values.Choice("align", _aligns.Length)])
        {
            Placement = Placement(values),
            Indent = PulseOffice.Foundation.Units.Twip.FromCentimeters(values.Number("indent", 0)),
            Direction = _directions[values.Choice("direction", _directions.Length)],
        };
    }

    private TableAlignment Placement(IReadOnlyDictionary<string, object> values) =>
        values.ContainsKey("placement") ? _placements[values.Choice("placement", _placements.Length)] : TableAlignment.Left;

    private TablePlacementPreview PlacementPreview(IFormValuesFeed feed, TableBlock table, double textWidth)
    {
        var preview = new TablePlacementPreview();
        var tableWidth = table.Columns.Sum(column => column.ToPoints());
        void Refresh()
        {
            var values = feed.Current;
            var indent = values.ContainsKey("indent") ? values.Number("indent", 0) : table.Indent.ToCentimeters();
            preview.Sketch = new TablePlacementSketch(textWidth, tableWidth > 0 ? tableWidth : textWidth, Math.Max(1, table.ColumnCount),
                values.ContainsKey("placement") ? Placement(values) : table.Alignment, PulseOffice.Foundation.Units.Twip.FromCentimeters(indent).ToPoints());
        }
        Refresh();
        feed.Changed += (_, _) => Refresh();
        return preview;
    }

    private IReadOnlyList<IReadOnlyList<string>> SplitRows(IReadOnlyDictionary<string, object> values)
    {
        var parts = Math.Clamp((int)Math.Round(values.Number("parts", 2)), 2, 32);
        return values.Choice("direction", 2) == 0
            ? [.. Enumerable.Range(0, parts).Select(_ => (IReadOnlyList<string>)[string.Empty])]
            : [[.. Enumerable.Repeat(string.Empty, parts)]];
    }

    private async Task<IReadOnlyDictionary<string, object>?> PreviewedAsync(string titleKey, string explanationKey, IReadOnlyList<FormField> fields,
        Func<IReadOnlyDictionary<string, object>, IReadOnlyList<IReadOnlyList<string>>> rows, Func<IReadOnlyDictionary<string, object>, bool> heading, CancellationToken ct)
    {
        var view = new FormPromptView(texts[explanationKey], fields, texts);
        var preview = new TableGridPreview();
        void Refresh()
        {
            var values = view.Values;
            preview.Rows = rows(values);
            preview.HeadingRow = heading(values);
        }
        Refresh();
        view.ValuesChanged += (_, _) => Refresh();
        view.Preview = preview;
        return await dialogs.ShowAsync<Dictionary<string, object>>(view, new DialogOptions { TitleKey = titleKey }, ct).ConfigureAwait(true);
    }

    private string Separator(TableTextSeparator separator) => texts[$"folio.table.convert.{separator.ToString().ToLowerInvariant()}"];

    private char Custom(string text) => string.IsNullOrEmpty(text) ? ',' : text[0];

    private string Column(TableBlock table, int column)
    {
        var letter = Letters(column);
        var heading = table.HeaderRows > 0 && !table.Cell(0, column).Covered ? grid.PlainText(table.Cell(0, column)).Trim() : string.Empty;
        var label = string.Format(CultureInfo.CurrentCulture, texts["folio.table.sort.column"], letter);
        return heading.Length == 0 ? label : $"{label} — {heading}";
    }

    private string Letters(int column)
    {
        var letters = new System.Text.StringBuilder();
        for (var value = column + 1; value > 0; value = (value - 1) / 26) letters.Insert(0, (char)('A' + (value - 1) % 26));
        return letters.ToString();
    }

    private bool Numeric(TableBlock table, TableCellRange range, int column) =>
        Enumerable.Range(range.Top, range.RowCount).Select(row => table.Rows[row].Cells[column]).Where(cell => !cell.Covered)
            .Select(cell => grid.PlainText(cell).Trim()).Where(text => text.Length > 0)
            .All(text => double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out _));
}
