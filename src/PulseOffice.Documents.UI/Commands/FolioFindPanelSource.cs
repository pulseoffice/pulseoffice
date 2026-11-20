using PulseOffice.UI.Controls.Search;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioFindPanelSource(FolioFindCommands commands) : IFindPanelSource
{
    public IFindReplaceTarget? Target() => commands.Target(withFormats: false);

    public void Highlight(FindRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        commands.Highlight(commands.Target(withFormats: false)?.QueryOf(request));
    }

    public void ClearHighlights() => commands.Highlight(null);

    public Task OpenReplaceAsync(string query) => commands.FindAsync(query.Length > 0 ? query : null, CancellationToken.None);
}
