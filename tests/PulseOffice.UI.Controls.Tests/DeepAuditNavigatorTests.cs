using Avalonia.Controls;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Navigation;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class DeepAuditNavigatorTests
{
    [Fact]
    public Task Il_navigatore_rimesso_in_vista_torna_ad_ascoltare_il_documento() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var source = new CountingSource();
        var panel = new NavigatorPanel(source, new PulseOffice.UI.Icons.PanelIcons(), new TextCatalog(UiLanguage.Italian));
        var window = new Window { Content = panel };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.Content = null;
        Dispatcher.UIThread.RunJobs();
        window.Content = panel;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, source.Listeners);
        window.Close();
    }, CancellationToken.None);

    private sealed class CountingSource : INavigatorSource
    {
        public int Listeners { get; private set; }

        public IReadOnlyList<NavigatorCategory> Categories => [new("headings", "Titoli")];

        public IReadOnlyList<NavigatorItem> Items(string category) => [new("h1", "Uno", 0)];

        public string? Current(string category) => null;

        public void Go(string category, string id)
        {
        }

        public bool CanMove(string category, string id, NavigatorMove move) => false;

        public void Move(string category, string id, NavigatorMove move)
        {
        }

        public bool CanDrop(string category, string id, string? beforeId) => false;

        public void Drop(string category, string id, string? beforeId)
        {
        }

        public event EventHandler? Changed
        {
            add => Listeners++;
            remove => Listeners--;
        }
    }
}
