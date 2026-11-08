using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Objects;

public sealed record MediaItem(string Id, string Name, string MediaType, ReadOnlyMemory<byte> Data, bool FromFile = false);

public interface IMediaPlayer
{
    Task<MediaItem?> ShowAsync(IReadOnlyList<MediaItem> items, string titleKey, CancellationToken ct = default);
}

public sealed class MediaPlayer(IDialogService dialogs, IFileDialogService files, IEmbeddedFileLauncher launcher, IStatusBarService status, ITextCatalog texts, IDiagnosticTexts diagnostics) : IMediaPlayer
{
    public Task<MediaItem?> ShowAsync(IReadOnlyList<MediaItem> items, string titleKey, CancellationToken ct = default) =>
        dialogs.ShowAsync<MediaItem>(
            new MediaPlayerView(items, files, launcher, status, texts, diagnostics),
            new DialogOptions { TitleKey = titleKey, PreferredWidth = 560 },
            ct);
}

public sealed class MediaPlayerView : UserControl, IDialogContent, IDialogFooterOwner
{
    private static readonly string[] Playable = [".mp4", ".mov", ".m4v", ".webm", ".avi", ".mkv", ".mp3", ".wav", ".ogg", ".m4a", ".aac", ".flac"];

    private readonly List<MediaItem> _items;
    private readonly ListBox _list = new() { Height = 220 };
    private readonly IEmbeddedFileLauncher _launcher;
    private readonly IStatusBarService _status;
    private readonly ITextCatalog _texts;
    private readonly IDiagnosticTexts _diagnostics;
    private readonly Button _play;
    private readonly DialogFooter _footer;

    public event EventHandler<object?>? Completed;

    public Control? Footer => _footer;

    public MediaPlayerView(IReadOnlyList<MediaItem> items, IFileDialogService files, IEmbeddedFileLauncher launcher, IStatusBarService status, ITextCatalog texts, IDiagnosticTexts diagnostics, InterfaceDensity density = InterfaceDensity.Compact)
    {
        _items = [.. items];
        _launcher = launcher;
        _status = status;
        _texts = texts;
        _diagnostics = diagnostics;
        var metrics = DesignTokens.For(density);

        _play = new Button { Content = "▶  " + texts["media.player.play"] };
        _play.Click += async (_, _) => await PlayAsync().ConfigureAwait(true);

        var open = new Button { Content = texts["media.player.open"] };
        open.Click += async (_, _) => await OpenAsync(files, open).ConfigureAwait(true);

        _footer = new DialogFooter(density)
            .WithCancel(texts["document.close"], () => Completed?.Invoke(this, null))
            .WithAction(texts["media.player.insert"], () =>
            {
                if (Chosen() is { } item) Completed?.Invoke(this, item);
            }, primary: true);

        _list.SelectionChanged += (_, _) => _play.IsEnabled = Chosen() is not null;
        Refill(_items.Count > 0 ? 0 : -1);

        Content = new StackPanel
        {
            Spacing = metrics.SpacingMedium,
            Children =
            {
                new TextBlock { Text = texts["media.player.explanation"], TextWrapping = TextWrapping.Wrap }
                    .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                _list,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingSmall, Children = { _play, open } },
            },
        };
    }

    private MediaItem? Chosen() => _list.SelectedIndex is var index && index >= 0 && index < _items.Count ? _items[index] : null;

    private void Refill(int selected)
    {
        _list.ItemsSource = _items.Select(Row).ToList();
        _list.SelectedIndex = selected;
        _play.IsEnabled = Chosen() is not null;
    }

    private Control Row(MediaItem item) => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Spacing = 8,
        Margin = new Thickness(4, 2),
        Children =
        {
            new TextBlock { Text = item.MediaType.StartsWith("video", StringComparison.Ordinal) ? "🎬" : "♪", VerticalAlignment = VerticalAlignment.Center },
            new TextBlock { Text = item.Name, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center },
            new TextBlock { Text = $"{item.MediaType} · {Size(item.Data.Length)}", VerticalAlignment = VerticalAlignment.Center }
                .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
        },
    };

    private string Size(long bytes) => bytes >= 1024 * 1024
        ? (bytes / 1024.0 / 1024.0).ToString("0.0", CultureInfo.CurrentCulture) + " MB"
        : Math.Max(1, bytes / 1024).ToString(CultureInfo.CurrentCulture) + " kB";

    private async Task OpenAsync(IFileDialogService files, Button open)
    {
        open.IsEnabled = false;
        string? name = null;
        try
        {
            var chosen = await files.OpenFileAsync(_texts["media.player.open"], [new FileFilter(_texts["media.player.files"], Playable)]).ConfigureAwait(true);
            if (chosen?.OpenRead is null) return;
            name = chosen.Name;
            await using var stream = await chosen.OpenRead(default).ConfigureAwait(true);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer).ConfigureAwait(true);
            _items.Add(new MediaItem(Guid.NewGuid().ToString("N"), chosen.Name, MediaTypes.Of(chosen.Name), buffer.ToArray(), FromFile: true));
            Refill(_items.Count - 1);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _status.Report(_texts.Format("status.open.failed", name ?? string.Empty), PulseOffice.UI.Controls.Notifications.NotificationSeverity.Error);
        }
        finally
        {
            open.IsEnabled = true;
        }
    }

    private async Task PlayAsync()
    {
        if (Chosen() is not { } item) return;
        try
        {
            var opened = await _launcher.OpenAsync(item.Data, item.Name).ConfigureAwait(true);
            _status.Report(opened.Succeeded ? _texts.Format("media.player.playing", item.Name) : _diagnostics.Describe(opened.Diagnostics[0]));
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _status.Report(_texts.Format("status.open.failed", item.Name), PulseOffice.UI.Controls.Notifications.NotificationSeverity.Error);
        }
    }
}

public static class MediaTypes
{
    public static string Of(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".mp4" or ".m4v" => "video/mp4",
        ".mov" => "video/quicktime",
        ".webm" => "video/webm",
        ".avi" => "video/x-msvideo",
        ".mkv" => "video/x-matroska",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".ogg" => "audio/ogg",
        ".m4a" or ".aac" => "audio/mp4",
        ".flac" => "audio/flac",
        _ => "application/octet-stream",
    };
}
