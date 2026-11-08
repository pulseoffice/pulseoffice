using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerSheetExtraCommands(
    IActiveSheetContext context,
    ISheetRandomFill random,
    ISheetFillAcross fillAcross,
    IWorkbookCheckpoints checkpoints,
    Dialogs.ISheetReferenceReader reader,
    Dialogs.ISheetReferenceSources sources,
    ISheetSelectionSet selectedSheets,
    IDialogService dialogs,
    PulseOffice.UI.Controls.Macros.IMacroEventsDialog macroEvents,
    LedgerMacroEventsSource macroEventsSource,
    PulseOffice.Platform.IFileDialogService files,
    IStatusBarService status,
    ITextCatalog texts,
    DataDialogs.LedgerDialogKit kit,
    DataDialogs.ILedgerDialogProbes probes,
    ISheetRegions regions,
    ICellEditText editTexts,
    ICellInputRecognizer recognizer,
    ISheetValueWrites writes,
    PulseOffice.Formats.ISpreadsheetFormatRegistry formats,
    IWorksheetTabPolicy tabs) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Sheet("ledger.sheet.rightToLeft", "sheet/layout", (_, sheet, _) =>
        {
            var next = !sheet.RightToLeft;
            sheet.RightToLeft = next;
            Context.History?.Push(new PulseOffice.Document.Commands.DelegateAction(
                texts["command.ledger.sheet.rightToLeft.title"],
                redo: () => sheet.RightToLeft = next,
                undo: () => sheet.RightToLeft = !next));
            return Task.CompletedTask;
        }) with
        {
            CheckedWhen = _ => Context.Workbook is { } workbook
                && Context.SheetIndex >= 0 && Context.SheetIndex < workbook.Sheets.Count
                && workbook.Sheets[Context.SheetIndex].RightToLeft,
        },

        Sheet("ledger.sheet.fromFile", "sheet/insert", (_, _, ct) => FromFileAsync(ct)),
        Sheet("ledger.sheet.fillSheets", "sheet/edit", (_, _, _) => FillSheetsAsync()),
        Sheet("ledger.sheet.labels", "sheet/names", (_, _, _) => LabelsAsync()),
        Sheet("ledger.sheet.events", "sheet/tools", (_, _, _) => EventsAsync()),
        Sheet("ledger.data.random", "data/generate", (_, _, _) => RandomAsync()),
        Sheet("ledger.data.shuffle", "data/generate", (_, _, _) => ShuffleAsync()),
        Sheet("ledger.data.form", "data/tools", (_, _, _) => FormAsync()),
    ];

    private CommandDefinition Sheet(string id, string path, Func<Workbook, Worksheet, CancellationToken, Task> run) => Prototype(id) with
    {
        IconKey = id,
        MenuPath = path,
        Surface = CommandSurface.Advanced,
        RequiredState = DocumentState.DocumentOpen,
        ForbiddenState = DocumentState.ReadOnly,
        EnabledWhen = _ => Context.HasTarget,
        Execute = async (_, ct) =>
        {
            if (Context.Workbook is not { } workbook) return;

            await run(workbook, workbook.Sheets[Context.SheetIndex], ct).ConfigureAwait(true);
            Context.RequestRefresh();
        },
    };

    private async Task FromFileAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        if (RefuseStructureChange(status, texts)) return;

        await using var chosen = await files.OpenFileAsync(
            texts["sheet.fromFile.title"],
            [.. formats.ReadableFormats.Select(format =>
                new PulseOffice.Platform.FileFilter(format.DisplayName, [.. format.Extensions.Select(extension => "*" + extension)]))],
            ct).ConfigureAwait(true);

        if (chosen is null) return;

        if (Context.Engine is not { } engine) return;

        await using var stream = await chosen.OpenRead(ct).ConfigureAwait(true);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct).ConfigureAwait(true);
        buffer.Position = 0;

        var read = formats.Read(chosen.Path, buffer);

        if (!read.Succeeded || read.Value is not { } source)
        {
            status.Report(texts["sheet.fromFile.failed"], NotificationSeverity.Error);
            return;
        }

        Checkpointed(checkpoints, texts["command.ledger.sheet.fromFile.title"], () =>
        {
            foreach (var sheet in source.Sheets)
            {
                var copy = workbook.AddSheet(tabs.UniqueName(workbook, sheet.Name));

                foreach (var (address, cell) in sheet.NonEmptyCells())
                {
                    if (cell.Formula is { } formula) copy.SetFormula(address, formula);
                    else copy.SetValue(address, cell.Value);
                }
            }

            engine.RecalculateAll();
            return source.Sheets.Count;
        });
        status.Report(texts.Format("sheet.fromFile.added", source.Sheets.Count), NotificationSeverity.Success);
    }

    private async Task FillSheetsAsync()
    {
        if (Context.Workbook is not { } workbook) return;

        var chosen = selectedSheets.Sheets.Where(index => index != Context.SheetIndex).ToList();
        var others = Enumerable.Range(0, workbook.Sheets.Count).Where(index => index != Context.SheetIndex).ToList();
        var names = (chosen.Count > 0 ? chosen : others).Select(index => workbook.Sheets[index].Name).ToList();

        var choice = await dialogs.ShowAsync<DataDialogs.FillSheetsChoice>(
            new DataDialogs.FillSheetsView(kit, workbook.Sheets[Context.SheetIndex].Name,
                [.. others.Select(index => workbook.Sheets[index].Name)], names, Context.Selection.ToString(), sources.For(Dialogs.ReferenceShape.Range)),
            new DialogOptions { TitleKey = "command.ledger.sheet.fillSheets.title" })
            .ConfigureAwait(true);

        if (choice is null) return;
        if (Context.Engine is not { } engine) return;
        if (reader.ReadLocal(workbook, choice.Area, Context.SheetIndex, Dialogs.ReferenceShape.Range) is not { } range)
        {
            status.Report(texts["status.analysis.badReference"], NotificationSeverity.Error);
            return;
        }

        var wanted = choice.Sheets.ToHashSet(StringComparer.Ordinal);
        var targets = Enumerable.Range(0, workbook.Sheets.Count)
            .Where(index => index != Context.SheetIndex && wanted.Contains(workbook.Sheets[index].Name))
            .ToList();

        if (targets.Count == 0 || choice.Content == FillAcrossContent.None) return;

        var options = new FillAcrossOptions(choice.Content, choice.SkipEmpty);
        var source = Context.SheetIndex;

        var filled = Checkpointed(checkpoints, texts["history.sheet.fill"],
            () => fillAcross.Fill(engine, workbook, source, range, targets, options));

        status.Report(filled < targets.Count ? texts["status.protection.locked"] : texts.Format("sheet.fill.done", filled),
            filled < targets.Count ? NotificationSeverity.Error : NotificationSeverity.Success);
    }

    private async Task LabelsAsync()
    {
        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var before = sheet.Labels.All.ToList();

        var choice = await dialogs.ShowAsync<DataDialogs.LabelsChoice>(
            new DataDialogs.LabelsView(kit, probes.Labels(workbook, Context.SheetIndex), before, Context.Selection.ToString(), sources.For(Dialogs.ReferenceShape.Range)),
            new DialogOptions { TitleKey = "command.ledger.sheet.labels.title" })
            .ConfigureAwait(true);

        if (choice is null || choice.Ranges.SequenceEqual(before)) return;

        var after = choice.Ranges.ToList();

        void Set(IReadOnlyList<LabelRange> ranges)
        {
            foreach (var range in sheet.Labels.All.ToList()) sheet.Labels.Remove(range.Labels);
            foreach (var range in ranges) sheet.Labels.Add(range);
        }

        Set(after);
        Context.History?.Push(new PulseOffice.Document.Commands.DelegateAction(
            texts["command.ledger.sheet.labels.title"], redo: () => Set(after), undo: () => Set(before)));

        status.Report(texts["labels.added"]);
    }

    private async Task EventsAsync()
    {
        if (Context.Workbook is not { } workbook || Context.History is not { } history) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        if (macroEventsSource.Create(sheet.Protection.IsEnabled) is not { } events) return;

        var kinds = Enum.GetValues<SheetEventKind>();
        var stem = new string([.. sheet.Name.Where(char.IsLetterOrDigit)]);
        var slots = kinds
            .Select(kind => new PulseOffice.Document.Forms.MacroEventSlot(
                kind.ToString(),
                $"sheet.event.{Lower(kind)}",
                $"sheet.event.{Lower(kind)}.description",
                $"{(stem.Length == 0 ? "Sheet" : stem)}_{kind}"))
            .ToList();

        var result = await macroEvents.ShowAsync(
            new PulseOffice.UI.Controls.Macros.MacroEventsRequest(
                slots,
                kinds.ToDictionary(kind => kind.ToString(), sheet.Events.For),
                events,
                "command.ledger.sheet.events.title"),
            CancellationToken.None).ConfigureAwait(true);

        if (result is null) return;

        var before = kinds.ToDictionary(kind => kind, sheet.Events.For);
        var after = kinds.ToDictionary(kind => kind, kind => result.GetValueOrDefault(kind.ToString()));
        if (kinds.All(kind => before[kind] == after[kind])) return;

        void Set(IReadOnlyDictionary<SheetEventKind, string?> values)
        {
            foreach (var (kind, macro) in values) sheet.Events.Bind(kind, macro);
        }

        Set(after);
        history.Push(new PulseOffice.Document.Commands.DelegateAction(
            texts["command.ledger.sheet.events.title"], () => Set(after), () => Set(before)));
        status.Report(texts["macroEvents.applied"]);
    }

    private string Lower(SheetEventKind kind) => char.ToLowerInvariant(kind.ToString()[0]) + kind.ToString()[1..];

    private async Task RandomAsync()
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.Engine is not { } engine) return;

        var choice = await dialogs.ShowAsync<DataDialogs.RandomChoice>(
            new DataDialogs.RandomView(kit, probes.Random, Context.Selection.ToString(), sources.For(Dialogs.ReferenceShape.Range)),
            new DialogOptions { TitleKey = "command.ledger.data.random.title" })
            .ConfigureAwait(true);

        if (choice is null) return;
        if (reader.ReadLocal(workbook, choice.Area, Context.SheetIndex, Dialogs.ReferenceShape.Range) is not { } range)
        {
            status.Report(texts["status.analysis.badReference"], NotificationSeverity.Error);
            return;
        }

        Apply(engine, random.Plan(choice.Request(range)), "history.data.random");
    }

    private async Task ShuffleAsync()
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.Engine is not { } engine) return;

        var choice = await dialogs.ShowAsync<DataDialogs.ShuffleChoice>(
            new DataDialogs.ShuffleView(kit, probes.Shuffle(workbook, Context.SheetIndex), Context.Selection.ToString(), sources.For(Dialogs.ReferenceShape.Range)),
            new DialogOptions { TitleKey = "command.ledger.data.shuffle.title" })
            .ConfigureAwait(true);

        if (choice is null) return;
        if (reader.ReadLocal(workbook, choice.Area, Context.SheetIndex, Dialogs.ReferenceShape.Range) is not { } range)
        {
            status.Report(texts["status.analysis.badReference"], NotificationSeverity.Error);
            return;
        }

        Apply(engine, random.Shuffle(workbook.Sheets[Context.SheetIndex], range), "history.data.shuffle");
    }

    private const int MaximumFormRecords = 10_000;

    private async Task FormAsync()
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.Engine is not { } engine) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var range = Context.Selection.IsSingleCell ? regions.CurrentRegion(sheet, Context.Selection.Start) : Context.Selection;
        var columns = range.End.Column - range.Start.Column + 1;

        var headers = Enumerable.Range(range.Start.Column, columns)
            .Select(column => sheet[new CellAddress(range.Start.Row, column)].Value.AsText())
            .ToList();

        if (headers.Count == 0 || headers.All(string.IsNullOrWhiteSpace))
        {
            status.Report(texts["dataForm.noHeaders"], NotificationSeverity.Warning);
            return;
        }

        string Text(int row, int index) => Shown(workbook, sheet, new CellAddress(row, range.Start.Column + index));
        var last = range.RowCount > 1 ? range.End.Row : range.Start.Row;
        if (range.RowCount <= 1)
            while (last + 1 < CellAddress.MaxRows && last - range.Start.Row < MaximumFormRecords
                && Enumerable.Range(0, columns).Any(index => Text(last + 1, index).Length > 0)) last++;

        var records = Enumerable.Range(range.Start.Row + 1, Math.Max(0, last - range.Start.Row))
            .Select(row => (IReadOnlyList<string>)[.. Enumerable.Range(0, columns).Select(index => Text(row, index))])
            .ToList();
        var labels = headers.Select((header, index) => header is { Length: > 0 } ? header : CellAddress.ColumnName(range.Start.Column + index)).ToList();

        var choice = await dialogs.ShowAsync<DataDialogs.DataFormChoice>(
            new DataDialogs.DataFormView(kit, new DataDialogs.DataFormModel(labels, records)),
            new DialogOptions { TitleKey = "command.ledger.data.form.title" })
            .ConfigureAwait(true);

        if (choice is null || choice.Records.Count == 0) return;

        var appended = choice.Records.Keys.Select(record => range.Start.Row + 1 + record).Where(row => row > last).ToList();
        if (appended.Any(row => row >= CellAddress.MaxRows))
        {
            status.Report(texts["dataForm.sheetFull"], NotificationSeverity.Error);
            return;
        }

        bool Occupied(int row) =>
            sheet.NonEmptyCellsIn(new CellRange(new CellAddress(row, range.Start.Column), new CellAddress(row, range.End.Column))).Any();
        if (appended.Any(Occupied))
        {
            status.Report(texts["dataForm.rowOccupied"], NotificationSeverity.Error);
            return;
        }

        var written = new List<(CellAddress, Values.CellValue)>();
        foreach (var (record, values) in choice.Records)
            for (var index = 0; index < values.Count && index < columns; index++)
            {
                var row = range.Start.Row + 1 + record;
                if (row <= last && values[index] == Text(row, index)) continue;
                written.Add((new CellAddress(row, range.Start.Column + index), Parse(values[index])));
            }

        Apply(engine, written, "history.data.form");
    }

    private Values.CellValue Parse(string text) =>
        text.Length == 0 ? Values.CellValue.Empty : recognizer.Recognize(text).Value;

    private string Shown(Workbook workbook, Worksheet sheet, CellAddress address)
    {
        var value = sheet[address].Value;
        return value.Kind is Values.ValueKind.Number or Values.ValueKind.Boolean
            ? editTexts.Value(value, workbook.NumberFormats.CodeOf(workbook.Styles.Get(sheet.StyleIdAt(address)).NumberFormatId))
            : value.AsText();
    }

    private void Apply(
        Engine.Recalculation.CalculationEngine engine,
        IReadOnlyList<(CellAddress Address, Values.CellValue Value)> written,
        string historyKey) =>
        writes.Write(Context, engine, written, texts[historyKey]);
}
