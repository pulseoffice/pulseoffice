using PulseOffice.Sheets.Formatting;
using PulseOffice.Localization;
using PulseOffice.Foundation.Modules;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Shell;
using PulseOffice.UI.Controls.Services;

using PulseOffice.UI.Controls.Forms;
using PulseOffice.UI.Controls.Drawing;

namespace PulseOffice.Sheets.UI;

public interface ILedgerDocumentFactory
{
    OpenDocument CreateEmpty();

    OpenDocument CreateFrom(Workbook workbook, string title);

    OpenDocument? CreateSecondView(OpenDocument document);
}

public sealed class LedgerDocumentFactory(
    ICalculationEngineFactory engines,
    ICellPresenter presenter,
    PulseOffice.Sheets.Engine.Operations.ConditionalFormattingEvaluator conditional,
    IActiveSheetContext sheetContext,
    IDocumentWorkspace workspace,
    IStatusBarService status,
    ITextCatalog texts,
    LedgerCommands commands,
    LedgerSheetCommands sheets,
    LedgerFilterCommands filterCommands,
    PulseOffice.Sheets.Engine.Operations.IDataValidator validator,
    IWebResourceAccess web,
    Pivot.IPivotEngine pivots,
    IContextMenuFactory contextMenus,
    IFormDesignMode design,
    IDrawingToolState tools,
    IFormEventRunner formEvents,
    ISheetSpelling spelling,
    Text.Spelling.IAutoCorrect autoCorrect,
    Text.Spelling.IAutoCorrectSettings autoCorrectSettings,
    PulseOffice.Sheets.Engine.Operations.ISheetFill fill,
    PulseOffice.Document.Forms.IOptionGroups optionGroups,
    INameBoxResolver nameBox,
    PulseOffice.Sheets.Engine.Operations.IReferenceCycler references,
    PulseOffice.UI.Controls.Services.IRibbonStateNotifier ribbonState,
    Formatting.IRowAutoHeight rowHeights,
    Engine.Localization.IFormulaLocalization formulas,
    Engine.Operations.ICellInputRecognizer inputs,
    PulseOffice.UI.Controls.Links.ILinkFollower links,
    IFormControlBinding formBinding,
    IInterfaceCulture culture,
    PulseOffice.Sheets.Engine.Operations.IRangeFilterEvaluator rangeFilters) : ILedgerDocumentFactory
{
    private int _counter;

    private bool Editable => workspace.Active?.IsReadOnly != true;

    private Pivot.PivotLabels PivotLabelsFor() =>
        new(texts["pivot.grandTotal"], texts["pivot.subtotal"]);

    public OpenDocument CreateEmpty() =>
        Build(Languaged(Workbook.CreateDefault(texts["sheet.defaultName"])), $"{ModuleIdentity.Sheets.BrandName}{++_counter}", recalculate: false);

    public OpenDocument CreateFrom(Workbook workbook, string title) =>
        Build(Languaged(workbook), System.IO.Path.GetFileNameWithoutExtension(title), recalculate: true);

    private Workbook Languaged(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        if (workbook.TextLanguage is null) workbook.DefaultLanguage = culture.Culture.Name;
        return workbook;
    }

    public OpenDocument? CreateSecondView(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Model is not Workbook workbook) return null;
        if (document.View is not LedgerView existing) return null;

        var view = CreateView(workbook, existing.Engine, document.History);

        return document.CreateWindow(view, document.Title + " : 2");
    }

    private OpenDocument Build(Workbook workbook, string title, bool recalculate)
    {
        var engine = engines.Create(workbook);

        engine.Web = web;
        engine.Pivots = new Pivot.PivotDataLookup(workbook, pivots, PivotLabelsFor());

        var history = new PulseOffice.Document.Commands.UndoStack();

        if (recalculate)
        {
            engine.RecalculateAll();
            rangeFilters.Restore(workbook, new PulseOffice.Sheets.Engine.Operations.EngineShiftedFormulas(engine));
        }

        var view = CreateView(workbook, engine, history);

        return new OpenDocument(ModuleKind.Sheets, title, view, history)
        {
            Model = workbook,
        };
    }

    private LedgerView CreateView(Workbook workbook, CalculationEngine engine, PulseOffice.Document.Commands.UndoStack history)
    {
        var view = new LedgerView(
            workbook, engine, presenter, conditional, sheetContext, workspace, history, status, texts,
            validator, contextMenus, design, tools, spelling, autoCorrect, autoCorrectSettings, formBinding: formBinding)
        {
            FormEvents = formEvents,
            Fill = fill,
            OptionGroups = optionGroups,
            NameBox = nameBox,
            References = references,
            RibbonState = ribbonState,
            RowHeights = rowHeights,
            Formulas = formulas,
            Inputs = inputs,
            Links = links,
        };

        view.ObjectEditRequested += (_, _) =>
        {
            if (Editable) commands.EditSelectedChart();
        };
        view.SheetRenameRequested += (_, _) =>
        {
            if (Editable) _ = sheets.RenameAsync();
        };
        view.FilterMenuRequested += (_, column) =>
        {
            if (Editable) _ = filterCommands.ShowMenuAsync(column);
        };
        return view;
    }
}
