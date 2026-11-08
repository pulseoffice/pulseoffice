using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Navigation;
using PulseOffice.UI.Controls.Review;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ChromePerformanceCollection
{
    public const string Name = "Chrome performance";
}

[Collection(ChromePerformanceCollection.Name)]
public sealed class ReviewPanelCostTests
{
    private const int Count = 5000;

    private static void Record(string line) =>
        File.AppendAllText(Path.Combine(Path.GetTempPath(), "pulseoffice-chrome-cost.txt"), $"{DateTimeOffset.Now:O} {line}\n");

    [Fact]
    public Task Five_thousand_comments_realize_only_the_visible_cards_and_a_burst_of_changes_refills_once() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var source = new ManySource(Count);
        var panel = new ReviewPanel(source, new IconCatalog(), new TextCatalog(UiLanguage.Italian));
        var window = new Window { Content = panel, Width = 320, Height = 800 };
        var clock = Stopwatch.StartNew();
        window.Show();
        panel.Show(true);
        Dispatcher.UIThread.RunJobs();
        var opened = clock.Elapsed;

        clock.Restart();
        const int edits = 20;
        for (var i = 0; i < edits; i++) source.Raise();
        Dispatcher.UIThread.RunJobs();
        var burst = clock.Elapsed;
        var realized = window.GetVisualDescendants().OfType<ListBoxItem>().Count();
        Record($"review panel comments={Count} open={opened.TotalMilliseconds:0} burst of {edits} changes={burst.TotalMilliseconds:0} realized={realized} reads={source.Reads}");

        Assert.Equal(Count, panel.Items.Count);
        Assert.True(realized < 200, $"{realized} cards realized");
        Assert.True(source.Reads <= 3, $"{source.Reads} refills");
        Assert.True(opened < TimeSpan.FromSeconds(1), $"open {opened.TotalMilliseconds:0} ms");
        Assert.True(burst < TimeSpan.FromMilliseconds(300), $"burst {burst.TotalMilliseconds:0} ms");
        window.Close();
    }, CancellationToken.None);

    private sealed class ManySource(int count) : IReviewSource
    {
        private readonly IReadOnlyList<ReviewItem> _comments =
            [.. Enumerable.Range(0, count).Select(index => new ReviewItem($"c{index}", ReviewItemKind.Comment, $"Autore {index % 7}", DateTimeOffset.UnixEpoch, $"Commento numero {index}", 0xFF2563EB))];

        public int Reads { get; private set; }

        public ReviewAbilities Abilities => new(true, true, true, true);

        public IReadOnlyList<ReviewItem> Changes => [];

        public IReadOnlyList<ReviewItem> Comments
        {
            get
            {
                Reads++;
                return _comments;
            }
        }

        public string? Current => null;

        public event EventHandler? Changed;

        public void Raise() => Changed?.Invoke(this, EventArgs.Empty);

        public void Go(string id)
        {
        }

        public void Accept(IReadOnlyList<string> ids)
        {
        }

        public void Reject(IReadOnlyList<string> ids)
        {
        }

        public void Reply(string id, string text)
        {
        }

        public void Edit(string id, string text)
        {
        }

        public void ToggleResolved(string id)
        {
        }

        public void Delete(IReadOnlyList<string> ids)
        {
        }
    }
}

[Collection(ChromePerformanceCollection.Name)]
public sealed class NavigatorPanelCostTests
{
    private const int Count = 5000;

    [Fact]
    public Task Five_thousand_headings_realize_only_the_visible_rows_and_a_burst_of_changes_refills_once() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var source = new ManyHeadings(Count);
        var panel = new NavigatorPanel(source, new PanelIcons(), new TextCatalog(UiLanguage.Italian));
        var window = new Window { Content = panel, Width = 320, Height = 800 };
        var clock = Stopwatch.StartNew();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var opened = clock.Elapsed;
        var readsAfterOpen = source.Reads;

        clock.Restart();
        const int edits = 20;
        for (var i = 0; i < edits; i++) source.Raise();
        Dispatcher.UIThread.RunJobs();
        var burst = clock.Elapsed;
        var realized = window.GetVisualDescendants().OfType<ListBoxItem>().Count();
        File.AppendAllText(Path.Combine(Path.GetTempPath(), "pulseoffice-chrome-cost.txt"),
            $"{DateTimeOffset.Now:O} navigator headings={Count} open={opened.TotalMilliseconds:0} burst of {edits} changes={burst.TotalMilliseconds:0} realized={realized} reads per burst={source.Reads - readsAfterOpen}\n");

        Assert.Equal(Count, panel.Visible.Count);
        Assert.True(realized < 200, $"{realized} rows realized");
        Assert.True(source.Reads - readsAfterOpen <= 2, $"{source.Reads - readsAfterOpen} reads for one burst");
        Assert.True(burst < TimeSpan.FromMilliseconds(300), $"burst {burst.TotalMilliseconds:0} ms");
        window.Close();
    }, CancellationToken.None);

    private sealed class ManyHeadings(int count) : INavigatorSource
    {
        private readonly IReadOnlyList<NavigatorItem> _items =
            [.. Enumerable.Range(0, count).Select(index => new NavigatorItem($"h{index}", $"Titolo {index}", index % 3) { Movable = true })];

        public int Reads { get; private set; }

        public IReadOnlyList<NavigatorCategory> Categories => [new("headings", "Titoli")];

        public IReadOnlyList<NavigatorItem> Items(string category)
        {
            Reads++;
            return _items;
        }

        public string? Current(string category) => null;

        public event EventHandler? Changed;

        public void Raise() => Changed?.Invoke(this, EventArgs.Empty);

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
    }
}
