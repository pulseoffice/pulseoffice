using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Styling;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerSheetCommands(
    IActiveSheetContext context,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts,
    PulseOffice.Sheets.Engine.Operations.ISheetDuplicator duplicator,
    PulseOffice.Sheets.Engine.Operations.ISheetRenaming renaming,
    PulseOffice.Sheets.Engine.Operations.IChartSheetReferences charts,
    IWorksheetTabPolicy tabs,
    DataDialogs.LedgerDialogKit kit,
    PulseOffice.UI.Commands.IRibbonPaletteBuilder palettes) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Sheet("ledger.worksheet.insert", workbook =>
        {
            var index = Context.SheetIndex + 1;
            var sheet = new Worksheet(workbook.UniqueSheetName(texts["sheet.defaultName"]));

            return new SheetChange(
                texts["history.sheet.insert"],
                () => workbook.InsertSheet(sheet, index),
                () => workbook.RemoveSheet(sheet),
                index);
        }),

        Sheet("ledger.worksheet.duplicate", workbook =>
        {
            var source = workbook.Sheets[Context.SheetIndex];
            var index = Context.SheetIndex + 1;

            var copy = duplicator.Duplicate(workbook, source, workbook.UniqueSheetName(source.Name));

            return new SheetChange(
                texts["history.sheet.duplicate"],
                () => duplicator.Insert(workbook, copy, index),
                () => duplicator.Remove(workbook, copy),
                index);
        }),

        Sheet("ledger.worksheet.delete", workbook =>
        {
            var index = Context.SheetIndex;

            if (!tabs.CanDelete(workbook, index))
            {
                status.Report(texts[workbook.Sheets.Count <= 1 ? "status.sheet.lastOne" : "status.sheet.lastVisible"],
                    NotificationSeverity.Warning);
                return null;
            }

            var sheet = workbook.Sheets[index];
            var select = tabs.VisibleAfterRemoval(workbook, index);
            var broken = charts.Removed(workbook, sheet);

            return new SheetChange(
                texts["history.sheet.delete"],
                () =>
                {
                    workbook.RemoveSheet(sheet);
                    charts.Apply(broken, forward: true);
                },
                () =>
                {
                    workbook.InsertSheet(sheet, index);
                    charts.Apply(broken, forward: false);
                },
                select);
        }, _ => Context.Workbook is { } workbook && tabs.CanDelete(workbook, Context.SheetIndex)),

        Sheet("ledger.worksheet.moveLeft", workbook => Move(workbook, -1),
            _ => Context.SheetIndex > 0, "sheet/order"),

        Sheet("ledger.worksheet.moveRight", workbook => Move(workbook, 1),
            _ => Context.Workbook is { } workbook && Context.SheetIndex < workbook.Sheets.Count - 1,
            "sheet/order"),

        Prototype("ledger.worksheet.protect") with
        {
            IconKey = "ledger.worksheet.protect",
            MenuPath = "sheet/worksheet",
            ContextPaths = ["sheet/appearance"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            CheckedWhen = _ => Context.Workbook is { } workbook && workbook.Sheets[Context.SheetIndex].Protection.IsEnabled,
            OutsideHistory = true,
            Execute = (_, ct) => ProtectAsync(ct),
        },

        Sheet("ledger.worksheet.hide", "sheet/appearance", workbook =>
        {
            if (!tabs.CanHide(workbook, Context.SheetIndex))
            {
                status.Report(texts["status.sheet.lastVisible"], NotificationSeverity.Warning);
                return null;
            }

            var sheet = workbook.Sheets[Context.SheetIndex];

            return new SheetChange(
                texts["history.sheet.hide"],
                () => sheet.IsHidden = true,
                () => sheet.IsHidden = false,
                tabs.VisibleNear(workbook, Context.SheetIndex));
        }, _ => Context.Workbook is { } workbook && tabs.CanHide(workbook, Context.SheetIndex)),

        Prototype("ledger.worksheet.rename") with
        {
            IconKey = "ledger.worksheet.rename",
            MenuPath = "sheet/worksheet",
            ContextPaths = ["sheet/manage"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => !StructureLocked,
            Execute = (_, ct) => RenameAsync(ct),
        },

        Prototype("ledger.worksheet.colour") with
        {
            IconKey = "ledger.worksheet.colour",
            MenuPath = "sheet/worksheet",
            ContextPaths = ["sheet/appearance"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => !StructureLocked,
            Execute = (_, ct) => ColourAsync(ct),
        },

        Prototype("ledger.worksheet.show") with
        {
            IconKey = "ledger.worksheet.show",
            MenuPath = "sheet/worksheet",
            ContextPaths = ["sheet/appearance"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => !StructureLocked && Context.Workbook is { } workbook
                && workbook.Sheets.Any(sheet => sheet.IsHidden),
            Execute = (_, ct) => ShowHiddenAsync(ct),
        },
    ];

    private const string ContextPath = "sheet/manage";

    private sealed record SheetChange(string Description, Action Apply, Action Undo, int SelectIndex);

    private CommandDefinition Sheet(
        string id, Func<Workbook, SheetChange?> change,
        Func<CommandContext, bool>? condition = null, string path = ContextPath) =>
        Sheet(id, path, change, condition);

    private CommandDefinition Sheet(
        string id, string path, Func<Workbook, SheetChange?> change,
        Func<CommandContext, bool>? condition = null) =>
    Prototype(id) with
    {
        IconKey = id,
        MenuPath = "sheet/worksheet",
        ContextPaths = [path],
        Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
        RequiredState = DocumentState.DocumentOpen,
        ForbiddenState = DocumentState.ReadOnly,
        EnabledWhen = context => !StructureLocked && (condition?.Invoke(context) ?? true),
        Execute = (_, _) =>
        {
            Apply(change);
            return Task.CompletedTask;
        },
    };

    private SheetChange? Move(Workbook workbook, int delta)
    {
        var from = Context.SheetIndex;
        var to = from + delta;

        if (to < 0 || to >= workbook.Sheets.Count) return null;

        return new SheetChange(
            texts["history.sheet.move"],
            () => workbook.MoveSheet(from, to),
            () => workbook.MoveSheet(to, from),
            to);
    }

    private void Apply(Func<Workbook, SheetChange?> change)
    {
        if (Context.Workbook is not { } workbook) return;
        if (RefuseStructureChange(status, texts)) return;
        if (change(workbook) is not { } outcome) return;

        outcome.Apply();

        Context.History?.Push(new DelegateAction(
            outcome.Description, redo: outcome.Apply, undo: outcome.Undo));

        Context.SelectSheet(outcome.SelectIndex);
        Context.RequestRefresh();
    }

    public Task RenameAsync() => RenameAsync(CancellationToken.None);

    private async Task RenameAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        if (RefuseStructureChange(status, texts)) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var previous = sheet.Name;

        string? Problem(string candidate)
        {
            var trimmed = candidate.Trim();
            if (trimmed.Length == 0) return texts["prompt.rule.required"];
            if (trimmed.IndexOfAny([.. tabs.ForbiddenNameCharacters]) is >= 0 and var at) return texts.Format("prompt.rule.forbidden", trimmed[at]);
            if (!tabs.NameAllowed(trimmed)) return texts.Format("sheet.name.invalid", trimmed);
            return !string.Equals(trimmed, previous, StringComparison.Ordinal) && tabs.NameTaken(workbook, sheet, trimmed)
                ? texts.Format("status.sheet.nameTaken", trimmed)
                : null;
        }

        var chosen = Context.Tabs is { } surface && surface.Shows(Context.SheetIndex)
            ? await surface.EditNameAsync(Context.SheetIndex, previous, Problem).ConfigureAwait(true)
            : await dialogs.ShowAsync<string>(
                new TextPromptView(texts["sheet.rename.explanation"], previous, texts, rules:
                [
                    new PulseOffice.UI.Controls.Prompts.RequiredTextRule(texts["prompt.rule.required"]),
                    new PulseOffice.UI.Controls.Prompts.ForbiddenCharactersRule(tabs.ForbiddenNameCharacters, found => texts.Format("prompt.rule.forbidden", found)),
                    new PulseOffice.UI.Controls.Prompts.MaximumLengthRule(tabs.MaxNameLength, _ => texts.Format("sheet.name.invalid", string.Empty)),
                    new PulseOffice.UI.Controls.Prompts.UniqueNameRule(
                        candidate => !string.Equals(candidate, previous, StringComparison.Ordinal) && tabs.NameTaken(workbook, sheet, candidate),
                        taken => texts.Format("status.sheet.nameTaken", taken)),
                ]),
                new DialogOptions { TitleKey = "dialog.sheetRename.title", PreferredWidth = 380 }, ct)
                .ConfigureAwait(true);

        if (chosen is not { Length: > 0 } name || !StillEditable(workbook, sheet)) return;

        var trimmed = name.Trim();
        if (trimmed.Length == 0 || string.Equals(trimmed, previous, StringComparison.Ordinal)) return;

        if (!tabs.NameAllowed(trimmed))
        {
            status.Report(texts.Format("sheet.name.invalid", trimmed), NotificationSeverity.Error);
            return;
        }

        if (tabs.NameTaken(workbook, sheet, trimmed))
        {
            status.Report(texts.Format("status.sheet.nameTaken", trimmed), NotificationSeverity.Error);
            return;
        }

        if (Context.Engine is not { } engine) return;

        var plan = renaming.Plan(workbook, sheet, trimmed);
        renaming.Apply(engine, plan);

        Context.History?.Push(new DelegateAction(
            texts["history.sheet.rename"],
            redo: () => renaming.Apply(engine, plan),
            undo: () => renaming.Revert(engine, plan)));

        Context.RequestRefresh();
    }

    private async Task ProtectAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var index = Context.SheetIndex;
        var sheet = workbook.Sheets[index];
        var previous = sheet.Protection;

        var choice = await dialogs.ShowAsync<DataDialogs.SheetProtectionChoice>(
            new DataDialogs.SheetProtectionView(kit, previous),
            new DialogOptions { TitleKey = "command.ledger.worksheet.protect.title" }, ct)
            .ConfigureAwait(true);

        if (choice is null) return;

        var password = choice.Password;

        if (previous.IsEnabled && !previous.Accepts(password))
        {
            status.Report(texts["protect.wrongPassword"], NotificationSeverity.Error);
            return;
        }

        var next = previous.IsEnabled
            ? previous with { IsEnabled = false, PasswordHash = null }
            : choice.Permissions with
            {
                IsEnabled = true,
                PasswordHash = password is { Length: > 0 } ? WorkbookProtection.Hash(password) : null,
            };

        OutsideHistory(() => sheet.Protection = next);

        Context.SelectSheet(index);
        Context.RequestRefresh();
    }

    private async Task ColourAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        if (RefuseStructureChange(status, texts)) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var previous = sheet.TabColorHex;

        string? colour;
        if (Context.Tabs is { } surface && surface.Shows(Context.SheetIndex))
        {
            var picked = await surface.ShowAtAsync<PulseOffice.UI.Commands.RibbonColor>(Context.SheetIndex, done =>
            {
                var palette = new PulseOffice.UI.Controls.Ribbon.ColorPaletteView(
                    palettes.Build(palettes.OfficeTheme),
                    new PulseOffice.UI.Controls.Ribbon.ColorPaletteLabels(texts["sheet.colour.none"], texts["ribbon.colour.theme"], texts["ribbon.colour.standard"], null),
                    previous, kit.Metrics);
                palette.Chosen += (_, chosen) => done(chosen);
                return new Avalonia.Controls.StackPanel { Spacing = 4, Children = { kit.Caption("sheet.colour.label"), palette } };
            }).ConfigureAwait(true);
            if (picked is null) return;
            colour = picked.Hex is { Length: > 0 } hex ? hex : null;
        }
        else
        {
            var values = await dialogs.ShowAsync<Dictionary<string, object>>(
                new FormPromptView(texts["sheet.colour.explanation"], [new ColorField("colour", texts["sheet.colour.label"], previous)], texts),
                new DialogOptions { TitleKey = "dialog.sheetColour.title", PreferredWidth = 380 }, ct)
                .ConfigureAwait(true);
            if (values is null) return;
            colour = values["colour"] is string { Length: > 0 } chosen ? chosen : null;
        }

        if (!StillEditable(workbook, sheet)) return;

        if (string.Equals(colour, previous, StringComparison.OrdinalIgnoreCase)) return;

        sheet.TabColorHex = colour;

        Context.History?.Push(new DelegateAction(
            texts["history.sheet.colour"],
            redo: () => sheet.TabColorHex = colour,
            undo: () => sheet.TabColorHex = previous));

        Context.RequestRefresh();
    }

    private bool StillEditable(Workbook workbook, Worksheet sheet) =>
        ReferenceEquals(Context.Workbook, workbook) && workbook.IndexOf(sheet) >= 0 && !RefuseStructureChange(status, texts);

    private async Task ShowHiddenAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        if (RefuseStructureChange(status, texts)) return;

        var hidden = workbook.Sheets.Where(sheet => sheet.IsHidden).ToList();
        if (hidden.Count == 0) return;

        var names = hidden.Select(sheet => sheet.Name).ToList();

        var answers = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(texts["sheet.show.explanation"],
            [
                new PulseOffice.UI.Controls.Lists.ChecklistField("sheets", texts["sheet.show.which"], names, names),
            ], texts),
            new DialogOptions { TitleKey = "dialog.sheetShow.title", PreferredWidth = 400 }, ct)
            .ConfigureAwait(true);

        if (answers?.GetValueOrDefault("sheets") is not IReadOnlyList<string> chosen || chosen.Count == 0) return;

        if (!ReferenceEquals(Context.Workbook, workbook) || RefuseStructureChange(status, texts)) return;

        var shown = hidden.Where(sheet => sheet.IsHidden && workbook.IndexOf(sheet) >= 0 && chosen.Contains(sheet.Name)).ToList();
        if (shown.Count == 0) return;

        foreach (var sheet in shown) sheet.IsHidden = false;

        Context.History?.Push(new DelegateAction(
            texts["history.sheet.show"],
            redo: () => shown.ForEach(sheet => sheet.IsHidden = false),
            undo: () => shown.ForEach(sheet => sheet.IsHidden = true)));

        Context.SelectSheet(workbook.IndexOf(shown[0]));
        Context.RequestRefresh();
    }
}
