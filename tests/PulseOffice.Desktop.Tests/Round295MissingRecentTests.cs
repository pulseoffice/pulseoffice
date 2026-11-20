using Avalonia.Controls;
using Avalonia.LogicalTree;
using PulseOffice.Localization;
using PulseOffice.Shell;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class Round295MissingRecentTests(HeadlessDesktopSession headless)
{
    private sealed class Recent : IRecentDocuments
    {
        public IReadOnlyList<RecentDocument> Items { get; } =
        [
            new RecentDocument("/d/Here.ods", "Here", DateTimeOffset.Now),
            new RecentDocument("/d/Gone.ods", "Gone", DateTimeOffset.Now),
        ];

        public void Remember(string path, string name)
        {
        }

        public void Forget(string path)
        {
        }

        public void Clear()
        {
        }

        public event EventHandler? Changed { add { } remove { } }
    }

    private sealed class Availability : IRecentAvailability
    {
        public HashSet<string> Missing { get; } = ["/d/Gone.ods"];

        public Task<IReadOnlySet<string>> MissingAsync(IReadOnlyList<string> paths, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(Missing));
    }

    private static List<string?> Marked(StartView view) =>
        [.. view.GetLogicalDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains("startRecentMissing"))
            .Select(button => Avalonia.Automation.AutomationProperties.GetName(button))];

    private sealed class NoIcons : IIconCatalog
    {
        public IconGeometry? Find(string key) => null;

        public bool Contains(string key) => false;

        public IReadOnlyCollection<string> Keys => [];
    }

    [Fact]
    public Task A_missing_recent_entry_is_marked_before_it_is_clicked() => headless.Session.Dispatch(() =>
    {
        var view = new StartView(new Recent(), new TextCatalog(UiLanguage.English), new NoIcons(), new Availability());

        var missing = view.GetLogicalDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains("startRecentMissing"))
            .Select(button => Avalonia.Automation.AutomationProperties.GetName(button))
            .ToList();

        Assert.Equal(["Gone"], missing);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task A_recent_entry_that_comes_back_is_unmarked_and_a_newly_missing_one_marked_on_recheck() => headless.Session.Dispatch(async () =>
    {
        var availability = new Availability();
        var view = new StartView(new Recent(), new TextCatalog(UiLanguage.English), new NoIcons(), availability);
        Assert.Equal(["Gone"], Marked(view));

        availability.Missing.Clear();
        availability.Missing.Add("/d/Here.ods");
        await view.RecheckAvailabilityAsync();
        await view.RecheckAvailabilityAsync();

        Assert.Equal(["Here"], Marked(view));
    }, TestContext.Current.CancellationToken);
}
