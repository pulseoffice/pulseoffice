using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using PulseOffice.Shell;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI.LedgerViewParts;

internal sealed class LedgerCellWriter(
    Workbook workbook,
    CalculationEngine engine,
    PulseOffice.Document.Commands.UndoStack history,
    IStatusBarService status,
    ITextCatalog texts,
    IDocumentWorkspace workspace,
    SheetGridView grid,
    Engine.Operations.IDataValidator? validator,
    Text.Spelling.IAutoCorrect? autoCorrect,
    Text.Spelling.IAutoCorrectSettings? autoCorrectSettings,
    ISheetSpelling? spelling)
{
    public event EventHandler? SelectionStale;

    public Engine.Localization.IFormulaLocalization? Formulas { get; set; }

    public Engine.Operations.ICellEditText EditTexts { get; set; } = new Engine.Operations.CellEditText();

    public Engine.Operations.ICellInputRecognizer Inputs { get; set; } = new Engine.Operations.CellInputRecognizer();

    public ICellEntryStyle EntryStyles { get; set; } = new CellEntryStyle();

    private string FormulaLanguage => texts.Language.ToCode();

    public string EditableText(Cell cell, Worksheet sheet, CellAddress address) => cell.HasFormula
        ? Formulas?.ToDisplay(cell.Formula!, FormulaLanguage) ?? cell.Formula!
        : EditTexts.Value(cell.Value, workbook.NumberFormats.CodeOf(workbook.Styles.Get(sheet.StyleIdAt(address)).NumberFormatId));

    public void Write(string text, CellAddress address, int sheetIndex)
    {
        if (sheetIndex < 0 || sheetIndex >= workbook.Sheets.Count) return;
        var target = new SheetCell(sheetIndex, address);

        var sheet = workbook.Sheets[sheetIndex];
        var previous = sheet[address];

        if (workspace.Active?.IsReadOnly == true)
        {
            status.Report(texts["status.readOnly"], NotificationSeverity.Error, NotificationRemedies.EditMode);
            SelectionStale?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (IsLocked(sheet, address))
        {
            status.Report(texts["status.protection.locked"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            SelectionStale?.Invoke(this, EventArgs.Empty);
            return;
        }

        var content = text.StartsWith('=')
            ? previous with { Formula = Formulas?.ToCanonical(text, FormulaLanguage) ?? text, ImplicitIntersection = false }
            : Entered(previous, text, sheet, address);

        if (!WriteValidated(target, content))
        {
            SelectionStale?.Invoke(this, EventArgs.Empty);
            return;
        }

        grid.Refresh();

        var value = engine.GetValue(target);
        if (value.IsError) status.Report($"{address.ToA1()}: {value.ErrorText}");
    }

    public bool CanEdit()
    {
        if (workspace.Active?.IsReadOnly != true) return true;

        status.Report(texts["status.readOnly"], NotificationSeverity.Error, NotificationRemedies.EditMode);
        grid.Refresh();
        return false;
    }

    public bool IsLocked(Worksheet sheet, CellAddress address) =>
        sheet.Protection.IsEnabled && workbook.Styles.Get(sheet.StyleIdAt(address)).Locked;

    public bool WriteValidated(SheetCell target, Cell content)
    {
        var sheet = workbook.Sheets[target.SheetIndex];
        var previous = sheet[target.Address];

        if (!content.HasFormula && Refused(target, content.Value)) return false;

        Restore(engine, target, content);

        if (content.HasFormula && Refused(target, engine.GetValue(target)))
        {
            Restore(engine, target, previous);
            return false;
        }

        RecordWrite(target, previous, sheet[target.Address]);
        return true;
    }

    public bool Refused(SheetCell target, CellValue candidate)
    {
        if (validator is null) return false;

        var outcome = validator.Validate(engine, target.SheetIndex, target.Address, candidate);
        if (!outcome.Reported) return false;

        var message = outcome.Rule?.ErrorMessage is { Length: > 0 } custom
            ? custom
            : texts["status.validation.refused"];

        status.Report(message, NotificationSeverity.Error);

        return outcome.Blocks;
    }

    public void RecordWrite(SheetCell target, Cell previous, Cell next)
    {
        if (previous == next) return;

        var owner = engine;

        history.Push(new PulseOffice.Document.Commands.DelegateAction(
            texts.Format("history.cell.edit", target.Address.ToA1()),
            redo: () => Restore(owner, target, next),
            undo: () => Restore(owner, target, previous)));
    }

    private Cell Entered(Cell previous, string text, Worksheet sheet, CellAddress address)
    {
        var input = Inputs.Recognize(text);
        var value = input.Value.Kind == ValueKind.Text ? CellValue.Text(Corrected(text, sheet, address)) : input.Value;
        var entered = previous with { Formula = null, Value = value };
        return EntryStyles.StyleFor(workbook, sheet, address, input.FormatCode) is { } styleId
            ? entered with { StyleId = styleId }
            : entered;
    }

    private string Corrected(string text, Worksheet sheet, CellAddress address) =>
        autoCorrect is { } corrector && autoCorrectSettings is { Enabled: true } settings
            ? corrector.Apply(text, settings.Options with
            {
                Language = TextLanguage(sheet, address),
                CapitaliseFirstLetter = settings.Options.CapitaliseFirstLetter && text.Any(char.IsWhiteSpace),
            })
            : text;

    private string? TextLanguage(Worksheet sheet, CellAddress address) =>
        spelling?.LanguageOf(workbook, sheet, address).Canonical;

    private void Restore(CalculationEngine owner, SheetCell target, Cell cell)
    {
        var sheet = owner.Workbook.Sheets[target.SheetIndex];
        if (sheet[target.Address].StyleId != cell.StyleId)
            sheet[target.Address] = sheet[target.Address] with { StyleId = cell.StyleId };
        if (cell.HasFormula) owner.SetFormula(target, cell.Formula!);
        else owner.SetValue(target, cell.Value);
    }
}
