using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Foundation.Modules;
using PulseOffice.Formats.Interchange;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.UI.ExternalData;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerTailCommands(
    IActiveSheetContext context,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts,
    PulseOffice.Sheets.Engine.Operations.IWorkbookCheckpoints checkpoints,
    IExternalDataService external,
    PulseOffice.Sheets.Engine.Operations.IExternalDataWriter writer,
    PulseOffice.Shell.IDocumentWorkspace workspace,
    PulseOffice.Platform.IFileDialogService files,
    IExternalLinkSchedule schedule,
    PulseOffice.Data.UI.ITabularSnapshots snapshots,
    PulseOffice.Data.UI.IDataTableRenderer renderer,
    Dialogs.ISheetReferenceSources sources,
    Dialogs.ISheetReferenceReader reader,
    DataDialogs.LedgerDialogKit kit,
    PulseOffice.Sheets.Engine.Operations.IDefinedNameRules names) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("ledger.review.commentChange", "edit/changes", ct => CommentChangeAsync(ct)),
        Command("ledger.review.protectChanges", "edit/changes", ct => ProtectChangesAsync(ct)) with { OutsideHistory = true },
        Command("ledger.data.externalLinks", "data/external", ct => ExternalLinksAsync(ct)),
        Command("ledger.data.streams", "data/external", ct => StreamsAsync(ct)),
        Command("ledger.data.xmlSource", "data/external", ct => XmlSourceAsync(ct)),
        Command("ledger.data.provider", "data/external", ct => ProviderAsync(ct)),
        Command("ledger.data.providerRefresh", "data/external", ct => RefreshAsync(ct)),
    ];

    private CommandDefinition Command(string id, string path, Func<CancellationToken, Task> run) => Prototype(id) with
    {
        IconKey = id,
        MenuPath = path,
        Surface = CommandSurface.Advanced,
        RequiredState = DocumentState.DocumentOpen,
        ForbiddenState = DocumentState.ReadOnly,
        EnabledWhen = _ => Context.HasTarget
            && (id != "ledger.data.providerRefresh" || Context.Workbook?.ExternalData.IsEmpty == false),
        Execute = (_, ct) => run(ct),
    };

    private async Task CommentChangeAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var address = Context.Selection.Start;

        var change = workbook.Changes.At(Context.SheetIndex, address);

        if (change is null)
        {
            status.Report(texts["review.noChange"], NotificationSeverity.Warning);
            return;
        }

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(
                texts.Format("review.changeAt", address.ToA1(), change.Author),
                [new TextField("comment", texts["review.comment"], change.Comment ?? string.Empty)],
                texts),
            new DialogOptions { TitleKey = "command.ledger.review.commentChange.title" }, ct)
            .ConfigureAwait(true);

        if (values is null) return;

        Checkpointed(checkpoints, texts["command.ledger.review.commentChange.title"], () =>
        {
            workbook.Changes.Annotate(change, values["comment"] as string);
            return true;
        });

        Context.RequestRefresh();
        status.Report(texts["review.commented"]);
    }

    private async Task ProtectChangesAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var changes = workbook.Changes;

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(
                texts["review.protect.explanation"],
                [
                    new CheckField("protected", texts["review.protect.enabled"], changes.IsProtected),
                    new TextField("password", texts["protect.password"], string.Empty),
                ],
                texts),
            new DialogOptions { TitleKey = "command.ledger.review.protectChanges.title" }, ct)
            .ConfigureAwait(true);

        if (values is null) return;

        var protect = values["protected"] is true;
        var password = values.GetValueOrDefault("password") as string;

        if (changes.IsProtected && !changes.AcceptsProtection(password))
        {
            status.Report(texts["protect.wrongPassword"], NotificationSeverity.Error);
            return;
        }

        var before = (changes.IsProtected, changes.ProtectionHash);
        var after = protect
            ? (true, password is { Length: > 0 } ? WorkbookProtection.Hash(password) : changes.ProtectionHash)
            : (false, (string?)null);

        if (before == after) return;

        OutsideHistory(() =>
        {
            changes.IsProtected = after.Item1;
            changes.ProtectionHash = after.Item2;
        });

        status.Report(texts[changes.IsProtected ? "review.protected" : "review.unprotected"]);
    }

    private async Task<ExternalDocument?> OpenAsync(string location, ExternalDataFormat format, CancellationToken ct)
    {
        try
        {
            return await external.OpenAsync(location, format, DocumentFolder(), ct).ConfigureAwait(true);
        }
        catch (Exception exception) when (external.IsFailure(exception))
        {
            status.Report(texts.Format("externalData.failed", exception.Message), NotificationSeverity.Error);
            return null;
        }
    }

    private string? DocumentFolder() =>
        workspace.Active?.Path is { Length: > 0 } path ? Path.GetDirectoryName(path) : null;

    private PathField Location(string key, string label, string initial, string titleKey, params string[] extensions) =>
        new(key, label, initial, async ct =>
        {
            await using var chosen = await files.OpenFileAsync(texts[titleKey],
                [new PulseOffice.Platform.FileFilter(label, extensions)], ct).ConfigureAwait(true);
            return chosen?.Path;
        }, "https://");

    private PulseOffice.UI.Controls.Dialogs.StepStrip Steps(int current) =>
        new([texts["externalData.step.source"], texts["externalData.step.table"]], current,
            PulseOffice.UI.Theming.DesignTokens.For(PulseOffice.UI.Theming.InterfaceDensity.Compact), texts["dialog.step.accessible"]);

    private async Task<string?> ChooseTableAsync(ExternalDocument document, string titleKey, string initial, CancellationToken ct, bool stepped = false)
    {
        IReadOnlyList<string> tables;
        try
        {
            tables = document.Tables;
        }
        catch (Exception exception) when (external.IsFailure(exception))
        {
            status.Report(texts["externalData.unreadable"], NotificationSeverity.Error);
            return null;
        }

        if (tables.Count == 0)
        {
            status.Report(texts["externalData.noTables"], NotificationSeverity.Warning);
            return null;
        }

        var cache = new Dictionary<string, IReadOnlyList<IReadOnlyList<string>>>(StringComparer.Ordinal);
        IReadOnlyList<IReadOnlyList<string>> Rows(string table)
        {
            if (cache.TryGetValue(table, out var rows)) return rows;
            try
            {
                rows = document.Read(table).Rows;
            }
            catch (Exception exception) when (external.IsFailure(exception))
            {
                rows = [];
            }
            return cache[table] = rows;
        }

        Avalonia.Controls.Control view = new PulseOffice.Data.UI.TabularPreviewView(
            texts["externalData.chooseTable.explanation"],
            [.. tables.Select(table => new PulseOffice.Data.UI.TabularPreviewChoice(table, TableLabel(table)))],
            initial, Rows, snapshots, renderer, texts);
        if (stepped) view = new PulseOffice.UI.Controls.Dialogs.SteppedDialog(Steps(1), view);
        var chosen = await dialogs.ShowAsync<string>(
            view,
            new DialogOptions { TitleKey = titleKey }, ct).ConfigureAwait(true);

        return chosen;
    }

    private TabularData? Read(ExternalDocument document, string table)
    {
        try
        {
            return document.Read(table);
        }
        catch (Exception exception) when (external.IsFailure(exception))
        {
            status.Report(texts["externalData.unreadable"], NotificationSeverity.Error);
            return null;
        }
    }

    private string TableLabel(string table) =>
        table == HtmlTabularSource.AllTables ? texts["externalData.allTables"]
        : table.StartsWith("HTML_", StringComparison.Ordinal) ? texts.Format("externalData.htmlTable", table[5..])
        : table.Length == 0 ? texts["externalData.wholeFile"]
        : table;

    private int IndexOf(IReadOnlyList<string> items, string item)
    {
        for (var index = 0; index < items.Count; index++)
            if (items[index] == item) return index;
        return 0;
    }

    private int Seconds(object value)
    {
        var seconds = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
        return double.IsFinite(seconds) ? (int)Math.Clamp(Math.Round(seconds), 0, 86_400 * 7) : 0;
    }

    private int Index(object value) => (int)Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);

    private bool Protected(Worksheet sheet)
    {
        if (!sheet.Protection.IsEnabled) return false;
        status.Report(texts["status.protection.locked"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
        return true;
    }

    private bool TryWrite(Worksheet sheet, Func<CellRange> write, out CellRange area)
    {
        area = default;
        if (Protected(sheet)) return false;
        try
        {
            area = write();
            return true;
        }
        catch (InvalidOperationException)
        {
            status.Report(texts["externalData.tooLarge"], NotificationSeverity.Error);
            return false;
        }
    }

    private async Task ExternalLinksAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var sheetIndex = Context.SheetIndex;
        var anchor = Context.Selection.Start;
        var existing = workbook.ExternalData.AreaLinks.FirstOrDefault(link => link.SheetIndex == sheetIndex && link.Area.Contains(anchor));
        if (existing is not null) anchor = existing.Anchor;

        ExternalDataFormat[] formats = [ExternalDataFormat.Html, ExternalDataFormat.Csv];
        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new PulseOffice.UI.Controls.Dialogs.SteppedDialog(Steps(0), nextLabel: texts["dialog.step.next"], inner: new FormPromptView(
                texts.Format("externalLinks.explanation", anchor.ToA1()),
                [
                    Location("address", texts["externalLinks.url"], existing?.Url ?? string.Empty, "command.ledger.data.externalLinks.title", ".html", ".htm", ".csv", ".txt"),
                    new ChoiceField("format", texts["externalData.format"],
                        [texts["externalData.format.Html"], texts["externalData.format.Csv"]],
                        existing?.Format == ExternalDataFormat.Csv ? 1 : 0),
                    new NumberField("refresh", texts["externalLinks.refresh"], existing?.RefreshSeconds ?? 0, 0, 86_400 * 7, 60),
                    new RangeField("destination", texts["xmlSource.target"], anchor.ToA1(), sources.For(Dialogs.ReferenceShape.Cell)),
                ],
                texts)),
            new DialogOptions { TitleKey = "command.ledger.data.externalLinks.title" }, ct)
            .ConfigureAwait(true);

        if (values is null || values["address"] is not string { Length: > 0 } url) return;
        if (reader.ReadLocal(workbook, values["destination"] as string ?? string.Empty, sheetIndex, Dialogs.ReferenceShape.Cell) is not { Start: var chosenAnchor })
        {
            status.Report(texts["xmlSource.badTarget"], NotificationSeverity.Error);
            return;
        }

        anchor = chosenAnchor;
        var format = formats[Index(values["format"])];
        if (await OpenAsync(url, format, ct).ConfigureAwait(true) is not { } document) return;
        if (await ChooseTableAsync(document, "command.ledger.data.externalLinks.title", existing?.Source ?? string.Empty, ct, stepped: true).ConfigureAwait(true) is not { } table) return;

        if (Read(document, table) is not { } data) return;
        var sheet = workbook.Sheets[sheetIndex];

        var written = Checkpointed(checkpoints, texts["command.ledger.data.externalLinks.title"], () =>
        {
            if (!TryWrite(sheet, () => writer.Write(sheet, anchor, data.Rows, existing?.Area), out var area)) return false;
            workbook.ExternalData.Replace(new AreaLink
            {
                SheetIndex = sheetIndex,
                Anchor = anchor,
                Url = url.Trim(),
                Format = format,
                Source = table,
                RefreshSeconds = Seconds(values["refresh"]),
                Columns = area.End.Column - area.Start.Column + 1,
                Rows = area.End.Row - area.Start.Row + 1,
            });
            return true;
        }, done => done);

        Context.RequestRefresh();
        if (!written) return;
        schedule.Trust(workbook);
        status.Report(texts.Format("externalLinks.loaded", data.RowCount, anchor.ToA1()), NotificationSeverity.Success);
    }

    private async Task StreamsAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var current = workbook.ExternalData.Stream;
        var range = current?.Range ?? Context.Selection;

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new PulseOffice.UI.Controls.Dialogs.SteppedDialog(Steps(0), nextLabel: texts["dialog.step.next"], inner: new FormPromptView(
                texts["streams.explanation"],
                [
                    Location("address", texts["streams.url"], current?.Url ?? string.Empty, "command.ledger.data.streams.title", ".csv", ".txt"),
                    new RangeField("range", texts["streams.range"], range.ToString(), sources.For(Dialogs.ReferenceShape.Range)),
                    new PulseOffice.UI.Controls.Choices.IconChoiceField("mode", texts["streams.mode"],
                        [
                            new PulseOffice.UI.Controls.Choices.IconChoice("bottom", "streams.mode.bottom", texts["streams.mode.bottom"]),
                            new PulseOffice.UI.Controls.Choices.IconChoice("top", "streams.mode.top", texts["streams.mode.top"]),
                        ], kit.Icons, current?.Insertion == StreamInsertion.Top ? 1 : 0) { ShowLabels = true },
                    new CheckField("emptyLine", texts["streams.emptyLine"], current?.RefreshOnEmptyLine ?? false),
                ],
                texts)),
            new DialogOptions { TitleKey = "command.ledger.data.streams.title" }, ct).ConfigureAwait(true);

        if (values is null || values["address"] is not string { Length: > 0 } url) return;
        if (reader.ReadLocal(workbook, values["range"] as string ?? string.Empty, Context.SheetIndex, Dialogs.ReferenceShape.Range) is not { } target)
        {
            status.Report(texts["streams.badRange"], NotificationSeverity.Error);
            return;
        }

        var stream = new DataStreamSource
        {
            Url = url.Trim(),
            SheetIndex = current?.SheetIndex ?? Context.SheetIndex,
            Range = target,
            Insertion = Index(values["mode"]) == 1 ? StreamInsertion.Top : StreamInsertion.Bottom,
            RefreshOnEmptyLine = values["emptyLine"] is true,
        };

        if (await OpenAsync(stream.Url, ExternalDataFormat.Csv, ct).ConfigureAwait(true) is not { } document) return;
        if (await ChooseTableAsync(document, "command.ledger.data.streams.title", string.Empty, ct, stepped: true).ConfigureAwait(true) is not { } table) return;
        if (Read(document, table) is not { } data) return;

        if (Protected(workbook.Sheets[stream.SheetIndex])) return;

        Checkpointed(checkpoints, texts["command.ledger.data.streams.title"], () =>
        {
            if (current is not null && current.SheetIndex < workbook.Sheets.Count
                && (current.Range != stream.Range || current.SheetIndex != stream.SheetIndex)
                && !workbook.Sheets[current.SheetIndex].Protection.IsEnabled)
                writer.WriteStream(workbook.Sheets[current.SheetIndex], current.Range, [], current.Insertion);
            writer.WriteStream(workbook.Sheets[stream.SheetIndex], stream.Range, data.Rows, stream.Insertion);
            workbook.ExternalData.Stream = stream;
            return true;
        });

        Context.RequestRefresh();
        status.Report(texts.Format("streams.loaded", Math.Min(data.RowCount, target.End.Row - target.Start.Row + 1)), NotificationSeverity.Success);
    }

    private async Task XmlSourceAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new PulseOffice.UI.Controls.Dialogs.SteppedDialog(Steps(0), nextLabel: texts["dialog.step.next"], inner: new FormPromptView(
                texts["xmlSource.explanation"],
                [
                    Location("file", texts["xmlSource.file"], string.Empty, "command.ledger.data.xmlSource.title", ".xml"),
                    new RangeField("destination", texts["xmlSource.target"], Context.Selection.Start.ToA1(), sources.For(Dialogs.ReferenceShape.Cell)),
                ],
                texts)),
            new DialogOptions { TitleKey = "command.ledger.data.xmlSource.title" }, ct).ConfigureAwait(true);

        if (values is null || values["file"] is not string { Length: > 0 } file) return;
        if (reader.ReadLocal(workbook, values["destination"] as string ?? string.Empty, Context.SheetIndex, Dialogs.ReferenceShape.Cell) is not { Start: var destination })
        {
            status.Report(texts["xmlSource.badTarget"], NotificationSeverity.Error);
            return;
        }

        if (await OpenAsync(file, ExternalDataFormat.Xml, ct).ConfigureAwait(true) is not { } document) return;
        if (await ChooseTableAsync(document, "command.ledger.data.xmlSource.title", string.Empty, ct, stepped: true).ConfigureAwait(true) is not { } path) return;

        if (Read(document, path) is not { } data) return;
        if (data.RowCount == 0)
        {
            status.Report(texts["externalData.noTables"], NotificationSeverity.Warning);
            return;
        }

        var sheet = workbook.Sheets[Context.SheetIndex];
        var written = Checkpointed(checkpoints, texts["command.ledger.data.xmlSource.title"],
            () => TryWrite(sheet, () => writer.Write(sheet, destination, data.Rows, null), out _), done => done);

        Context.RequestRefresh();
        if (written) status.Report(texts.Format("xmlSource.imported", data.RowCount - 1, destination.ToA1()), NotificationSeverity.Success);
    }

    private async Task ProviderAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var existingRange = workbook.DatabaseRanges.At(Context.SheetIndex, Context.Selection.Start);
        var existing = existingRange is null ? null : workbook.ExternalData.Mappings.FirstOrDefault(mapping =>
            string.Equals(mapping.DatabaseName, existingRange.Name, StringComparison.CurrentCultureIgnoreCase));
        ExternalDataFormat[] kinds = [ExternalDataFormat.Csv, ExternalDataFormat.Html, ExternalDataFormat.Xml];

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new PulseOffice.UI.Controls.Dialogs.SteppedDialog(Steps(0), nextLabel: texts["dialog.step.next"], inner: new FormPromptView(
                texts["provider.explanation"],
                [
                    new PulseOffice.UI.Controls.Choices.IconChoiceField("kind", texts["provider.kind"],
                        [.. kinds.Select(kind => new PulseOffice.UI.Controls.Choices.IconChoice(kind.ToString(), "externalData.kind." + kind, texts["externalData.format." + kind]))],
                        kit.Icons, existing is null ? 0 : Array.IndexOf(kinds, existing.Provider)) { ShowLabels = true },
                    Location("address", texts["provider.url"], existing?.Url ?? string.Empty, "command.ledger.data.provider.title", ".csv", ".txt", ".html", ".htm", ".xml"),
                    new TextField("name", texts["provider.database"], existingRange?.Name ?? NextDatabaseName(workbook)),
                    new RangeField("destination", texts["provider.target"], (existingRange?.Range.Start ?? Context.Selection.Start).ToA1(), sources.For(Dialogs.ReferenceShape.Cell)),
                ],
                texts)),
            new DialogOptions { TitleKey = "command.ledger.data.provider.title" }, ct).ConfigureAwait(true);

        if (values is null || values["address"] is not string { Length: > 0 } url) return;
        if (values["name"] is not string { Length: > 0 } entered || entered.Trim() is var name && !names.IsValid(name))
        {
            status.Report(texts["provider.badName"], NotificationSeverity.Error);
            return;
        }
        if (reader.ReadLocal(workbook, values["destination"] as string ?? string.Empty, Context.SheetIndex, Dialogs.ReferenceShape.Cell) is not { Start: var destination })
        {
            status.Report(texts["xmlSource.badTarget"], NotificationSeverity.Error);
            return;
        }

        if (workbook.DatabaseRanges.Find(name) is { } taken && !ReferenceEquals(taken, existingRange)
            && !workbook.ExternalData.Mappings.Any(mapping => string.Equals(mapping.DatabaseName, name, StringComparison.CurrentCultureIgnoreCase)))
        {
            status.Report(texts["provider.badName"], NotificationSeverity.Error);
            return;
        }

        var kind = kinds[Index(values["kind"])];
        if (await OpenAsync(url, kind, ct).ConfigureAwait(true) is not { } document) return;
        if (await ChooseTableAsync(document, "command.ledger.data.provider.title", existing?.Id ?? string.Empty, ct, stepped: true).ConfigureAwait(true) is not { } table) return;

        if (Read(document, table) is not { } data) return;
        var sheetIndex = existingRange?.SheetIndex ?? Context.SheetIndex;
        var mapping = new DataMapping { Id = table, Url = url.Trim(), Provider = kind, DatabaseName = name };

        var written = Checkpointed(checkpoints, texts["command.ledger.data.provider.title"],
            () => ApplyMapping(workbook, mapping, sheetIndex, destination, data, existingRange), done => done);

        Context.RequestRefresh();
        if (written) status.Report(texts.Format("provider.loaded", data.RowCount, name), NotificationSeverity.Success);
    }

    private bool ApplyMapping(Workbook workbook, DataMapping mapping, int sheetIndex, CellAddress destination, TabularData data, DatabaseRange? previous)
    {
        var sheet = workbook.Sheets[sheetIndex];
        if (!TryWrite(sheet, () => writer.Write(sheet, destination, data.Rows, previous?.SheetIndex == sheetIndex ? previous.Range : null), out var area)) return false;

        if (previous is not null && !string.Equals(previous.Name, mapping.DatabaseName, StringComparison.CurrentCultureIgnoreCase))
            workbook.DatabaseRanges.Remove(previous.Name);
        workbook.DatabaseRanges.Define((workbook.DatabaseRanges.Find(mapping.DatabaseName) ?? new DatabaseRange
        {
            Name = mapping.DatabaseName,
            SheetIndex = sheetIndex,
            Range = area,
        }) with { SheetIndex = sheetIndex, Range = area, HasHeader = true });
        workbook.ExternalData.Replace(mapping);
        return true;
    }

    private string NextDatabaseName(Workbook workbook)
    {
        for (var index = 1; ; index++)
        {
            var name = "Data" + index;
            if (workbook.DatabaseRanges.Find(name) is null) return name;
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var catalog = workbook.ExternalData;
        var links = new List<(AreaLink Link, TabularData Data)>();
        var mappings = new List<(DataMapping Mapping, TabularData Data)>();
        (DataStreamSource Stream, TabularData Data)? stream = null;
        var failures = 0;

        foreach (var link in catalog.AreaLinks.ToList())
        {
            if (await OpenAsync(link.Url, link.Format, ct).ConfigureAwait(true) is { } document && Read(document, link.Source) is { } data) links.Add((link, data));
            else failures++;
        }

        if (catalog.Stream is { } source)
        {
            if (await OpenAsync(source.Url, ExternalDataFormat.Csv, ct).ConfigureAwait(true) is { } document && Read(document, string.Empty) is { } data) stream = (source, data);
            else failures++;
        }

        foreach (var mapping in catalog.Mappings.ToList())
        {
            if (await OpenAsync(mapping.Url, mapping.Provider, ct).ConfigureAwait(true) is { } document && Read(document, mapping.Id) is { } data) mappings.Add((mapping, data));
            else failures++;
        }

        if (links.Count + mappings.Count == 0 && stream is null)
        {
            status.Report(texts.Format("provider.refreshedPartly", 0, failures), NotificationSeverity.Warning);
            return;
        }

        var refreshed = Checkpointed(checkpoints, texts["command.ledger.data.providerRefresh.title"], () =>
        {
            var count = 0;
            foreach (var (link, data) in links)
            {
                if (link.SheetIndex >= workbook.Sheets.Count) continue;
                if (!TryWrite(workbook.Sheets[link.SheetIndex], () => writer.Write(workbook.Sheets[link.SheetIndex], link.Anchor, data.Rows, link.Area), out var area)) continue;
                catalog.Replace(link with { Columns = area.End.Column - area.Start.Column + 1, Rows = area.End.Row - area.Start.Row + 1 });
                count++;
            }

            if (stream is { } streamed && streamed.Stream.SheetIndex < workbook.Sheets.Count
                && !Protected(workbook.Sheets[streamed.Stream.SheetIndex]))
            {
                writer.WriteStream(workbook.Sheets[streamed.Stream.SheetIndex], streamed.Stream.Range, streamed.Data.Rows, streamed.Stream.Insertion);
                count++;
            }

            foreach (var (mapping, data) in mappings)
            {
                if (workbook.DatabaseRanges.Find(mapping.DatabaseName) is not { } range || range.SheetIndex >= workbook.Sheets.Count) continue;
                if (ApplyMapping(workbook, mapping, range.SheetIndex, range.Range.Start, data, range)) count++;
            }

            return count;
        }, count => count > 0);

        Context.RequestRefresh();
        schedule.Trust(workbook);
        status.Report(failures == 0
            ? texts.Format("provider.refreshed", refreshed)
            : texts.Format("provider.refreshedPartly", refreshed, failures),
            failures == 0 ? NotificationSeverity.Success : NotificationSeverity.Warning);
    }
}
