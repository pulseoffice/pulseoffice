using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Formats;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerCompareCommands(
    IActiveSheetContext context,
    ISpreadsheetFormatRegistry formats,
    IWorkbookComparer comparer,
    IWorkbookMerger merger,
    IFileDialogService files,
    IStatusBarService status,
    ITextCatalog texts,
    PulseOffice.Sheets.Engine.Operations.IWorkbookCheckpoints checkpoints,
    PulseOffice.Foundation.Identity.IUserIdentity identity) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("ledger.edit.compare", "edit/compare", ct => CompareAsync(ct)),
        Command("ledger.edit.merge", "edit/compare", ct => MergeAsync(ct)),
    ];

    private CommandDefinition Command(string id, string path, Func<CancellationToken, Task> run) => Prototype(id) with
    {
        IconKey = id,
        MenuPath = path,
        Surface = CommandSurface.Advanced,
        RequiredState = DocumentState.DocumentOpen,
        ForbiddenState = DocumentState.ReadOnly,
        EnabledWhen = _ => Context.HasTarget,
        Execute = (_, ct) => run(ct),
    };

    private async Task CompareAsync(CancellationToken ct)
    {
        if (await OtherAsync(ct).ConfigureAwait(true) is not (var mine, { } theirs)) return;

        var differences = comparer.Compare(mine, theirs, identity.Name);

        if (differences.IsEmpty)
        {
            status.Report(texts["compare.identical"], NotificationSeverity.Success);
            return;
        }

        Checkpointed(checkpoints, texts["command.ledger.edit.compare.title"], () =>
        {
            mine.Changes.Import(differences.CellChanges);
            mine.Changes.ShowChanges = true;
            return true;
        });

        Context.RequestRefresh();

        status.Report(texts.Format("compare.found",
            differences.CellChanges.Count, differences.SheetChanges.Count), NotificationSeverity.Success);
    }

    private async Task MergeAsync(CancellationToken ct)
    {
        if (await OtherAsync(ct).ConfigureAwait(true) is not (var mine, { } theirs)) return;

        var differences = comparer.Compare(mine, theirs, identity.Name);

        if (differences.IsEmpty)
        {
            status.Report(texts["compare.identical"], NotificationSeverity.Success);
            return;
        }

        var merged = Checkpointed(checkpoints, texts["command.ledger.edit.merge.title"], () =>
        {
            var count = merger.Merge(mine, differences);
            mine.Changes.ShowChanges = true;
            return count;
        });

        Context.RequestRefresh();

        status.Report(texts.Format("merge.done", merged), NotificationSeverity.Success);
    }

    private async Task<(Workbook Mine, Workbook? Theirs)> OtherAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } mine) return (new Workbook(), null);

        var chosen = await files.OpenFileAsync(
            texts["compare.choose"],
            [.. formats.ReadableFormats.Select(format =>
                new FileFilter(format.DisplayName, [.. format.Extensions.Select(item => "*" + item)]))],
            ct).ConfigureAwait(true);

        if (chosen is null) return (mine, null);

        await using var stream = await chosen.OpenRead(ct).ConfigureAwait(true);

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct).ConfigureAwait(true);
        buffer.Position = 0;

        var read = formats.Read(chosen.Path, buffer);

        if (!read.Succeeded)
        {
            status.Report(texts.Format("compare.unreadable", chosen.Name), NotificationSeverity.Error);
            return (mine, null);
        }

        return (mine, read.Value);
    }
}
