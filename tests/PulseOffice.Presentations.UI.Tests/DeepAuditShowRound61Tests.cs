using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Foundation.Network;
using PulseOffice.Platform;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeepAuditShowRound61Tests
{
    private sealed class Status : IStatusBarService
    {
        public List<(string Message, NotificationSeverity Severity)> Reports { get; } = [];
        public IReadOnlyList<StatusSegment> Segments => [];
        public IReadOnlyList<BackgroundActivity> Activities => [];
        public event EventHandler? Changed { add { } remove { } }
        public StatusSegment Register(string id, StatusSegmentKind kind) => throw new NotSupportedException();
        public void Remove(string id) { }
        public void Report(string message, TimeSpan? duration = null) => Reports.Add((message, NotificationSeverity.Info));
        public void DismissTransient() { }
        public void Report(Notification notification) => Reports.Add((notification.Message, notification.Severity));
        public void Report(string message, NotificationSeverity severity, NotificationAction? action = null) => Reports.Add((message, severity));
        public BackgroundActivity BeginActivity(string description, CancellationTokenSource? cancellation = null) => throw new NotSupportedException();
        public void EndActivity(BackgroundActivity activity) { }
    }

    private sealed class Opener : IShellOpener
    {
        public List<string> Opened { get; } = [];
        public OperationResult<bool> OpenUrl(string url) { Opened.Add(url); return OperationResult<bool>.Ok(true); }
        public OperationResult<bool> OpenFile(string path) { Opened.Add(path); return OperationResult<bool>.Ok(true); }
    }

    private sealed class Launcher(Func<string, OperationResult<bool>> open) : IEmbeddedFileLauncher
    {
        public List<string> Names { get; } = [];

        public Task<OperationResult<bool>> OpenAsync(ReadOnlyMemory<byte> data, string fileName, CancellationToken ct = default)
        {
            Names.Add(fileName);
            return Task.FromResult(open(fileName));
        }
    }

    private static MediaElement Embedded(string? name) => new()
    {
        Id = "m",
        Kind = MediaKind.Video,
        Data = [1, 2, 3],
        MimeType = "video/mp4",
        FileName = name,
    };

    private static (ShowMediaPlayer Player, Status Status, Launcher Launcher) Player(Func<string, OperationResult<bool>> open)
    {
        var status = new Status();
        var launcher = new Launcher(open);
        var player = new ShowMediaPlayer(
            new DeckMediaFiles(new ExecutableFileTypes(), new NetworkSharePaths()), new Opener(), launcher, status, new FakeTextCatalog());
        return (player, status, launcher);
    }

    [Fact]
    public async Task An_embedded_medium_the_launcher_refuses_is_reported_not_dropped()
    {
        var (player, status, _) = Player(_ => OperationResult<bool>.Fail(Diagnostic.Error("OPEN004", "refused")));

        await player.PlayAsync(Embedded("clip.exe"), null);

        Assert.Contains(status.Reports, report => report.Severity == NotificationSeverity.Warning);
    }

    [Fact]
    public async Task An_embedded_medium_that_cannot_be_written_is_reported_without_throwing()
    {
        var (player, status, _) = Player(_ => throw new IOException("disk full"));

        await player.PlayAsync(Embedded("clip.mp4"), null);

        Assert.Contains(status.Reports, report => report.Severity == NotificationSeverity.Warning);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_embedded_medium_without_a_name_gets_one_from_its_type(string? name)
    {
        var (player, status, launcher) = Player(_ => OperationResult<bool>.Ok(true));

        await player.PlayAsync(Embedded(name), null);

        Assert.Equal(["media.mp4"], launcher.Names);
        Assert.Empty(status.Reports);
    }
}
