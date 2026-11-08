using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerFinalCommands(
    IActiveSheetContext context,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts,
    ISheetThemeApplier themeApplier,
    PulseOffice.Sheets.Pivot.IPivotFormulas pivotFormulas,
    PulseOffice.Sheets.Pivot.IPivotCoordinator pivots,
    PulseOffice.Sheets.Engine.Operations.IWorkbookCheckpoints checkpoints,
    ISheetFormattingService formatting,
    PulseOffice.Sheets.Formatting.IColumnAutoWidth columnWidths,
    Dialogs.ISheetReferenceSources sources,
    Dialogs.ISheetReferenceReader reader,
    PulseOffice.UI.Controls.Tables.ITableStyleGallery gallery,
    ILedgerTableLooks looks,
    PulseOffice.UI.Controls.Themes.IThemePaletteGallery themeGallery,
    CellFormat.FormatCellsServices cellServices,
    INumberFormatCodeEditor codes,
    PulseOffice.Sheets.Pivot.IPivotSources? pivotSources = null) : LedgerCommandSet(context)
{
    private readonly PulseOffice.Sheets.Pivot.IPivotSources _pivotSources = pivotSources ?? new PulseOffice.Sheets.Pivot.PivotSources();

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("ledger.format.paintbrush", "edit/clipboard", _ =>
        {
            CopyFormat();
            return Task.CompletedTask;
        }),

        Command("ledger.format.thousands", "format/number", _ =>
        {
            ToggleThousands();
            return Task.CompletedTask;
        }),

        Command("ledger.format.optimalRowHeight", "format/rows", _ => OptimalRows()),
        Command("ledger.format.optimalColumnWidth", "format/columns", OptimalColumnsAsync),
        Command("ledger.format.paragraph", "format/cells", ct => ParagraphAsync(ct)),
        Command("ledger.format.tableStyles", "format/styles", ct => TableStylesAsync(ct)),
        Command("ledger.format.theme", "format/styles", ct => ThemeAsync(ct)),

        Command("ledger.pivot.calculatedField", "data/pivot", ct => CalculatedFieldAsync(ct)) with { EnabledWhen = _ => HasPivot },
        Command("ledger.pivot.delete", "data/pivot", _ =>
        {
            DeletePivot();
            return Task.CompletedTask;
        }) with { EnabledWhen = _ => HasPivot },

        Command("ledger.object.enterGroup", "format/arrange", _ =>
        {
            EnterGroup();
            return Task.CompletedTask;
        }, () => Context.SelectedObject?.Content is Drawing.Shapes.ShapeGroupObject),

        Command("ledger.object.leaveGroup", "format/arrange", _ =>
        {
            LeaveGroup();
            return Task.CompletedTask;
        }),

        Command("ledger.object.anchor", "format/arrange", ct => AnchorAsync(ct), () => Context.SelectedObject is not null),
    ];

    public CellStyle? CopiedFormat { get; private set; }

    private string _copiedFormatCode = "General";

    public string? EnteredGroup { get; private set; }

    private CommandDefinition Command(string id, string path, Func<CancellationToken, Task> run, Func<bool>? applies = null) => Prototype(id) with
    {
        IconKey = id,
        MenuPath = path,
        Surface = CommandSurface.Advanced,
        RequiredState = DocumentState.DocumentOpen,
        ForbiddenState = DocumentState.ReadOnly,
        EnabledWhen = _ => Context.HasTarget && (applies?.Invoke() ?? true),
        Execute = (_, ct) => run(ct),
    };

    private void CopyFormat()
    {
        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];

        if (CopiedFormat is null)
        {
            CopiedFormat = workbook.Styles.Get(sheet.StyleIdAt(Context.Selection.Start));
            _copiedFormatCode = workbook.NumberFormats.CodeOf(CopiedFormat.NumberFormatId);
            status.Report(texts["paintbrush.copied"]);
            return;
        }

        var copied = CopiedFormat with { NumberFormatId = workbook.NumberFormats.Register(_copiedFormatCode) };
        CopiedFormat = null;
        formatting.ApplyToSelection(texts["history.format.paintbrush"], _ => copied);
    }

    private void ToggleThousands()
    {
        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var grouped = !codes.HasGrouping(Code(workbook, workbook.Styles.Get(sheet.StyleIdAt(Context.Selection.Start)).NumberFormatId));

        formatting.ApplyToSelection(texts["history.format.thousands"], style =>
        {
            var format = workbook.NumberFormats.Get(style.NumberFormatId);
            if (format.Sections[0].ValueKind is not (Formatting.FormatValueKind.Number or Formatting.FormatValueKind.Scientific)) return style;
            var code = codes.WithGrouping(Code(workbook, style.NumberFormatId), grouped);
            return style with { NumberFormatId = workbook.NumberFormats.Register(code) };
        });
    }

    private string Code(Workbook workbook, int formatId)
    {
        var format = workbook.NumberFormats.Get(formatId);
        return format.IsGeneral ? "0" : format.Code;
    }

    private Task OptimalRows()
    {
        if (Context.Workbook is { } workbook) AutomaticRows(workbook);
        return Task.CompletedTask;
    }

    private async Task OptimalColumnsAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(
                texts["optimal.columns"],
                [new NumberField("margin", texts["optimal.padding"], 2, 0, 20) { Unit = "pt", Presets = [0, 2, 4, 8, 12] }],
                texts),
            new DialogOptions
            {
                TitleKey = "command.ledger.format.optimalColumnWidth.title",
            },
            ct).ConfigureAwait(true);

        if (values is null) return;

        var padding = Convert.ToDouble(values["margin"], System.Globalization.CultureInfo.InvariantCulture);
        var sheet = workbook.Sheets[Context.SheetIndex];
        if (sheet.Protection.IsEnabled)
        {
            status.Report(texts["status.protection.format"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return;
        }
        if (sheet.UsedRange() is not { } used) return;

        var selection = Context.Selection;
        var layout = sheet.Layout;
        var first = Math.Max(selection.Start.Column, used.Start.Column);
        var last = Math.Min(selection.End.Column, used.End.Column);
        if (first > last) return;

        var changes = new List<(int Index, Foundation.Units.Twip? Before, Foundation.Units.Twip? After)>();

        for (var index = first; index <= last; index++)
        {
            if (layout.IsColumnHidden(index)) continue;

            var size = columnWidths.Measure(workbook, sheet, index, selection, padding);
            Foundation.Units.Twip? before = layout.ColumnWidths.TryGetValue(index, out var existing) ? existing : null;
            if (before == size) continue;
            changes.Add((index, before, size));
        }

        if (changes.Count == 0) return;

        void Set(bool after)
        {
            foreach (var (index, before, size) in changes)
            {
                if ((after ? size : before) is { } width) layout.SetColumnWidth(index, width);
                else layout.ResetColumnWidth(index);
            }
            Context.RequestRefresh();
        }

        Set(true);
        Context.History?.Push(new Document.Commands.DelegateAction(texts["history.format.optimal"], redo: () => Set(true), undo: () => Set(false)));
    }

    private void AutomaticRows(Workbook workbook)
    {
        var sheet = workbook.Sheets[Context.SheetIndex];
        if (sheet.Protection.IsEnabled)
        {
            status.Report(texts["status.protection.format"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return;
        }

        var selection = Context.Selection;
        var layout = sheet.Layout;
        var last = Math.Min(selection.End.Row, new[] { sheet.UsedRange()?.End.Row ?? -1 }.Concat(layout.RowHeights.Keys).Max());
        var manual = (layout.DefaultRowHeightManual
                ? Enumerable.Range(selection.Start.Row, Math.Max(0, last - selection.Start.Row + 1))
                : layout.RowHeights.Keys.Where(index => index >= selection.Start.Row && index <= selection.End.Row))
            .Where(index => !layout.IsRowHidden(index) && !layout.IsRowAutomatic(index))
            .Select(index => (Index: index, Height: layout.RowHeights.TryGetValue(index, out var height) ? height : (Foundation.Units.Twip?)null))
            .ToList();
        if (manual.Count == 0) return;

        void Set(bool automatic)
        {
            foreach (var (index, height) in manual)
            {
                if (automatic) layout.MakeRowOptimal(index);
                else if (height is { } size) layout.SetRowHeight(index, size);
                else layout.ClearRowOptimal(index);
            }
            Context.RequestRefresh();
        }

        Set(true);
        Context.History?.Push(new Document.Commands.DelegateAction(texts["history.format.optimal"], redo: () => Set(true), undo: () => Set(false)));
    }

    private async Task ParagraphAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var style = workbook.Styles.Get(sheet.StyleIdAt(Context.Selection.Start));

        var value = sheet.GetValue(Context.Selection.Start);
        var sample = value.Kind == Values.ValueKind.Text && value.AsText() is { Length: > 0 and <= 24 } text ? text : texts["format.sample.text"];
        var updated = await dialogs.ShowAsync<CellStyle>(
            new CellFormat.CellAlignmentDialogView(style, sample, cellServices, texts),
            new DialogOptions { TitleKey = "command.ledger.format.paragraph.title", PreferredWidth = 640 }, ct).ConfigureAwait(true);

        if (updated is null) return;

        formatting.ApplyToSelection(texts["history.format.paragraph"],
            current => current with
            {
                Horizontal = updated.Horizontal,
                Vertical = updated.Vertical,
                Indent = updated.Indent,
                WrapText = updated.WrapText,
                RotationDegrees = updated.RotationDegrees,
            });
    }

    private async Task TableStylesAsync(CancellationToken ct)
    {
        if (Context.Workbook is null) return;
        if (await gallery.AskAsync("command.ledger.format.tableStyles.title", null, true, true, ct).ConfigureAwait(true) is not { } choice) return;
        looks.ApplyToSelection(texts["history.format.tableStyle"], choice.Look, choice.HeaderRow, choice.BandedRows);
    }

    private async Task ThemeAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        if (await themeGallery.AskAsync("command.ledger.format.theme.title", "theme.explanation", null, ct).ConfigureAwait(true) is not { } chosen) return;

        if (Context.Engine is not { } engine || Context.History is not { } history) return;

        var namedBefore = workbook.NamedStyles.All.ToList();
        var before = checkpoints.Capture(workbook);
        var restyled = themeApplier.Apply(workbook, chosen);
        var namedAfter = workbook.NamedStyles.All.ToList();
        var after = checkpoints.Capture(workbook);

        history.Push(new Document.Commands.DelegateAction(texts["command.ledger.format.theme.title"],
            redo: () =>
            {
                workbook.NamedStyles.ReplaceAll(namedAfter);
                checkpoints.Restore(engine, workbook, after);
            },
            undo: () =>
            {
                workbook.NamedStyles.ReplaceAll(namedBefore);
                checkpoints.Restore(engine, workbook, before);
            }));

        Context.RequestRefresh();
        status.Report(texts.Format("theme.applied", texts["theme." + chosen.Key], restyled));
    }

    private async Task CalculatedFieldAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        if (workbook.Pivots.Count == 0)
        {
            status.Report(texts["pivot.none"], NotificationSeverity.Warning);
            return;
        }

        var current = CurrentPivot(workbook);
        var definition = workbook.Pivots[Math.Max(current, 0)];

        var fields = new List<FormField>();
        if (workbook.Pivots.Count > 1)
            fields.Add(new ChoiceField("pivot", texts["pivot.calculated.pivot"], [.. workbook.Pivots.Select(pivot => pivot.Name)], Math.Max(current, 0)));

        if (_pivotSources.Open(workbook, definition) is not { } source)
        {
            status.Report(texts["pivot.calculated.noSource"], NotificationSeverity.Error);
            return;
        }

        var headers = source.Headers;
        fields.Add(new TextField("name", texts["pivot.calculated.name"], NextFieldName(definition)));
        fields.Add(new Analysis.PivotFormulaField("formula", texts["pivot.calculated.formula"], "=", headers));

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(
                texts["pivot.calculated.explanationShort"],
                fields,
                texts),
            new DialogOptions { TitleKey = "command.ledger.pivot.calculatedField.title" }, ct)
            .ConfigureAwait(true);

        if (values is null) return;

        if (values.TryGetValue("pivot", out var chosen))
        {
            definition = workbook.Pivots[(int)Convert.ToInt64(chosen, System.Globalization.CultureInfo.InvariantCulture)];
            if (_pivotSources.Open(workbook, definition) is not { } other) return;
            headers = other.Headers;
        }

        var name = (values["name"] as string ?? string.Empty).Trim();
        var formula = (values["formula"] as string ?? string.Empty).Trim();

        if (name.Length == 0 || definition.Fields.Any(field => string.Equals(field.Label, name, StringComparison.CurrentCultureIgnoreCase)))
        {
            status.Report(texts["pivot.calculated.badName"], NotificationSeverity.Error);
            return;
        }

        var compiled = pivotFormulas.Compile(formula, headers);
        if (!compiled.IsValid || compiled.Columns.Count == 0)
        {
            status.Report(texts["pivot.calculated.badFormula"], NotificationSeverity.Error);
            return;
        }

        if (workbook.FindSheet(definition.DestinationSheet)?.Protection.IsEnabled == true)
        {
            status.Report(texts["status.protection.locked"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return;
        }

        var order = definition.In(PulseOffice.Sheets.Pivot.PivotArea.Value).Select(field => field.AreaOrder).DefaultIfEmpty(-1).Max() + 1;
        var updated = definition with
        {
            Fields =
            [
                .. definition.Fields,
                new PulseOffice.Sheets.Pivot.PivotField
                {
                    SourceName = name,
                    SourceColumnIndex = -1,
                    Formula = formula.StartsWith('=') ? formula : "=" + formula,
                    Area = PulseOffice.Sheets.Pivot.PivotArea.Value,
                    AreaOrder = order,
                },
            ],
        };

        var result = Checkpointed(checkpoints, texts["command.ledger.pivot.calculatedField.title"],
            () => pivots.Apply(workbook, updated, new PulseOffice.Sheets.Pivot.PivotLabels(texts["pivot.grandTotal"], texts["pivot.subtotal"])),
            outcome => outcome.Succeeded);

        Context.RequestRefresh();
        status.Report(result.Succeeded
            ? texts.Format("pivot.calculated.added", name)
            : texts.Format($"pivot.diagnostic.{result.Diagnostics[0].Code}", result.Diagnostics[0].Location ?? string.Empty));
    }

    private string NextFieldName(PulseOffice.Sheets.Pivot.PivotDefinition definition)
    {
        for (var index = 1; ; index++)
        {
            var name = texts.Format("pivot.calculated.defaultName", index);
            if (!definition.Fields.Any(field => string.Equals(field.Label, name, StringComparison.CurrentCultureIgnoreCase))) return name;
        }
    }

    private int CurrentPivot(Workbook workbook) =>
        workbook.Pivots.FindIndex(pivot => pivot.DestinationSheet == workbook.Sheets[Context.SheetIndex].Name
            && pivot.OutputRange is { } output && output.Contains(Context.Selection.Start));

    private bool HasPivot => Context.HasTarget && Context.Workbook is { Pivots.Count: > 0 };

    private void DeletePivot()
    {
        if (Context.Workbook is not { } workbook) return;
        if (workbook.Pivots.Count == 0)
        {
            status.Report(texts["pivot.none"], NotificationSeverity.Warning);
            return;
        }

        var sheetName = workbook.Sheets[Context.SheetIndex].Name;
        var cursor = Context.Selection.Start;
        if (workbook.Pivots.FirstOrDefault(pivot => pivot.DestinationSheet == sheetName
                && pivot.OutputRange is { } output && output.Contains(cursor)) is not { } removed)
        {
            status.Report(texts["pivot.notUnderCursor"], NotificationSeverity.Warning);
            return;
        }

        Checkpointed(checkpoints, texts["history.pivot.delete"], () =>
        {
            pivots.Remove(workbook, removed.Id);
            return true;
        });

        Context.RequestRefresh();
    }

    private void EnterGroup()
    {
        if (Context.SelectedObject?.Content is not Drawing.Shapes.ShapeGroupObject group)
        {
            status.Report(texts["group.selectFirst"], NotificationSeverity.Warning);
            return;
        }

        EnteredGroup = group.Id;
        status.Report(texts.Format("group.entered", group.Children.Count));
    }

    private void LeaveGroup()
    {
        EnteredGroup = null;
        status.Report(texts["group.left"]);
    }

    private async Task AnchorAsync(CancellationToken ct)
    {
        if (Context.SelectedObject is not { } placed)
        {
            status.Report(texts["anchor.selectFirst"], NotificationSeverity.Warning);
            return;
        }

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(
                texts.Format("anchor.explanation", placed.Anchor.ToA1()),
                [new RangeField("cell", texts["anchor.cell"], placed.Anchor.ToA1(), sources.For(Dialogs.ReferenceShape.Cell))],
                texts),
            new DialogOptions { TitleKey = "command.ledger.object.anchor.title" }, ct).ConfigureAwait(true);

        if (values is null) return;
        if (Context.Workbook is not { } workbook) return;
        if (reader.ReadLocal(workbook, values["cell"] as string ?? string.Empty, Context.SheetIndex, Dialogs.ReferenceShape.Cell) is not { Start: var address })
        {
            status.Report(texts["status.analysis.badReference"], NotificationSeverity.Error);
            return;
        }

        var sheet = workbook.Sheets[Context.SheetIndex];
        var index = sheet.Objects.IndexOf(placed);

        if (index < 0) return;

        var replacement = placed with { Anchor = address };

        sheet.Objects[index] = replacement;

        Context.History?.Push(new Document.Commands.DelegateAction(
            texts["history.object.anchor"],
            redo: () => sheet.Objects[index] = replacement,
            undo: () => sheet.Objects[index] = placed));

        Context.SelectObject(replacement);
        Context.RequestRefresh();
    }
}
