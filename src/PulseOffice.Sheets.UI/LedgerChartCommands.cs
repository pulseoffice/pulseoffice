using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Foundation.Modules;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using PulseOffice.Sheets.Styling;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.Drawing.Assets;
using PulseOffice.Platform;
using PulseOffice.UI.Controls.Search;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerChartCommands(
    ISheetFormattingService formatting,
    IActiveSheetContext context,
    IStatusBarService status,
    ISheetImageInsertion insertion,
    PulseOffice.UI.Controls.Charts.IChartPicker charts,
    PulseOffice.UI.Controls.Charts.IChartEditor chartEditor,
    IChartSettingsMapper chartSettings,
    IChartDataExtractor chartData,
    Dialogs.ISheetReferenceSources referenceSources,
    ITextCatalog texts,
    IChartSourceLayout? sourceLayout = null,
    PulseOffice.Sheets.Formatting.ISpreadsheetLocale? locale = null) : LedgerCommandSet(context)
{
    private readonly IChartSourceLayout _sourceLayout = sourceLayout ?? new ChartSourceLayout();
    private readonly PulseOffice.Sheets.Formatting.ISpreadsheetLocale _locale = locale ?? new PulseOffice.Sheets.Formatting.SystemSpreadsheetLocale();
    private readonly ChartSourceReferences _references = new();

    public IEnumerable<CommandDefinition> GetCommands() => ChartCommands();

    private IEnumerable<CommandDefinition> ChartCommands() =>
    [
        Chart("ledger.chart.column", ChartType.Column),
        Chart("ledger.chart.bar", ChartType.Bar),
        Chart("ledger.chart.line", ChartType.Line),
        Chart("ledger.chart.area", ChartType.Area),
        Chart("ledger.chart.pie", ChartType.Pie),
        Chart("ledger.chart.doughnut", ChartType.Doughnut),

        Prototype("ledger.chart.gallery") with
        {
            IconKey = "ledger.chart.gallery",
            MenuPath = "insert/chart",
            Surface = CommandSurface.Common,
            RequiredSelection = SelectionKind.CellRange,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => formatting.CanFormat,
            Execute = (_, ct) => InsertFromChartGalleryAsync(ct),
        },

        Prototype("ledger.chart.edit") with
        {
            MenuPath = "insert/chart",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectSelected,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.SelectedObject?.Content is ChartDefinition,
            Execute = (_, ct) => EditChartAsync(ct),
        },

        Prototype("ledger.chart.delete") with
        {
            MenuPath = "insert/chart",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Workbook is { } workbook
                && workbook.Sheets[Context.SheetIndex].Objects
                    .Any(placed => placed.Content is ChartDefinition && Context.Selection.Contains(placed.Anchor)),
            Execute = (_, _) =>
            {
                if (Context.Workbook is not { } workbook) return Task.CompletedTask;

                var sheet = workbook.Sheets[Context.SheetIndex];
                var selection = Context.Selection;
                var removed = sheet.Objects.Where(o => o.Content is ChartDefinition && selection.Contains(o.Anchor)).ToList();

                if (removed.Count == 0) return Task.CompletedTask;
                if (sheet.Protection.IsEnabled)
                {
                    status.Report(texts["status.protection.objects"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
                    return Task.CompletedTask;
                }

                var description = texts["command.ledger.chart.delete.title"];
                using var transaction = Context.History?.BeginTransaction(description);

                foreach (var placed in removed)
                {
                    sheet.Objects.Remove(placed);

                    Context.History?.Push(new Document.Commands.DelegateAction(
                        description,
                        redo: () => sheet.Objects.Remove(placed),
                        undo: () => sheet.Objects.Add(placed)));
                }

                Context.RequestRefresh();
                return Task.CompletedTask;
            },
        },
    ];

    private CommandDefinition Chart(string id, ChartType type) =>
    Prototype(id) with
    {
        MenuPath = "insert/chart",
        Surface = CommandSurface.Common,
        RequiredState = DocumentState.DocumentOpen,
        RequiredSelection = SelectionKind.CellRange,
        ForbiddenState = DocumentState.ReadOnly,
        EnabledWhen = _ => formatting.CanFormat,
        Execute = (_, _) =>
        {
            InsertChart(type, texts[$"command.{id}.title"]);
            return Task.CompletedTask;
        },
    };

    private bool HasHeaderRow(Worksheet sheet, CellRange range)
    {
        if (range.RowCount < 2) return false;

        var cells = Enumerable.Range(range.Start.Column, range.ColumnCount)
            .Select(column => sheet[new CellAddress(range.Start.Row, column)].Value)
            .ToList();
        return cells.All(value => value.IsEmpty || value.Kind == ValueKind.Text)
            && cells.Any(value => value.Kind == ValueKind.Text);
    }

    private void InsertChart(ChartType type, string title)
    {
        if (Draft(type, null) is { } draft) InsertChart(draft with { ChartTitle = title });
    }

    private ChartDefinition? Draft(ChartType type, bool? firstRowIsHeader)
    {
        if (Context.Workbook is not { } workbook) return null;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var range = Context.Selection;

        return new ChartDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = type,
            SourceSheet = sheet.Name,
            SourceRange = range.ToString(),
            Legend = range.ColumnCount > 2 ? LegendPosition.Right : LegendPosition.None,
            FirstRowIsHeader = firstRowIsHeader ?? HasHeaderRow(sheet, range),
        };
    }

    private void InsertChart(ChartDefinition definition)
    {
        var range = Context.Selection;

        insertion.Insert(new PlacedObject
        {
            Id = definition.Id,
            Content = definition,
            Anchor = new CellAddress(range.Start.Row, Math.Min(range.End.Column + 2, CellAddress.MaxColumns - 1)),
        }, texts.Format("history.chart.insert", definition.ChartTitle ?? texts[$"chart.type.{Lower(definition.Type.ToString())}"]));
    }

    public void EditSelectedChart() => _ = EditChartAsync(CancellationToken.None);

    private async Task InsertFromChartGalleryAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        if (Draft(ChartType.Column, null) is not { } draft) return;

        var choice = await charts.ChooseAsync(new PulseOffice.UI.Controls.Charts.ChartPickerRequest
        {
            Settings = chartSettings.From(draft) with { SourceRange = null },
            Culture = _locale.Culture,
            Data = settings => chartData.Extract(workbook, chartSettings.Apply(draft, settings)),
        }, ct).ConfigureAwait(true);

        if (choice is null) return;

        InsertChart(chartSettings.Apply(draft, choice));
    }

    private async Task EditChartAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.SelectedObject is not { Content: ChartDefinition chart } placed) return;
        if (RefusesObjects(workbook.Sheets[Context.SheetIndex])) return;

        var updated = await chartEditor.EditAsync(chart, new PulseOffice.UI.Controls.Charts.ChartEditSource
        {
            Culture = _locale.Culture,
            Data = definition => chartData.Extract(workbook, definition),
            RangeValidator = text => CellRange.TryParseA1(text, out _),
            RangePicker = referenceSources.Picker(Dialogs.ReferenceShape.Range),
            SeriesRanges = new PulseOffice.UI.Controls.Charts.ChartSeriesRangeEditing(
                settings => _sourceLayout.Expand(chartSettings.Apply(chart, settings with { SeriesSources = null })),
                text => SeriesReference(workbook, chart, text),
                referenceSources.Picker(Dialogs.ReferenceShape.Range)),
        }, ct).ConfigureAwait(true);

        if (updated is null) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var index = sheet.Objects.IndexOf(placed);
        if (index < 0 || RefusesObjects(sheet)) return;

        var replacement = placed with { Content = updated };
        var previous = placed;

        void Apply(PlacedObject value)
        {
            sheet.Objects[index] = value;
            Context.SelectObject(value);
            Context.RequestRefresh();
        }

        Apply(replacement);

        Context.History?.Push(new Document.Commands.DelegateAction(
            texts["command.ledger.chart.edit.title"],
            redo: () => Apply(replacement),
            undo: () => Apply(previous)));

        status.Report(texts["status.chart.updated"]);
    }

    private string? SeriesReference(Workbook workbook, ChartDefinition chart, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var split = new SheetReferenceText().Split(text);
        var sheet = split.Sheet ?? (string.IsNullOrEmpty(chart.SourceSheet) ? workbook.Sheets[Context.SheetIndex].Name : chart.SourceSheet);
        return workbook.FindSheet(sheet) is { } found && CellRange.TryParseA1(split.Local, out var range)
            ? _references.Write(found.Name, range)
            : null;
    }

    private bool RefusesObjects(Worksheet sheet)
    {
        if (!sheet.Protection.IsEnabled) return false;
        status.Report(texts["status.protection.objects"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
        return true;
    }

    private static string Lower(string name) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{char.ToLowerInvariant(name[0])}{name[1..]}");
}
