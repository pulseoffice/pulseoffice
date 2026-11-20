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
        public Task<IReadOnlySet<string>> MissingAsync(IReadOnlyList<string> paths, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string> { "/d/Gone.ods" });
    }

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
}
