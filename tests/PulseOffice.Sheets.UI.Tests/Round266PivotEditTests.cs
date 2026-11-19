using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round266PivotEditTests
{
    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection().AddSheetsEngine().BuildServiceProvider()
            .GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    private static readonly PulseOffice.Localization.ITextCatalog Texts =
        new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);

    private static readonly PivotEngine PivotEngine = new(new PivotAggregator(), new PivotGrouping(),
        new PivotFormulas(new Engine.Parsing.FormulaParser()));

    private sealed class AnsweringDialogs(Func<Control, object?> answer) : IDialogService
    {
        public bool IsDialogOpen => false;

        public Control? Shown { get; private set; }

        public Task<TResult?> ShowAsync<TResult>(Control content, DialogOptions options, CancellationToken ct = default)
        {
            Shown = content;
            return Task.FromResult((TResult?)answer(content));
        }

        public Task ShowAsync(Control content, DialogOptions options, CancellationToken ct = default) => Task.CompletedTask;

        public void Close(object? result)
        {
        }
    }

    private static PivotField Row => new() { SourceName = "Name", SourceColumnIndex = 0, Area = PivotArea.Row, SortDescending = true };

    private static PivotField Value => new()
    {
        SourceName = "Amount", SourceColumnIndex = 1, Area = PivotArea.Value,
        DisplayMode = ValueDisplayMode.PercentOfTotal, NumberFormat = "0.0%",
    };

    private static (Workbook Workbook, UndoStack History, ActiveSheetContext Context) Scene()
    {
        var workbook = Workbook.CreateDefault();
        var engine = Engines.Create(workbook);
        var data = workbook.Sheets[0];
        data.SetValue(new CellAddress(0, 0), CellValue.Text("Name"));
        data.SetValue(new CellAddress(0, 1), CellValue.Text("Amount"));
        data.SetValue(new CellAddress(1, 0), CellValue.Text("a"));
        data.SetValue(new CellAddress(1, 1), CellValue.Number(5));
        data.SetValue(new CellAddress(2, 0), CellValue.Text("b"));
        data.SetValue(new CellAddress(2, 1), CellValue.Number(3));
        var coordinator = new PivotCoordinator(PivotEngine, new PivotRenderer());
        Assert.True(coordinator.Apply(workbook, new PivotDefinition
        {
            Id = "p", Name = "Pivot1", SourceSheet = data.Name, SourceRange = new CellRange(new(0, 0), new(2, 1)),
            DestinationSheet = "Pivot1", Destination = CellAddress.First, Fields = [Row, Value],
        }, new PivotLabels("Grand total", "{0} total")).Succeeded);

        var history = new UndoStack();
        var context = new ActiveSheetContext();
        var pivotSheet = workbook.Sheets.ToList().FindIndex(sheet => sheet.Name == "Pivot1");
        context.Attach(new SheetTarget
        {
            Workbook = workbook, SheetIndex = pivotSheet, History = history, Engine = engine,
            Selection = () => new CellRange(new CellAddress(1, 0), new CellAddress(1, 0)),
            WriteSelection = _ => { }, ReadViewOption = _ => false, WriteViewOption = (_, _) => { }, Refresh = () => { },
            FreezePanes = (_, _) => { }, SplitPanes = (_, _) => { }, ReadPanesSplit = () => false, ApplyRowFilter = (_, _) => { },
            ReadZoom = () => 1, WriteZoom = _ => { }, ReadSelectedObject = () => null, ReadSelectedObjects = () => [],
            WriteSelectedObjects = _ => { }, SelectSheet = _ => { }, WriteSelectedObject = _ => { },
        });
        return (workbook, history, context);
    }

    private static CommandDefinition Edit(ActiveSheetContext context, IDialogService dialogs) =>
        new LedgerPivotCommands(context, new PivotCoordinator(PivotEngine, new PivotRenderer()), dialogs,
                new StatusBarService(TimeProvider.System, new PulseOffice.UI.Controls.Notifications.NotificationCenter(TimeProvider.System),
                    new PulseOffice.UI.Controls.Notifications.NotificationHistory(TimeProvider.System)),
                Texts, new Engine.Operations.WorkbookCheckpoints(new Engine.Operations.SheetSnapshots()),
                new DataDialogs.LedgerDialogKit(Texts, System.Reflection.DispatchProxy.Create<PulseOffice.UI.Icons.IIconCatalog, LedgerCommandsAuditTests.Inert>(),
                    new PulseOffice.UI.Controls.Dialogs.HelpTextSplitter()),
                PivotEngine, new WorksheetTabPolicy())
            .GetCommands().Single(command => command.Id.Value == "ledger.pivot.edit");

    [Fact]
    public void The_layout_model_seeded_from_a_pivot_keeps_every_field_setting()
    {
        var model = new PivotLayoutModel(["Name", "Amount"], new PivotDefinition
        {
            Id = "p", Name = "p", SourceSheet = "s", SourceRange = default, DestinationSheet = "d", Destination = CellAddress.First,
            Fields = [Row, Value, new PivotField { SourceName = "Calc", SourceColumnIndex = -1, Area = PivotArea.Value, Formula = "=Amount*2" }],
            Layout = PivotLayout.Tabular, RepeatItemLabels = true,
        });

        var fields = model.Fields();

        Assert.Equal([0], model.In(PivotArea.Row));
        Assert.True(fields.Single(field => field.SourceName == "Name").SortDescending);
        Assert.Equal("0.0%", fields.Single(field => field.SourceName == "Amount").NumberFormat);
        Assert.Equal(ValueDisplayMode.PercentOfTotal, fields.Single(field => field.SourceName == "Amount").DisplayMode);
        Assert.Equal(1, fields.Single(field => field.IsCalculated).AreaOrder);
        Assert.Equal(PivotLayout.Tabular, model.Options.Layout);
        Assert.True(model.Options.RepeatLabels);
    }

    [Fact]
    public void Configuring_a_field_survives_moving_it_to_another_area()
    {
        var model = new PivotLayoutModel(["Date", "Amount"]);
        model.Place(0, PivotArea.Row);
        model.Configure(0, field => field with { DateGrouping = DateGrouping.Months });

        model.Place(0, PivotArea.Column);

        Assert.Equal(DateGrouping.Months, model.Fields().Single().DateGrouping);
        Assert.Equal(PivotArea.Column, model.Fields().Single().Area);
    }

    [Fact]
    public async Task Editing_a_pivot_applies_the_new_settings_in_one_undo_step()
    {
        var (workbook, history, context) = Scene();
        var dialogs = new AnsweringDialogs(_ => new PivotChoice(
            [Row with { SortDescending = false }, Value with { DisplayMode = ValueDisplayMode.Normal, NumberFormat = null }],
            "ignored", new PivotTableOptions(PivotLayout.Outline, false, true, true)));
        var edit = Edit(context, dialogs);

        Assert.True(edit.EnabledWhen!(null!));
        await edit.Execute(null!, CancellationToken.None);

        var pivot = workbook.Pivots.Single();
        Assert.Equal("Pivot1", pivot.DestinationSheet);
        Assert.Equal(PivotLayout.Outline, pivot.Layout);
        Assert.False(pivot.ShowGrandTotalRows);
        Assert.True(pivot.RepeatItemLabels);
        Assert.Equal(ValueDisplayMode.Normal, pivot.In(PivotArea.Value).Single().DisplayMode);
        Assert.IsType<PivotPromptView>(dialogs.Shown);
        Assert.True(((PivotPromptView)dialogs.Shown!).IsEditing);

        history.Undo();

        Assert.False(history.CanUndo);
        Assert.Equal(ValueDisplayMode.PercentOfTotal, workbook.Pivots.Single().In(PivotArea.Value).Single().DisplayMode);
        Assert.Equal(PivotLayout.Compact, workbook.Pivots.Single().Layout);
    }

    [Fact]
    public async Task A_cancelled_edit_adds_no_undo_step()
    {
        var (_, history, context) = Scene();

        await Edit(context, new AnsweringDialogs(_ => null)).Execute(null!, CancellationToken.None);

        Assert.False(history.CanUndo);
    }

    [Fact]
    public void Reordering_the_value_fields_keeps_sorting_and_top_filters_on_the_same_measure()
    {
        var model = new PivotLayoutModel(["Name", "Amount", "Cost"]);
        model.Place(0, PivotArea.Row);
        model.Place(1, PivotArea.Value);
        model.Place(2, PivotArea.Value);
        model.Configure(0, field => field with { SortByValue = 1, TopFilter = new PivotTopFilter(3, 1) });

        model.Place(2, PivotArea.Value, 0);

        var row = model.Fields().Single(field => field.Area == PivotArea.Row);
        Assert.Equal(0, row.SortByValue);
        Assert.Equal(0, row.TopFilter!.ValueField);

        model.Place(2, PivotArea.Unused);

        row = model.Fields().Single(field => field.Area == PivotArea.Row);
        Assert.Null(row.SortByValue);
        Assert.Null(row.TopFilter);
    }
}
