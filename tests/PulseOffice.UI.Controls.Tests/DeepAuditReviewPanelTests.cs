using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Review;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class DeepAuditReviewPanelTests
{
    [Fact]
    public Task Senza_permesso_di_commentare_Canc_e_Ctrl_Invio_non_toccano_i_commenti() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var source = new Source(ReviewAbilities.None);
        var panel = new ReviewPanel(source, new IconCatalog(), new TextCatalog(UiLanguage.Italian));
        panel.Show(true);
        panel.Select("c1");
        var list = panel.GetLogicalDescendants().OfType<ListBox>().First();

        list.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Delete, Source = list });
        panel.Reply("risposta");

        Assert.Empty(source.Deleted);
        Assert.Empty(source.Replied);
    }, CancellationToken.None);

    [Fact]
    public Task Il_pannello_rimesso_in_vista_torna_ad_ascoltare_il_documento() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var source = new Source(new ReviewAbilities(true, true, true, true));
        var panel = new ReviewPanel(source, new IconCatalog(), new TextCatalog(UiLanguage.Italian));
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

    private sealed class Source(ReviewAbilities abilities) : IReviewSource
    {
        public List<string> Deleted { get; } = [];

        public List<string> Replied { get; } = [];

        public int Listeners { get; private set; }

        public ReviewAbilities Abilities => abilities;

        public IReadOnlyList<ReviewItem> Changes => [];

        public IReadOnlyList<ReviewItem> Comments { get; } = [new("c1", ReviewItemKind.Comment, "Anna", DateTimeOffset.Now, "Da rivedere", 0xFF2563EB)];

        public string? Current => null;

        public event EventHandler? Changed
        {
            add => Listeners++;
            remove => Listeners--;
        }

        public void Go(string id)
        {
        }

        public void Accept(IReadOnlyList<string> ids)
        {
        }

        public void Reject(IReadOnlyList<string> ids)
        {
        }

        public void Reply(string id, string text) => Replied.Add(id);

        public void Edit(string id, string text)
        {
        }

        public void ToggleResolved(string id)
        {
        }

        public void Delete(IReadOnlyList<string> ids) => Deleted.AddRange(ids);
    }
}
