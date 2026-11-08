using PulseOffice.Scripting.Editor.Facades;
using PulseOffice.Shell;

namespace PulseOffice.Sheets.UI.Scripting;

public sealed class DocumentFacade(
    IScriptDispatcher dispatcher,
    IActiveSheetContext context,
    IDocumentWorkspace workspace,
    PulseOffice.Sheets.Engine.Operations.ISheetRenaming renaming,
    PulseOffice.Sheets.Engine.Operations.IWorkbookCheckpoints checkpoints,
    PulseOffice.Sheets.Engine.Operations.IDefinedNameRules nameRules,
    PulseOffice.Sheets.Engine.Parsing.IFormulaExpressionParser parser,
    IScriptProtection protection,
    IWorksheetTabPolicy tabs,
    PulseOffice.Sheets.Engine.Operations.IChartSheetReferences charts) : ScriptFacade(dispatcher)
{
    public string Name => Read(() => workspace.Active?.Title ?? string.Empty);

    public string Path => Read(() => workspace.Active?.Path ?? string.Empty);

    public bool IsModified => Read(() => workspace.Active?.IsModified ?? false);

    public int SheetCount => Read(() => context.Workbook?.Sheets.Count ?? 0);

    public string[] SheetNames => Read(() =>
        context.Workbook is { } workbook
            ? [.. workbook.Sheets.Select(sheet => sheet.Name)]
            : Array.Empty<string>());

    public string ActiveSheet => Read(() =>
        context.Workbook is { } workbook ? workbook.Sheets[context.SheetIndex].Name : string.Empty);

    public string Title
    {
        get => Read(() => context.Workbook?.Properties.Title ?? string.Empty);
        set => Change(() =>
        {
            if (context.Workbook is not { } workbook) return;
            var before = workbook.Properties.Title;
            var after = value ?? string.Empty;
            if (before == after) return;
            Recorded("Title", () => workbook.Properties.Title = after, () => workbook.Properties.Title = before);
        });
    }

    public string Author
    {
        get => Read(() => context.Workbook?.Properties.Author ?? string.Empty);
        set => Change(() =>
        {
            if (context.Workbook is not { } workbook) return;
            var before = workbook.Properties.Author;
            var after = value ?? string.Empty;
            if (before == after) return;
            Recorded("Author", () => workbook.Properties.Author = after, () => workbook.Properties.Author = before);
        });
    }

    public string Subject
    {
        get => Read(() => context.Workbook?.Properties.Subject ?? string.Empty);
        set => Change(() =>
        {
            if (context.Workbook is not { } workbook) return;
            var before = workbook.Properties.Subject;
            var after = value ?? string.Empty;
            if (before == after) return;
            Recorded("Subject", () => workbook.Properties.Subject = after, () => workbook.Properties.Subject = before);
        });
    }

    public string Keywords
    {
        get => Read(() =>
            context.Workbook is { } workbook ? string.Join(", ", workbook.Properties.Keywords) : string.Empty);
        set => Change(() =>
        {
            if (context.Workbook is not { } workbook) return;

            var before = workbook.Properties.Keywords.ToList();
            var after = (value ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (before.SequenceEqual(after)) return;

            Recorded("Keywords", () => Replace(workbook.Properties.Keywords, after), () => Replace(workbook.Properties.Keywords, before));
        });
    }

    public bool AddSheet(string name) => Write(() =>
    {
        if (context.Workbook is not { } workbook) return false;
        protection.Structure();
        if (!tabs.NameAllowed(name) || workbook.FindSheet(name.Trim()) is not null) return false;

        Checkpointed(workbook, "AddSheet", () => workbook.AddSheet(name.Trim()));
        context.RequestRefresh();

        return true;
    });

    public bool RemoveSheet(string name) => Write(() =>
    {
        if (context.Workbook is not { } workbook) return false;
        protection.Structure();
        if (workbook.Sheets.Count <= 1) return false;
        if (workbook.FindSheet(name) is not { } sheet) return false;

        var removed = false;
        var broken = charts.Removed(workbook, sheet);
        Checkpointed(workbook, "RemoveSheet", () =>
        {
            removed = workbook.RemoveSheet(sheet);
            if (removed) charts.Apply(broken, forward: true);
        });

        if (removed) context.RequestRefresh();

        return removed;
    });

    public bool RenameSheet(string name, string newName) => Write(() =>
    {
        if (context.Workbook is not { } workbook) return false;
        if (workbook.FindSheet(name) is not { } sheet) return false;
        protection.Structure();
        if (!tabs.NameAllowed(newName) || newName.Trim() == sheet.Name) return false;
        if (workbook.FindSheet(newName.Trim()) is { } clash && !ReferenceEquals(clash, sheet)) return false;
        if (context.Engine is not { } engine) return false;

        var plan = renaming.Plan(workbook, sheet, newName.Trim());
        renaming.Apply(engine, plan);

        context.History?.Push(new PulseOffice.Document.Commands.DelegateAction(
            newName, redo: () => renaming.Apply(engine, plan), undo: () => renaming.Revert(engine, plan)));

        context.RequestRefresh();

        return true;
    });

    public bool Activate(string name) => Read(() =>
    {
        if (context.Workbook is not { } workbook) return false;

        var index = workbook.Sheets
            .Select((sheet, position) => (sheet, position))
            .FirstOrDefault(pair => string.Equals(
                pair.sheet.Name, name, StringComparison.OrdinalIgnoreCase)).position;

        if (workbook.FindSheet(name) is null) return false;

        context.SelectSheet(index);

        return true;
    });

    public bool Undo() => Write(() =>
    {
        if (context.History is not { CanUndo: true } history) return false;

        history.Undo();
        context.RequestRefresh();

        return true;
    });

    public bool Redo() => Write(() =>
    {
        if (context.History is not { CanRedo: true } history) return false;

        history.Redo();
        context.RequestRefresh();

        return true;
    });

    public void Recalculate() => Change(() =>
    {
        context.Engine?.RecalculateAll();
        context.RequestRefresh();
    });

    public string[] Names => Read(() =>
        context.Workbook is { } workbook
            ? [.. workbook.DefinedNames.Select(name => name.Name)]
            : Array.Empty<string>());

    public bool DefineName(string name, string reference) => Write(() =>
    {
        if (context.Workbook is not { } workbook) return false;
        if (!nameRules.IsValid(name?.Trim() ?? string.Empty))
            throw new ArgumentException($"'{name}' is not a valid name: start with a letter or '_', no spaces, and not a cell address.", nameof(name));
        var expression = Expression(reference);

        var defined = new DefinedName(name!.Trim(), expression);
        var previous = workbook.FindName(defined.Name);
        if (previous == defined) return true;

        Recorded(
            "DefineName",
            () => workbook.DefineName(defined),
            () =>
            {
                if (previous is null) workbook.RemoveName(defined.Name);
                else workbook.DefineName(previous);
            });
        context.Engine?.RecalculateAll();

        return true;
    });

    private string Expression(string reference)
    {
        var text = reference?.Trim().TrimStart('=') ?? string.Empty;
        if (text.Length == 0) throw new ArgumentException("The reference is empty.", nameof(reference));
        try
        {
            parser.Parse("=" + text);
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            throw new ArgumentException($"'{reference}' is not a valid reference or formula: {failure.Message}", nameof(reference), failure);
        }
        return text;
    }

    private void Recorded(string label, Action redo, Action undo)
    {
        redo();
        context.History?.Push(new PulseOffice.Document.Commands.DelegateAction(label, redo: redo, undo: undo));
    }

    private void Checkpointed(Workbook workbook, string label, Action change)
    {
        if (context.Engine is not { } engine)
        {
            change();
            return;
        }

        var before = checkpoints.Capture(workbook);
        change();
        var after = checkpoints.Capture(workbook);

        context.History?.Push(new PulseOffice.Document.Commands.DelegateAction(
            label,
            redo: () => checkpoints.Restore(engine, workbook, after),
            undo: () => checkpoints.Restore(engine, workbook, before)));
    }

    private void Replace(ICollection<string> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }

    public int MacroCount => Read(() => context.Workbook?.Macros.All.Count ?? 0);
}
