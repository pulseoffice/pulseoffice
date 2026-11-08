using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class StatusBarViewTests
{
    [Fact]
    public Task Un_messaggio_lungo_si_accorcia_e_non_copre_gli_indicatori_a_destra() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var time = new ManualTime();
        var status = new StatusBarService(time, new NotificationCenter(time), new NotificationHistory(time));
        var bar = new StatusBarView(status);
        status.Register("format", StatusSegmentKind.Permanent).Text = "ODF";
        var window = new Window { Width = 500, Height = 120, Content = new DockPanel { LastChildFill = false, Children = { bar } } };
        DockPanel.SetDock(bar, Dock.Bottom);
        window.Show();

        status.Report(string.Join(" ", Enumerable.Repeat("funzionalità non conservate dal formato", 12)), NotificationSeverity.Warning);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var texts = bar.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).ToList();
        var message = texts.Single(block => block.Text?.StartsWith("funzionalità", StringComparison.Ordinal) == true);
        var format = texts.Single(block => block.Text == "ODF");
        var messageRight = message.TranslatePoint(new Avalonia.Point(message.Bounds.Width, 0), bar)!.Value.X;
        var formatLeft = format.TranslatePoint(new Avalonia.Point(0, 0), bar)!.Value.X;
        Assert.True(messageRight <= formatLeft, $"{messageRight} > {formatLeft}");
        window.Close();
    }, CancellationToken.None);
}
