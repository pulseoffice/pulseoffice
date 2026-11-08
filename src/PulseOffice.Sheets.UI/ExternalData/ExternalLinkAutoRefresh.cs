using PulseOffice.UI.Controls.Notifications;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using PulseOffice.Formats.Interchange;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI.ExternalData;

public interface IExternalLinkSchedule
{
    void Trust(Workbook workbook);

    IReadOnlyList<AreaLink> Due(Workbook workbook, DateTimeOffset now);

    void Done(Workbook workbook, AreaLink link, DateTimeOffset now);
}

public sealed class ExternalLinkSchedule : IExternalLinkSchedule
{
    private readonly ConditionalWeakTable<Workbook, Dictionary<(int Sheet, CellAddress Anchor), DateTimeOffset>> _last = [];

    public void Trust(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        _last.GetValue(workbook, _ => []);
    }

    public IReadOnlyList<AreaLink> Due(Workbook workbook, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        if (!_last.TryGetValue(workbook, out var last)) return [];

        var due = new List<AreaLink>();
        foreach (var link in workbook.ExternalData.AreaLinks.Where(link => link.RefreshSeconds > 0))
        {
            var key = (link.SheetIndex, link.Anchor);
            if (!last.TryGetValue(key, out var previous))
            {
                last[key] = now;
                continue;
            }
            if (now - previous >= TimeSpan.FromSeconds(link.RefreshSeconds)) due.Add(link);
        }
        return due;
    }

    public void Done(Workbook workbook, AreaLink link, DateTimeOffset now)
    {
        if (_last.TryGetValue(workbook, out var last)) last[(link.SheetIndex, link.Anchor)] = now;
    }
}

public sealed class ExternalLinkAutoRefresh(
    IActiveSheetContext context,
    IExternalLinkSchedule schedule,
    IExternalDataService external,
    IExternalDataWriter writer,
    IAutomaticRefreshHistory refreshHistory,
    PulseOffice.Shell.IDocumentWorkspace workspace,
    IStatusBarService status,
    ITextCatalog texts,
    TimeProvider clock) : IDisposable
{
    private DispatcherTimer? _timer;
    private bool _busy;

    public void Start()
    {
        if (_timer is not null) return;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += async (_, _) => await TickAsync().ConfigureAwait(true);
        _timer.Start();
    }

    private async Task TickAsync()
    {
        if (_busy || context.Workbook is not { } workbook || workspace.Active is not { IsReadOnly: false } document) return;

        var due = schedule.Due(workbook, clock.GetUtcNow());
        if (due.Count == 0) return;

        _busy = true;
        try
        {
            var folder = document.Path is { Length: > 0 } path ? Path.GetDirectoryName(path) : null;
            foreach (var link in due)
            {
                schedule.Done(workbook, link, clock.GetUtcNow());
                await RefreshAsync(workbook, link, folder).ConfigureAwait(true);
            }
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task RefreshAsync(Workbook workbook, AreaLink link, string? folder)
    {
        TabularData data;
        try
        {
            var opened = await external.OpenAsync(link.Url, link.Format, folder, CancellationToken.None).ConfigureAwait(true);
            data = opened.Read(link.Source);
        }
        catch (Exception exception) when (external.IsFailure(exception))
        {
            status.Report(texts.Format("externalData.failed", exception.Message), NotificationSeverity.Error);
            return;
        }

        if (!ReferenceEquals(context.Workbook, workbook)) return;
        var current = workbook.ExternalData.AreaLinks.FirstOrDefault(candidate => candidate.SheetIndex == link.SheetIndex && candidate.Anchor == link.Anchor);
        if (current is null || current.SheetIndex >= workbook.Sheets.Count) return;

        var sheet = workbook.Sheets[current.SheetIndex];
        if (sheet.Protection.IsEnabled) return;

        var tooLarge = false;
        bool Write()
        {
            try
            {
                var area = writer.Write(sheet, current.Anchor, data.Rows, current.Area);
                workbook.ExternalData.Replace(current with { Columns = area.End.Column - area.Start.Column + 1, Rows = area.End.Row - area.Start.Row + 1 });
                return true;
            }
            catch (InvalidOperationException)
            {
                tooLarge = true;
                return false;
            }
        }

        var changed = context.History is { } history && context.Engine is { } engine
            ? refreshHistory.Record(history, engine, workbook, new AutomaticRefreshKey(current.SheetIndex, current.Anchor), texts["history.externalLinks.autoRefresh"], Write)
            : Write();

        if (tooLarge)
        {
            status.Report(texts["externalData.tooLarge"], NotificationSeverity.Error);
            return;
        }
        if (!changed) return;

        context.RequestRefresh();
        status.Report(texts.Format("externalLinks.autoRefreshed", current.Anchor.ToA1()));
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer = null;
    }
}
