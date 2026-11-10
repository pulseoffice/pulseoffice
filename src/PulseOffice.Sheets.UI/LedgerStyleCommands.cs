using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Styling;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerStyleCommands(
    IActiveSheetContext context,
    ISheetFormattingService formatting,
    IDialogService dialogs,
    IStatusBarService status,
    ISheetEditGuard guard,
    PulseOffice.Shell.IDocumentWorkspace workspace,
    PulseOffice.UI.Controls.Galleries.IGalleryNavigator navigator,
    ICellStyleReferences references,
    PulseOffice.UI.Controls.StylePanels.IStyleRenamePrompt renames,
    ITextCatalog texts) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("ledger.style.apply") with
        {
            MenuPath = "format/styles",
            ContextPaths = ["cell/notes"],
            Surface = CommandSurface.Common | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            RequiredSelection = SelectionKind.Cell | SelectionKind.CellRange,
            EnabledWhen = _ => formatting.Accepts("ledger.style.apply"),
            RibbonControl = new ChoiceSpec(Gallery, CurrentName)
            {
                Look = RibbonChoiceLook.Gallery,
                InlineTiles = 0,
                Width = 140,
                MoreCommandId = "ledger.style.createFromSelection",
            },
            Execute = (ctx, ct) => ctx.Argument is string name ? ApplyNamed(name) : ApplyAsync(ct),
        },

        Prototype("ledger.style.createFromSelection") with
        {
            MenuPath = "format/styles",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            RequiredSelection = SelectionKind.Cell | SelectionKind.CellRange,
            EnabledWhen = _ => formatting.Accepts("ledger.style.createFromSelection"),
            Execute = (_, ct) => CreateAsync(ct),
        },

        Prototype("ledger.style.updateFromSelection") with
        {
            MenuPath = "format/styles",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            RequiredSelection = SelectionKind.Cell | SelectionKind.CellRange,
            EnabledWhen = _ => formatting.Accepts("ledger.style.updateFromSelection")
                && Context.Workbook is { } workbook
                && workbook.Sheets[Context.SheetIndex][Context.Selection.Start].StyleName is { Length: > 0 },
            Execute = (_, _) =>
            {
                UpdateFromSelection();
                return Task.CompletedTask;
            },
        },
    ];

    public void UpdateFromSelection()
    {
        if (Context.Workbook is not { } workbook) return;

        var cell = workbook.Sheets[Context.SheetIndex][Context.Selection.Start];
        if (cell.StyleName is not { Length: > 0 } name) return;

        Define(workbook, name, "command.ledger.style.updateFromSelection.title");

        Context.RequestRefresh();
    }

    private async Task ApplyAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var styles = workbook.NamedStyles.All.ToList();
        if (styles.Count == 0) return;

        var looks = styles.ToDictionary(named => named.Name, named => workbook.NamedStyles.StyleOf(named), StringComparer.Ordinal);
        var items = styles.Select(named => new PulseOffice.UI.Controls.Galleries.ThumbnailGalleryItem<string>(
            named.Name, Title(named), texts[named.IsBuiltIn ? "style.group.builtIn" : "style.group.custom"], named.Name)).ToList();
        var picker = new PulseOffice.UI.Controls.Galleries.GalleryPickerView<string>(
            texts["style.apply.explanation"], items, new CellFormat.CellStylePainter(looks, texts["conditional.sample"]), navigator,
            new PulseOffice.UI.Controls.Galleries.ThumbnailGalleryOptions
            {
                TileWidth = 116,
                ThumbnailHeight = 34,
                Columns = 5,
                Scrolls = true,
                MaxViewportHeight = 380,
                Filterable = styles.Count > 15,
                FilterPlaceholder = texts["style.filter"],
                EmptyText = texts["style.filter.empty"],
                AccessibleName = texts["dialog.style.title"],
            },
            texts, CurrentName());

        var chosen = await dialogs.ShowAsync<string>(picker, new DialogOptions { TitleKey = "dialog.style.title", PreferredWidth = 680 }, ct)
            .ConfigureAwait(true);
        if (chosen is not null && workbook.NamedStyles.Find(chosen) is { } named) Apply(workbook, named);
    }

    public Task ApplyNamed(string name)
    {
        if (Context.Workbook is { } workbook && workbook.NamedStyles.Find(name) is { } chosen)
            Apply(workbook, chosen);

        return Task.CompletedTask;
    }

    private IReadOnlyList<RibbonChoice> Gallery()
    {
        if (Context.Workbook is not { } workbook) return [];

        return [.. workbook.NamedStyles.All.Select(named =>
        {
            var style = workbook.NamedStyles.StyleOf(named);

            return new RibbonChoice(named.Name, named.IsBuiltIn ? BuiltInTitle(named.Name) : named.Name)
            {
                Sample = new RibbonSample(style.BackgroundHex, style.Font.ColorHex, style.Font.Bold, style.Font.Italic,
                    style.Borders.IsEmpty ? null : style.Borders.Bottom.ColorHex)
                {
                    FontFamily = style.Font.Family,
                    FontSize = style.Font.Size,
                    Underline = style.Font.Underline || style.Font.DoubleUnderline,
                    Strikethrough = style.Font.Strikethrough,
                    Align = style.Horizontal switch
                    {
                        CellHorizontalAlignment.Left => RibbonSampleAlign.Start,
                        CellHorizontalAlignment.Right => RibbonSampleAlign.End,
                        _ => RibbonSampleAlign.Center,
                    },
                },
            };
        })];
    }

    public string Title(NamedCellStyle named) => named.IsBuiltIn ? BuiltInTitle(named.Name) : named.Name;

    public void Delete(string name)
    {
        if (Context.Workbook is not { } workbook || workbook.NamedStyles.Find(name) is not { IsBuiltIn: false } || Refuses(workbook, name)) return;
        if (references.Delete(workbook, name) is not { } relink) return;
        Record(workbook, relink, texts["styles.panel.delete"]);
    }

    public bool CanRename(string name) =>
        Context.Workbook is { } workbook && workbook.NamedStyles.Find(name) is { IsBuiltIn: false };

    public async Task RenameAsync(string name, CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook || !CanRename(name) || Refuses(workbook, name)) return;
        if (await renames.AskAsync(name, "ledger.styles.rename.explanation", ct).ConfigureAwait(true) is not { } chosen) return;
        if (Context.Workbook is not { } current || !ReferenceEquals(current, workbook) || Refuses(workbook, name)) return;
        switch (references.CheckRename(workbook, name, chosen))
        {
            case CellStyleRenameCheck.Taken:
                status.Report(texts.Format("styles.rename.taken", chosen.Trim()), NotificationSeverity.Warning);
                return;
            case CellStyleRenameCheck.Invalid:
                status.Report(texts.Format("styles.rename.invalid", references.MaximumNameLength), NotificationSeverity.Warning);
                return;
            case CellStyleRenameCheck.Allowed:
                break;
            default:
                return;
        }
        if (references.Rename(workbook, name, chosen) is not { } relink) return;
        Record(workbook, relink, texts["styles.rename.title"]);
        status.Report(texts.Format("styles.renamed", chosen.Trim()));
    }

    private bool Refuses(Workbook workbook, string name)
    {
        if (workspace.Active?.IsReadOnly == true)
        {
            status.Report(texts["status.readOnly"], NotificationSeverity.Error, NotificationRemedies.EditMode);
            return true;
        }
        if (references.SheetsUsing(workbook, name).All(guard.CanFormat)) return false;
        status.Report(texts["status.protection.cellFormat"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
        return true;
    }

    private void Record(Workbook workbook, CellStyleRelink relink, string description)
    {
        Context.History?.Push(new PulseOffice.Document.Commands.DelegateAction(description,
            redo: () => references.Apply(workbook, relink),
            undo: () => references.Revert(workbook, relink)));
        Context.RequestRefresh();
    }

    private string BuiltInTitle(string name) =>
        texts.Contains($"style.builtin.{name}") ? texts[$"style.builtin.{name}"] : name;

    public string? CurrentName() =>
        Context.Workbook is { } workbook ? workbook.Sheets[Context.SheetIndex][Context.Selection.Start].StyleName : null;

    private void Apply(Workbook workbook, NamedCellStyle chosen)
    {
        var style = workbook.NamedStyles.StyleOf(chosen);
        var sheet = workbook.Sheets[Context.SheetIndex];
        if (workspace.Active?.IsReadOnly == true)
        {
            status.Report(texts["status.readOnly"], NotificationSeverity.Error, NotificationRemedies.EditMode);
            return;
        }
        if (!guard.CanFormat(sheet))
        {
            status.Report(texts["status.protection.cellFormat"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return;
        }

        var description = texts["command.ledger.style.apply.title"];
        using var transaction = Context.History?.BeginTransaction(description);

        formatting.ApplyToSelection(description, _ => style);

        var cells = sheet.NonEmptyCellsIn(Context.Selection).Select(pair => pair.Key).ToList();
        var previous = cells.Select(address => sheet[address].StyleName).ToList();

        foreach (var address in cells)
            sheet.SetStyleName(address, chosen.Name);

        Context.History?.Push(new PulseOffice.Document.Commands.DelegateAction(description,
            redo: () =>
            {
                foreach (var address in cells) sheet.SetStyleName(address, chosen.Name);
            },
            undo: () =>
            {
                for (var index = 0; index < cells.Count; index++) sheet.SetStyleName(cells[index], previous[index]);
            }));

        status.Report(texts.Format("status.style.applied", BuiltInTitle(chosen.Name)));
    }

    public async Task CreateAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var choice = await dialogs.ShowAsync<CellFormat.NewCellStyleChoice>(
            new CellFormat.NewCellStyleView(formatting.CurrentStyle, string.Empty, candidate => workbook.NamedStyles.Find(candidate) is not null, texts),
            new DialogOptions { TitleKey = "dialog.styleCreate.title", PreferredWidth = 520 }, ct)
            .ConfigureAwait(true);

        if (choice?.Name.Trim() is not { Length: > 0 } trimmed) return;

        Define(workbook, trimmed, "command.ledger.style.createFromSelection.title");
        status.Report(texts.Format("status.style.created", trimmed));
    }

    public async Task DeriveAsync(string parent, CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook || workbook.NamedStyles.Find(parent) is not { } basis) return;

        var look = workbook.NamedStyles.StyleOf(basis);
        var choice = await dialogs.ShowAsync<CellFormat.NewCellStyleChoice>(
            new CellFormat.NewCellStyleView(look, Title(basis) + " 2", candidate => workbook.NamedStyles.Find(candidate) is not null, texts,
                new CellFormat.NewCellStyleInheritance(look, CellStyleProperties.None, workbook.NamedStyles.Layering)),
            new DialogOptions { TitleKey = "dialog.styleCreate.title", PreferredWidth = 560 }, ct)
            .ConfigureAwait(true);

        if (choice?.Name.Trim() is not { Length: > 0 } trimmed || workbook.NamedStyles.Find(basis.Name) is null) return;

        Define(workbook, trimmed, "command.ledger.style.createFromSelection.title",
            new NamedStyleDefinition(choice.Style, basis.Name, false, choice.Explicit ?? CellStyleProperties.None));
        status.Report(texts.Format("status.style.created", trimmed));
    }

    private void Define(Workbook workbook, string name, string titleKey) =>
        Define(workbook, name, titleKey, Definition(workbook, name, formatting.CurrentStyle));

    public NamedStyleDefinition Definition(Workbook workbook, string name, CellStyle look)
    {
        var catalog = workbook.NamedStyles;
        if (catalog.Find(name) is not { } existing) return new NamedStyleDefinition(look, null, false, null);

        var inherited = catalog.InheritedStyleOf(existing);
        var declared = existing.Explicit | catalog.Layering.Differences(look, inherited);
        return new NamedStyleDefinition(look, existing.Parent, existing.IsBuiltIn, declared);
    }

    private void Define(Workbook workbook, string name, string titleKey, NamedStyleDefinition definition)
    {
        var catalog = workbook.NamedStyles;
        var position = catalog.IndexOf(name);
        var before = catalog.Find(name);
        var after = catalog.Define(before?.Name ?? name, definition.Style, definition.Parent, definition.BuiltIn, definition.Explicit);

        Context.History?.Push(new PulseOffice.Document.Commands.DelegateAction(texts[titleKey],
            redo: () => catalog.Restore(name, after, position),
            undo: () => catalog.Restore(name, before, position)));
    }
}

public sealed record NamedStyleDefinition(CellStyle Style, string? Parent, bool BuiltIn, CellStyleProperties? Explicit);
