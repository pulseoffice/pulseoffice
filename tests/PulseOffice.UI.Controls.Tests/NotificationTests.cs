using PulseOffice.Testing;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class ManualTime : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state, _now + dueTime);
        _timers.Add(timer);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        _now += by;
        foreach (var timer in _timers.ToList().Where(timer => timer.Due is { } due && due <= _now))
        {
            timer.Due = null;
            timer.Fire();
        }
    }

    private sealed class ManualTimer(ManualTime owner, TimerCallback callback, object? state, DateTimeOffset due) : ITimer
    {
        public DateTimeOffset? Due { get; set; } = due;

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Due = owner._now + dueTime;
            return true;
        }

        public void Dispose()
        {
            Due = null;
            owner._timers.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

public sealed class NotificationQueueTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    private static Notification Error(string text) => new(text, NotificationSeverity.Error);

    private static Notification Warning(string text) => new(text, NotificationSeverity.Warning);

    [Fact]
    public void UnMessaggioUgualeNonSiAccumulaMaRiparteIlTempo()
    {
        var queue = new NotificationQueue(NotificationPolicy.Standard);
        var first = queue.Post(Warning("Il foglio è protetto"), Start);

        var second = queue.Post(Warning("Il foglio è protetto"), Start.AddSeconds(4));

        Assert.Equal(NotificationPostKind.Bumped, second.Kind);
        Assert.Same(first.Entry, second.Entry);
        Assert.Single(queue.Entries);
        Assert.Equal(Start.AddSeconds(9), second.Entry.Expires);
    }

    [Fact]
    public void LoStessoTestoConUnaGravitaDiversaEUnAltraNotifica()
    {
        var queue = new NotificationQueue(NotificationPolicy.Standard);
        queue.Post(Warning("x"), Start);

        Assert.Equal(NotificationPostKind.Added, queue.Post(Error("x"), Start).Kind);
        Assert.Equal(2, queue.Entries.Count);
    }

    [Fact]
    public void OltreIlTettoEsceLaPiuVecchia()
    {
        var queue = new NotificationQueue(NotificationPolicy.Standard);
        var oldest = queue.Post(Warning("1"), Start).Entry;
        queue.Post(Warning("2"), Start);
        queue.Post(Warning("3"), Start);

        var fourth = queue.Post(Warning("4"), Start);

        Assert.Equal([oldest], fourth.Evicted);
        Assert.Equal(["2", "3", "4"], queue.Entries.Select(entry => entry.Notification.Message));
    }

    [Fact]
    public void GliErroriRestanoPiuALungo()
    {
        var queue = new NotificationQueue(NotificationPolicy.Standard);
        queue.Post(Warning("avviso"), Start);
        queue.Post(Error("errore"), Start);

        var afterFive = queue.Expire(Start.AddSeconds(5));
        var afterSeven = queue.Expire(Start.AddSeconds(7));

        Assert.Equal(["avviso"], afterFive.Select(entry => entry.Notification.Message));
        Assert.Equal(["errore"], afterSeven.Select(entry => entry.Notification.Message));
        Assert.Empty(queue.Entries);
    }

    [Fact]
    public void ConIlPuntatoreSopraNonScadeEPoiRiparte()
    {
        var queue = new NotificationQueue(NotificationPolicy.Standard);
        var entry = queue.Post(Warning("resta"), Start).Entry;

        queue.Hover(entry.Id, true, Start.AddSeconds(2));
        Assert.Empty(queue.Expire(Start.AddSeconds(60)));
        Assert.Null(queue.NextDeadline);

        queue.Hover(entry.Id, false, Start.AddSeconds(60));
        Assert.Empty(queue.Expire(Start.AddSeconds(64)));
        Assert.Single(queue.Expire(Start.AddSeconds(65)));
    }

    [Fact]
    public void UnMessaggioRipetutoMentreSiStaSopraNonRiparteIlTempo()
    {
        var queue = new NotificationQueue(NotificationPolicy.Standard);
        var entry = queue.Post(Warning("x"), Start).Entry;
        queue.Hover(entry.Id, true, Start);

        queue.Post(Warning("x"), Start.AddSeconds(1));

        Assert.Null(entry.Expires);
    }

    [Fact]
    public void ChiudereLaTogliePerSempre()
    {
        var queue = new NotificationQueue(NotificationPolicy.Standard);
        var entry = queue.Post(Warning("x"), Start).Entry;

        Assert.Same(entry, queue.Dismiss(entry.Id));
        Assert.Null(queue.Dismiss(entry.Id));
        Assert.Empty(queue.Entries);
    }

    [Fact]
    public void LaProssimaScadenzaELaPiuVicina()
    {
        var queue = new NotificationQueue(NotificationPolicy.Standard);
        queue.Post(Error("e"), Start);
        queue.Post(Warning("w"), Start.AddSeconds(1));

        Assert.Equal(Start.AddSeconds(6), queue.NextDeadline);
    }
}

