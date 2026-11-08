using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerMiscCommands(
    IActiveSheetContext context,
    IDialogService dialogs,
    ITextCatalog texts) : LedgerCommandSet(context)
{
    private static readonly (string Id, string Character)[] Marks =
    [
        ("ledger.insert.nonBreakingSpace", " "),
        ("ledger.insert.narrowNonBreakingSpace", " "),
        ("ledger.insert.nonBreakingHyphen", "‑"),
        ("ledger.insert.softHyphen", "­"),
        ("ledger.insert.zeroWidthSpace", "​"),
        ("ledger.insert.wordJoiner", "⁠"),
        ("ledger.insert.leftToRightMark", "‎"),
        ("ledger.insert.rightToLeftMark", "‏"),
    ];

    public IEnumerable<CommandDefinition> GetCommands()
    {
        foreach (var (id, character) in Marks)
        {
            var mark = character;

            yield return Prototype(id) with
            {
                MenuPath = "insert/marks",
                Surface = CommandSurface.Advanced,
                RequiredState = DocumentState.DocumentOpen,
                ForbiddenState = DocumentState.ReadOnly,
                EnabledWhen = _ => Context.HasTarget,
                Execute = (_, _) =>
                {
                    Append(mark);
                    return Task.CompletedTask;
                },
            };
        }

        yield return Zoom("ledger.zoom.page", 0);
        yield return Zoom("ledger.zoom.pageWidth", 1);
        yield return Zoom("ledger.zoom.optimal", 2);

        yield return Prototype("ledger.insert.symbol") with
        {
            IconKey = "ledger.insert.symbol",
            MenuPath = "insert/marks",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, ct) => SymbolAsync(ct),
        };

        yield return Prototype("ledger.insert.fieldDate") with
        {
            MenuPath = "insert/fields",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, _) =>
            {
                Formula("=TODAY()");
                return Task.CompletedTask;
            },
        };

        yield return Prototype("ledger.insert.fieldTitle") with
        {
            MenuPath = "insert/fields",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Workbook?.Properties.Title is { Length: > 0 },
            Execute = (_, _) =>
            {
                if (Context.Workbook?.Properties.Title is { } title) Write(CellValue.Text(title));
                return Task.CompletedTask;
            },
        };
    }

    private CommandDefinition Zoom(string id, int kind) => Prototype(id) with
    {
        MenuPath = "view/zoom",
        Surface = CommandSurface.Advanced,
        RequiredState = DocumentState.DocumentOpen,
        EnabledWhen = _ => Context.HasTarget,
        Execute = (_, _) =>
        {
            Context.SetZoom(kind switch { 0 => 0.75, 1 => 1.0, _ => Optimal() });
            return Task.CompletedTask;
        },
    };

    private double Optimal()
    {
        if (Context.Workbook is null) return 1;

        var columns = Math.Max(1, Context.Selection.End.Column - Context.Selection.Start.Column + 1);

        return Math.Clamp(6.0 / columns, 0.25, 4.0);
    }

    private async Task SymbolAsync(CancellationToken ct)
    {
        var chosen = await dialogs.ShowAsync<string>(
            new SymbolPickerView(texts),
            new DialogOptions { TitleKey = "command.ledger.insert.symbol.title", PreferredWidth = 900 },
            ct).ConfigureAwait(true);

        if (string.IsNullOrEmpty(chosen)) return;

        Append(chosen);
    }

    private void Append(string text)
    {
        if (Context.Workbook is not { } workbook) return;

        var cell = workbook.Sheets[Context.SheetIndex][Context.Selection.Start];
        var current = cell.Value.Kind == ValueKind.Text && !cell.HasFormula ? cell.Value.AsText() : string.Empty;

        Write(CellValue.Text(current + text));
    }

    private void Formula(string formula)
    {
        if (Context.Engine is not { } engine) return;
        if (Context.Workbook is not { } workbook) return;

        var target = new SheetCell(Context.SheetIndex, Context.Selection.Start);
        var previous = workbook.Sheets[Context.SheetIndex][Context.Selection.Start];

        engine.SetFormula(target, formula);

        Context.History?.Push(new Document.Commands.DelegateAction(
            texts["history.insert.field"],
            redo: () =>
            {
                engine.SetFormula(target, formula);
                Context.RequestRefresh();
            },
            undo: () => Restore(engine, target, previous)));

        Context.RequestRefresh();
    }

    private void Restore(Engine.Recalculation.CalculationEngine engine, SheetCell target, Cell previous)
    {
        if (previous.HasFormula) engine.SetFormula(target, previous.Formula!);
        else engine.SetValue(target, previous.Value);
        Context.RequestRefresh();
    }

    private void Write(CellValue value)
    {
        if (Context.Engine is not { } engine) return;
        if (Context.Workbook is not { } workbook) return;

        var address = Context.Selection.Start;
        var target = new SheetCell(Context.SheetIndex, address);
        var previous = workbook.Sheets[Context.SheetIndex][address];

        engine.SetValue(target, value);

        Context.History?.Push(new Document.Commands.DelegateAction(
            texts["history.insert.symbol"],
            redo: () =>
            {
                engine.SetValue(target, value);
                Context.RequestRefresh();
            },
            undo: () => Restore(engine, target, previous)));

        Context.RequestRefresh();
    }
}
