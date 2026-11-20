using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round314FrozenStatusBarTests
{
    [Fact]
    public Task An_unfocused_window_keeps_its_own_status_until_it_is_focused_again() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var time = new ManualTime();
        var status = new StatusBarService(time, new NotificationCenter(time), new NotificationHistory(time));
        var bar = new StatusBarView(status);
        var window = new Window { Width = 400, Height = 100, Content = bar };
        window.Show();
        status.Register("format", StatusSegmentKind.Permanent).Text = "ODF";
        Dispatcher.UIThread.RunJobs();

        bar.IsFrozen = true;
        status.Register("format", StatusSegmentKind.Permanent).Text = "OOXML";
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(Texts(bar), text => text == "ODF");
        Assert.DoesNotContain(Texts(bar), text => text == "OOXML");

        bar.IsFrozen = false;
        Assert.Contains(Texts(bar), text => text == "OOXML");
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task A_frozen_status_bar_keeps_background_activity_progress_live() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var time = new ManualTime();
        var status = new StatusBarService(time, new NotificationCenter(time), new NotificationHistory(time));
        var bar = new StatusBarView(status);
        var window = new Window { Width = 400, Height = 100, Content = bar };
        window.Show();
        bar.IsFrozen = true;

        var activity = status.BeginActivity("Saving");
        activity.Progress = 0.5;
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(Texts(bar), text => text == "Saving");
        Assert.Contains(bar.GetVisualDescendants().OfType<ProgressBar>(), progress => progress.Value == 0.5);

        status.EndActivity(activity);
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(Texts(bar), text => text == "Saving");
        window.Close();
    }, CancellationToken.None);

    private static List<string?> Texts(StatusBarView bar) =>
        [.. bar.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text)];
}
