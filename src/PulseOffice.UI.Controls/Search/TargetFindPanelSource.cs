namespace PulseOffice.UI.Controls.Search;

public sealed class TargetFindPanelSource(Func<IFindReplaceTarget?> target, Func<string, Task> openReplace) : IFindPanelSource
{
    public IFindReplaceTarget? Target() => target();

    public void Highlight(FindRequest request)
    {
    }

    public void ClearHighlights()
    {
    }

    public Task OpenReplaceAsync(string query) => openReplace(query);
}

public interface IFindPanelPages
{
    void Show(ISidePanelHost panel, Func<IFindPanelSource> source);
}

public sealed class FindPanelPages(PulseOffice.Localization.ITextCatalog texts) : IFindPanelPages
{
    public const string Key = "find";

    public void Show(ISidePanelHost panel, Func<IFindPanelSource> source)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(source);
        if (panel.Pages.Contains(Key)) panel.Unregister(Key);
        panel.Register(new SidePanelPage(Key, () => new FindPanelView(source(), texts)));
        panel.Show(Key);
    }
}
