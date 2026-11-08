using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using PulseOffice.Settings;

namespace PulseOffice.Shell;

public sealed class AutosaveCoordinator : IDisposable
{
    private readonly IDocumentWorkspace _workspace;
    private readonly IDocumentRecovery _recovery;
    private readonly ISettingsService _settings;
    private readonly ILogger<AutosaveCoordinator> _logger;
    private readonly DispatcherTimer _timer = new();
    private bool _running;

    public AutosaveCoordinator(
        IDocumentWorkspace workspace,
        IDocumentRecovery recovery,
        ISettingsService settings,
        ILogger<AutosaveCoordinator> logger)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _timer.Tick += async (_, _) => await TickAsync();

        _settings.Changed += OnSettingChanged;
        Reschedule();
    }

    private void OnSettingChanged(object? sender, SettingChangedEventArgs e)
    {
        if (e.Key == ShellSettingKeys.AutoSaveMinutes) Reschedule();
    }

    private void Reschedule()
    {
        var minutes = (int)Math.Clamp(_settings.Get<long>(ShellSettingKeys.AutoSaveMinutes), 0L, 24L * 60L);

        _timer.Stop();
        if (minutes <= 0) return;

        _timer.Interval = TimeSpan.FromMinutes(minutes);
        _timer.Start();
    }

    private async Task TickAsync()
    {
        if (_running) return;
        _running = true;

        try
        {
            foreach (var document in _workspace.Documents.Where(d => d.IsModified).ToList())
            {
                try
                {
                    await _recovery.SnapshotAsync(document).ConfigureAwait(true);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    _logger.LogWarning(ex, "Autosave failed for {Title}", document.Title);
                }
            }
        }
        finally
        {
            _running = false;
        }
    }

    public void Dispose()
    {
        _settings.Changed -= OnSettingChanged;
        _timer.Stop();
    }
}
