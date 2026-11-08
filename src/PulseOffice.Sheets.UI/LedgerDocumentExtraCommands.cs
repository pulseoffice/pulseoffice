using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Sheets.Addressing;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerDocumentExtraCommands(
    IActiveSheetContext context,
    IFileDialogService files,
    IStatusBarService status,
    ITextCatalog texts,
    PulseOffice.UI.Controls.Objects.IImageLinks imageLinks,
    Queries.IQueryRefresher queries,
    PulseOffice.Sheets.Engine.Operations.ISheetSnapshots snapshots,
    PulseOffice.Platform.IAtomicFileWriter atomic,
    PulseOffice.Drawing.Assets.IRasterMediaTypes mediaTypes) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Simple("ledger.file.links", "data/external", false, ct => LinksAsync(ct)) with { RunsDocumentQueries = true, EnabledWhen = _ => Context.Workbook is { } workbook && workbook.Sheets.Any(sheet => sheet.QueryBindings.All.Count > 0 || sheet.Objects.Any(placed => placed.Content is PulseOffice.Drawing.Assets.RasterImageObject { LinkPath.Length: > 0 })) },
        Simple("ledger.image.export", "format/image", true, _ => ExportImageAsync(), () => Context.SelectedObject?.Content is Drawing.Assets.RasterImageObject),
    ];

    private CommandDefinition Simple(string id, string path, bool alwaysOn, Func<CancellationToken, Task> run, Func<bool>? applies = null) => Prototype(id) with
    {
        IconKey = id,
        MenuPath = path,
        Surface = CommandSurface.Advanced,
        RequiredState = DocumentState.DocumentOpen,
        ForbiddenState = alwaysOn ? DocumentState.None : DocumentState.ReadOnly,
        EnabledWhen = _ => Context.HasTarget && (applies?.Invoke() ?? true),
        Execute = (_, ct) => run(ct),
    };

    private async Task LinksAsync(CancellationToken ct)
    {
        while (Context.Workbook is { } workbook)
        {
            var bindings = workbook.Sheets.SelectMany((sheet, index) => sheet.QueryBindings.All.Select(binding => (Sheet: sheet, Index: index, Binding: binding))).ToList();
            var images = workbook.Sheets.SelectMany((sheet, index) => sheet.Objects
                .Where(placed => placed.Content is PulseOffice.Drawing.Assets.RasterImageObject { LinkPath.Length: > 0 })
                .Select(placed => (Sheet: sheet, Index: index, Placed: placed))).ToList();

            var described = bindings
                .Select(entry => new PulseOffice.UI.Controls.Objects.ExternalLink("q:" + entry.Binding.Id,
                    $"{entry.Sheet.Name}!{entry.Binding.Anchor.ToA1()} · {entry.Binding.DisplayName}", entry.Binding.Query.Sql, true))
                .Concat(imageLinks.Describe(images.Select(entry => ("i:" + entry.Placed.Id,
                    $"{entry.Sheet.Name} · {Path.GetFileName(((PulseOffice.Drawing.Assets.RasterImageObject)entry.Placed.Content).LinkPath)}",
                    (PulseOffice.Drawing.Assets.RasterImageObject)entry.Placed.Content))))
                .ToList();

            if (await imageLinks.ChooseAsync(described, "command.ledger.file.links.title", ct).ConfigureAwait(true) is not { } request) return;

            if (request.Id.StartsWith("q:", StringComparison.Ordinal))
            {
                if (bindings.FirstOrDefault(entry => "q:" + entry.Binding.Id == request.Id) is not { Binding: not null } target) return;
                if (request.Action == PulseOffice.UI.Controls.Objects.ExternalLinkAction.Update) await RefreshAsync(workbook, target.Sheet, target.Index, target.Binding).ConfigureAwait(true);
                else if (request.Action == PulseOffice.UI.Controls.Objects.ExternalLinkAction.Break) Unbind(target.Sheet, target.Binding);
                else status.Report(texts["links.query.source"]);
                continue;
            }

            if (images.FirstOrDefault(entry => "i:" + entry.Placed.Id == request.Id) is not { Placed: not null } found) return;
            var image = (PulseOffice.Drawing.Assets.RasterImageObject)found.Placed.Content;
            var updated = request.Action switch
            {
                PulseOffice.UI.Controls.Objects.ExternalLinkAction.Update => imageLinks.Update(image),
                PulseOffice.UI.Controls.Objects.ExternalLinkAction.ChangeSource => await imageLinks.ChangeSourceAsync(image, ct).ConfigureAwait(true),
                _ => imageLinks.Break(image),
            };
            if (updated is null)
            {
                status.Report(texts["links.unavailable"], NotificationSeverity.Error);
                continue;
            }
            Replace(found.Sheet, found.Placed, found.Placed with { Content = updated });
        }
    }

    private async Task RefreshAsync(Workbook workbook, Worksheet sheet, int index, QueryBinding binding)
    {
        var before = snapshots.Capture(sheet);
        status.Report(texts["status.query.refreshing"]);
        var outcome = await queries.RefreshAsync(workbook, sheet, binding).ConfigureAwait(true);
        if (!outcome.Succeeded || outcome.Value is not { } refreshed)
        {
            if (outcome.Diagnostics.Any(diagnostic => diagnostic.Code == Queries.QueryRefresher.DeclinedCode)) status.Report(texts["status.query.declined"]);
            else status.Report(texts.Format("status.query.failed", outcome.Diagnostics.Count), NotificationSeverity.Error);
            return;
        }

        sheet.QueryBindings.AddOrUpdate(refreshed);
        var after = snapshots.Capture(sheet);
        Context.History?.Push(new PulseOffice.Document.Commands.DelegateAction(
            texts["history.data.refresh"],
            redo: () => { if (Context.Engine is { } engine) snapshots.Restore(engine, index, after); sheet.QueryBindings.AddOrUpdate(refreshed); },
            undo: () => { if (Context.Engine is { } engine) snapshots.Restore(engine, index, before); sheet.QueryBindings.AddOrUpdate(binding); }));
        Context.RequestRefresh();
        status.Report(texts["links.refreshed"]);
    }

    private void Unbind(Worksheet sheet, QueryBinding binding)
    {
        sheet.QueryBindings.Remove(binding.Id);
        Context.History?.Push(new PulseOffice.Document.Commands.DelegateAction(
            texts["history.data.unbind"],
            redo: () => sheet.QueryBindings.Remove(binding.Id),
            undo: () => sheet.QueryBindings.AddOrUpdate(binding)));
        status.Report(texts["links.unbound"]);
    }

    private void Replace(Worksheet sheet, PlacedObject before, PlacedObject after)
    {
        var index = sheet.Objects.IndexOf(before);
        if (index < 0) return;
        sheet.Objects[index] = after;
        Context.History?.Push(new PulseOffice.Document.Commands.DelegateAction(
            texts["links.history"],
            redo: () => sheet.Objects[index] = after,
            undo: () => sheet.Objects[index] = before));
        Context.RequestRefresh();
    }

    private async Task ExportImageAsync()
    {
        if (Context.SelectedObject?.Content is not Drawing.Assets.RasterImageObject image)
        {
            status.Report(texts["image.selectFirst"], NotificationSeverity.Warning);
            return;
        }

        var chosen = await files.SaveFileAsync(
            texts["image.export.title"],
            texts["file.image"] + mediaTypes.ExtensionOf(image.MediaType),
            [new FileFilter(texts["file.image"], ["*" + mediaTypes.ExtensionOf(image.MediaType)])]).ConfigureAwait(true);

        if (chosen is null) return;

        using var content = new MemoryStream(image.Data.ToArray());
        await atomic.WriteAsync(chosen, content).ConfigureAwait(true);

        status.Report(texts.Format("image.export.done", chosen.Name), NotificationSeverity.Success);
    }
}
