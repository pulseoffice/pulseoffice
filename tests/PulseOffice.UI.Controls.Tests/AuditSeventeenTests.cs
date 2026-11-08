using PulseOffice.Testing;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Spectrum;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class AuditSeventeenTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private sealed class NoRunner : INotificationActionRunner
    {
        public void Run(NotificationAction action)
        {
        }
    }

    [Fact]
    public void UnMessaggioVuotoNonCompareENonFiniscePerStoria()
    {
        var time = new ManualTime();
        var center = new NotificationCenter(time);
        var history = new NotificationHistory(time);
        var status = new StatusBarService(time, center, history);

        status.Report("   ", NotificationSeverity.Error);
        status.Report(string.Empty);

        Assert.Empty(center.Visible);
        Assert.Empty(history.Entries);
        Assert.DoesNotContain(status.Segments, segment => segment.Id == "shell.transient");
    }

    [Fact]
    public void UnaDurataNegativaUsaQuellaPredefinita()
    {
        var time = new ManualTime();
        var status = new StatusBarService(time, new NotificationCenter(time), new NotificationHistory(time));

        status.Report("x", TimeSpan.FromSeconds(-3));
        var segment = status.Segments.Single(item => item.Id == "shell.transient");
        Assert.True(segment.IsVisible);

        time.Advance(TimeSpan.FromSeconds(6));
        Assert.False(segment.IsVisible);
    }

    [Fact]
    public void IlTimerVecchioNonNascondeIlMessaggioNuovo()
    {
        var time = new ManualTime();
        var status = new StatusBarService(time, new NotificationCenter(time), new NotificationHistory(time));

        status.Report("primo", TimeSpan.FromSeconds(2));
        time.Advance(TimeSpan.FromSeconds(1));
        status.Report("secondo", TimeSpan.FromSeconds(5));
        time.Advance(TimeSpan.FromSeconds(2));

        var segment = status.Segments.Single(item => item.Id == "shell.transient");
        Assert.True(segment.IsVisible);
        Assert.Equal("secondo", segment.Text);
    }

    [Fact]
    public void ISegmentiSonoUnaFotografia()
    {
        var time = new ManualTime();
        var status = new StatusBarService(time, new NotificationCenter(time), new NotificationHistory(time));

        status.Register("a", StatusSegmentKind.Contextual);
        var snapshot = status.Segments;
        status.Register("b", StatusSegmentKind.Contextual);

        Assert.Single(snapshot);
        Assert.Equal(2, status.Segments.Count);
    }

    [Fact]
    public Task LeNotificheTolteMentreLaComparsaEStaccataNonRestano() => Session.Dispatch(() =>
    {
        var center = new NotificationCenter(new ManualTime());
        var host = new NotificationToastHost(center, new IconCatalog(), new TextCatalog(UiLanguage.English), new NoRunner());
        var window = new Window { Width = 800, Height = 500, Content = host };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        center.Post(new Notification("uno", NotificationSeverity.Warning));
        Dispatcher.UIThread.RunJobs();
        var first = center.Visible[0].Id;

        window.Content = null;
        Dispatcher.UIThread.RunJobs();
        center.Dismiss(first);
        center.Post(new Notification("due", NotificationSeverity.Warning));
        window.Content = host;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["due"], host.Toasts.Select(toast => toast.Entry.Notification.Message));
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task CiòCheArrivaConLaStoriaApertaNonResta() => Session.Dispatch(() =>
    {
        var time = new ManualTime();
        var history = new NotificationHistory(time);
        var texts = new TextCatalog(UiLanguage.English);
        var button = new NotificationHistoryButton(history, new IconCatalog(), texts, new NoRunner());
        var window = new Window { Width = 400, Height = 300, Content = button };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        history.Record(new Notification("prima", NotificationSeverity.Error));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, history.Unread);

        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.True(button.IsHistoryOpen);
        Assert.Equal(0, history.Unread);

        history.Record(new Notification("dopo", NotificationSeverity.Warning));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, history.Unread);
        window.Close();
    }, CancellationToken.None);

    [Theory]
    [InlineData(double.NaN, double.NaN, double.NaN)]
    [InlineData(double.PositiveInfinity, 0.5, 0.5)]
    [InlineData(120, double.NegativeInfinity, double.PositiveInfinity)]
    public void ValoriNonFinitiNonRomponoLoSpettro(double hue, double saturation, double value)
    {
        var colour = new SpectrumColor(hue, saturation, value);

        var rgb = colour.ToRgb();
        var shaded = colour.WithHue(hue).WithShade(saturation, value);

        Assert.Matches("^#[0-9A-F]{6}$", rgb.Hex);
        Assert.True(double.IsFinite(shaded.Hue));
        Assert.True(double.IsFinite(shaded.Saturation));
        Assert.True(double.IsFinite(shaded.Value));
    }

    [Fact]
    public Task IlCodiceScrittoMaleTornaAlColoreAllUscita() => Session.Dispatch(() =>
    {
        var picker = new ColorPickerView("#112233", 1, new TextCatalog(UiLanguage.English));
        var window = new Window { Width = 400, Height = 500, Content = picker };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var hex = picker.GetVisualDescendantsOfType<TextBox>().First();
        hex.Text = "#zz";
        hex.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Escape });
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("#112233", hex.Text);
        Assert.Equal("#112233", picker.Value);
        Assert.False(double.IsNaN(new ColorPickerView(null, double.NaN, new TextCatalog(UiLanguage.English)).Strength));
        window.Close();
    }, CancellationToken.None);
}

internal static class VisualSearch
{
    public static IEnumerable<T> GetVisualDescendantsOfType<T>(this Avalonia.Visual root) =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root).OfType<T>();
}
