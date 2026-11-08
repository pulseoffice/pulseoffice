using PulseOffice.Testing;
using Avalonia;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class NotificationHistoryTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    [Fact]
    public void IlPiuRecenteStaInCima()
    {
        var history = new NotificationHistory(new ManualTime());
        history.Record(new Notification("uno", NotificationSeverity.Info));
        history.Record(new Notification("due", NotificationSeverity.Error));

        Assert.Equal(["due", "uno"], history.Entries.Select(entry => entry.Notification.Message));
    }

    [Fact]
    public void UnDoppioneConsecutivoSiConta()
    {
        var history = new NotificationHistory(new ManualTime());
        history.Record(new Notification("protetta", NotificationSeverity.Error));
        history.Record(new Notification("protetta", NotificationSeverity.Error));

        var only = Assert.Single(history.Entries);
        Assert.Equal(2, only.Repeats);
    }

    [Fact]
    public void LeInformazioniNonContanoComeNonLette()
    {
        var history = new NotificationHistory(new ManualTime());
        history.Record(new Notification("pronto", NotificationSeverity.Info));
        history.Record(new Notification("errore", NotificationSeverity.Error));
        history.Record(new Notification("attenzione", NotificationSeverity.Warning));

        Assert.Equal(2, history.Unread);
        history.MarkRead();
        Assert.Equal(0, history.Unread);
    }

    [Fact]
    public void IlRegistroHaUnTetto()
    {
        var history = new NotificationHistory(new ManualTime());
        for (var index = 0; index < NotificationHistory.Capacity + 30; index++)
            history.Record(new Notification($"m{index}", NotificationSeverity.Info));

        Assert.Equal(NotificationHistory.Capacity, history.Entries.Count);
        Assert.Equal($"m{NotificationHistory.Capacity + 29}", history.Entries[0].Notification.Message);
    }

    [Fact]
    public void LaBarraDiStatoRegistraOgniMessaggio()
    {
        var time = new ManualTime();
        var history = new NotificationHistory(time);
        var status = new StatusBarService(time, new NotificationCenter(time), history);

        status.Report("semplice");
        status.Report("rifiuto", NotificationSeverity.Error);

        Assert.Equal(["rifiuto", "semplice"], history.Entries.Select(entry => entry.Notification.Message));
        Assert.Equal(1, history.Unread);
    }

    [Fact]
    public Task IlPannelloMostraUnaRigaPerMessaggioESiSvuota() => Session.Dispatch(() =>
    {
        var history = new NotificationHistory(new ManualTime());
        history.Record(new Notification("uno", NotificationSeverity.Warning));
        history.Record(new Notification("due", NotificationSeverity.Error));

        var view = new NotificationHistoryView(history, new IconCatalog(), new TextCatalog(UiLanguage.English), new NoRunner());
        var window = new Window { Width = 600, Height = 600, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, view.RowCount);

        history.Clear();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, view.RowCount);
        window.Close();
    }, CancellationToken.None);

    private sealed class NoRunner : INotificationActionRunner
    {
        public void Run(NotificationAction action)
        {
        }
    }
}

public sealed class NotificationHistoryButtonTests
{
    [Fact]
    public Task IlClicSulCampanelloSegnaLettiIMessaggi() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var time = new ManualTime();
        var history = new NotificationHistory(time);
        history.Record(new Notification("errore", NotificationSeverity.Error));

        var bar = new StatusBarView(new StatusBarService(time, new NotificationCenter(time), history));
        var button = new NotificationHistoryButton(history, new IconCatalog(), new TextCatalog(UiLanguage.English), new Silent());
        bar.UseHistory(button);
        var window = new Window { Width = 800, Height = 200, Content = new DockPanel { LastChildFill = false, Children = { bar } } };
        DockPanel.SetDock(bar, Dock.Bottom);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var centre = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(centre, Avalonia.Input.MouseButton.Left);
        window.MouseUp(centre, Avalonia.Input.MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, history.Unread);
        window.Close();
    }, CancellationToken.None);

    private sealed class Silent : INotificationActionRunner
    {
        public void Run(NotificationAction action)
        {
        }
    }
}