public sealed class NotificationCenterTests
{
    [Fact]
    public void LaNotificaScadeDaSolaColTempo()
    {
        var time = new ManualTime();
        var center = new NotificationCenter(time);
        var changes = new List<NotificationChange>();
        center.Changed += (_, change) => changes.Add(change);

        center.Post(new Notification("x", NotificationSeverity.Warning));
        time.Advance(TimeSpan.FromSeconds(4));
        Assert.Single(center.Visible);

        time.Advance(TimeSpan.FromSeconds(1));

        Assert.Empty(center.Visible);
        Assert.Equal([NotificationChangeKind.Added, NotificationChangeKind.Removed], changes.Select(change => change.Kind));
    }

    [Fact]
    public void UnDoppioneAnnunciaLoScossone()
    {
        var center = new NotificationCenter(new ManualTime());
        var changes = new List<NotificationChangeKind>();
        center.Changed += (_, change) => changes.Add(change.Kind);

        center.Post(new Notification("x", NotificationSeverity.Error));
        center.Post(new Notification("x", NotificationSeverity.Error));

        Assert.Equal([NotificationChangeKind.Added, NotificationChangeKind.Bumped], changes);
    }

    [Fact]
    public void LaBarraDiStatoMandaAllaComparsaSoloCioCheConta()
    {
        var time = new ManualTime();
        var center = new NotificationCenter(time);
        var status = new StatusBarService(time, center, new NotificationHistory(time));

        status.Report("Pronto");
        status.Report("Salvato", NotificationSeverity.Info);
        status.Report("Esportato", NotificationSeverity.Success);
        status.Report("Il documento è in sola lettura", NotificationSeverity.Error, NotificationRemedies.EditMode);

        Assert.Equal(["Esportato", "Il documento è in sola lettura"], center.Visible.Select(entry => entry.Notification.Message));
        Assert.Equal("Il documento è in sola lettura", status.Segments.Single(segment => segment.Id == "shell.transient").Text);
        Assert.Same(NotificationRemedies.EditMode, center.Visible[1].Notification.Action);
    }
}

public sealed class NotificationToastHostTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private sealed class RecordingRunner : INotificationActionRunner
    {
        public List<NotificationAction> Ran { get; } = [];

        public void Run(NotificationAction action) => Ran.Add(action);
    }

    private static (NotificationCenter Center, NotificationToastHost Host, RecordingRunner Runner) Open()
    {
        var center = new NotificationCenter(new ManualTime());
        var runner = new RecordingRunner();
        var host = new NotificationToastHost(center, new IconCatalog(), new TextCatalog(UiLanguage.English), runner);
        var window = new Window { Width = 800, Height = 500, Content = host };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (center, host, runner);
    }

    [Fact]
    public Task LaPiuNuovaStaInBassoEIlTettoETre() => Session.Dispatch(() =>
    {
        var (center, host, _) = Open();

        foreach (var text in new[] { "uno", "due", "tre", "quattro" })
            center.Post(new Notification(text, NotificationSeverity.Warning));
        Dispatcher.UIThread.RunJobs();

        var live = host.Toasts.Where(toast => toast.IsHitTestVisible).Select(toast => toast.Entry.Notification.Message);
        Assert.Equal(["due", "tre", "quattro"], live);
    }, CancellationToken.None);

    [Fact]
    public Task IlPulsanteDAzioneEsegueEChiude() => Session.Dispatch(() =>
    {
        var (center, host, runner) = Open();
        center.Post(new Notification("Il documento è in sola lettura", NotificationSeverity.Error, NotificationRemedies.EditMode));
        Dispatcher.UIThread.RunJobs();

        var toast = host.Toasts.Single();
        Assert.Equal("Edit Mode", toast.ActionButton!.Content);
        Assert.False(toast.ActionButton.Focusable);

        toast.ActionButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal([NotificationRemedies.EditMode], runner.Ran);
        Assert.Empty(center.Visible);
    }, CancellationToken.None);

    [Fact]
    public Task LaCroceChiudeSenzaEseguire() => Session.Dispatch(() =>
    {
        var (center, host, runner) = Open();
        center.Post(new Notification("Riferimento non valido", NotificationSeverity.Error));
        Dispatcher.UIThread.RunJobs();

        host.Toasts.Single().CloseButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Empty(runner.Ran);
        Assert.Empty(center.Visible);
    }, CancellationToken.None);

    [Fact]
    public Task UnDoppioneNonAggiungeUnaSecondaPiastra() => Session.Dispatch(() =>
    {
        var (center, host, _) = Open();

        center.Post(new Notification("Il foglio è protetto", NotificationSeverity.Warning));
        center.Post(new Notification("Il foglio è protetto", NotificationSeverity.Warning));
        Dispatcher.UIThread.RunJobs();

        Assert.Single(host.Toasts);
    }, CancellationToken.None);
}
