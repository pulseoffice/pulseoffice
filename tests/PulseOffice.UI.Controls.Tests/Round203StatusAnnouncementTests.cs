using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round203StatusAnnouncementTests
{
    [Fact]
    public Task Transient_status_messages_are_a_polite_live_region() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var time = new ManualTime();
        var status = new StatusBarService(time, new NotificationCenter(time), new NotificationHistory(time));
        var bar = new StatusBarView(status);
        var window = new Window { Width = 400, Height = 100, Content = bar };
        window.Show();

        status.Report("Cell is locked", NotificationSeverity.Warning);
        Dispatcher.UIThread.RunJobs();

        var message = bar.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Text == "Cell is locked");
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(message));
        Assert.Equal("Cell is locked", AutomationProperties.GetName(message));
        window.Close();
    }, CancellationToken.None);
}
