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

    private sealed record Costs(TimeSpan Opened, TimeSpan Burst, int Realized, int Reads, int Items);

    private static Costs Measure(int count)
    {
        var source = new ManySource(count);
        var panel = new ReviewPanel(source, new IconCatalog(), new TextCatalog(UiLanguage.Italian));
        var window = new Window { Content = panel, Width = 320, Height = 800 };
        var clock = Stopwatch.StartNew();
        window.Show();
        panel.Show(true);
        Dispatcher.UIThread.RunJobs();
        var opened = clock.Elapsed;

        clock.Restart();
        for (var i = 0; i < Edits; i++) source.Raise();
        Dispatcher.UIThread.RunJobs();
        var burst = clock.Elapsed;
        var realized = window.GetVisualDescendants().OfType<ListBoxItem>().Count();
        var costs = new Costs(opened, burst, realized, source.Reads, panel.Items.Count);
        window.Close();
        return costs;
    }

    private const int Edits = 20;

    [Fact]
    public Task Five_thousand_comments_realize_only_the_visible_cards_and_a_burst_of_changes_refills_once() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        Measure(Count / 10);
        var small = Measure(Count / 10);
        var large = Measure(Count);
        Record($"review panel comments={Count} open={large.Opened.TotalMilliseconds:0} burst of {Edits} changes={large.Burst.TotalMilliseconds:0} realized={large.Realized} reads={large.Reads}");

        Assert.Equal(Count, large.Items);
        Assert.True(large.Realized < 200, $"{large.Realized} cards realized");
        Assert.True(large.Reads <= 3, $"{large.Reads} refills");
        var growth = new PulseOffice.Testing.GrowthCheck(TimeSpan.FromMilliseconds(10), 4);
        growth.Bounded(small.Opened, large.Opened, 10, $"open {small.Opened} at {Count / 10}, {large.Opened} at {Count}");
        growth.Bounded(small.Burst, large.Burst, 10, $"burst {small.Burst} at {Count / 10}, {large.Burst} at {Count}");
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

    private const int Edits = 20;

    private sealed record Costs(TimeSpan Burst, int Realized, int ReadsPerBurst, int Visible);

    private static Costs Measure(int count)
    {
        var source = new ManyHeadings(count);
        var panel = new NavigatorPanel(source, new PanelIcons(), new TextCatalog(UiLanguage.Italian));
        var window = new Window { Content = panel, Width = 320, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var readsAfterOpen = source.Reads;

        var clock = Stopwatch.StartNew();
        for (var i = 0; i < Edits; i++) source.Raise();
        Dispatcher.UIThread.RunJobs();
        var burst = clock.Elapsed;
        var realized = window.GetVisualDescendants().OfType<ListBoxItem>().Count();
        var costs = new Costs(burst, realized, source.Reads - readsAfterOpen, panel.Visible.Count);
        window.Close();
        return costs;
    }

    [Fact]
    public Task Five_thousand_headings_realize_only_the_visible_rows_and_a_burst_of_changes_refills_once() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        Measure(Count / 10);
        var small = Measure(Count / 10);
        var large = Measure(Count);
        File.AppendAllText(Path.Combine(Path.GetTempPath(), "pulseoffice-chrome-cost.txt"),
            $"{DateTimeOffset.Now:O} navigator headings={Count} burst of {Edits} changes={large.Burst.TotalMilliseconds:0} realized={large.Realized} reads per burst={large.ReadsPerBurst}\n");

        Assert.Equal(Count, large.Visible);
        Assert.True(large.Realized < 200, $"{large.Realized} rows realized");
        Assert.True(large.ReadsPerBurst <= 2, $"{large.ReadsPerBurst} reads for one burst");
        new PulseOffice.Testing.GrowthCheck(TimeSpan.FromMilliseconds(10), 4)
            .Bounded(small.Burst, large.Burst, 10, $"burst {small.Burst} at {Count / 10}, {large.Burst} at {Count}");
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
