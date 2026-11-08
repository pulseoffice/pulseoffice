using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using PulseOffice.Localization;
using PulseOffice.Shell;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class StartRecentClickTests(HeadlessDesktopSession headless)
{
    private sealed class Recent : IRecentDocuments
    {
        private readonly List<RecentDocument> _items = [];

        public IReadOnlyList<RecentDocument> Items => _items;

        public void Remember(string path, string name)
        {
            _items.Insert(0, new RecentDocument(path, name, DateTimeOffset.Now));
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Forget(string path) => _items.RemoveAll(item => item.Path == path);

        public void Clear() => _items.Clear();

        public event EventHandler? Changed;
    }

    private sealed class NoIcons : IIconCatalog
    {
        public IconGeometry? Find(string key) => null;

        public bool Contains(string key) => false;

        public IReadOnlyCollection<string> Keys => [];
    }

    [Fact]
    public Task A_click_on_a_recent_entry_opens_that_document_even_if_the_list_moved() => headless.Session.Dispatch(() =>
    {
        var recent = new Recent();
        recent.Remember("/d/B.ods", "B");
        recent.Remember("/d/A.ods", "A");
        var view = new StartView(recent, new TextCatalog(UiLanguage.English), new NoIcons());
        var requested = new List<string>();
        view.CommandRequested += (_, id) => requested.Add(id);
        var entryA = view.GetLogicalDescendants().OfType<Button>()
            .First(button => button.Classes.Contains("startRecent") && AutomationProperties.GetName(button) == "A");

        recent.Remember("/d/C.ods", "C");
        entryA.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(["shell.file.recent.1"], requested);
    }, TestContext.Current.CancellationToken);
}
