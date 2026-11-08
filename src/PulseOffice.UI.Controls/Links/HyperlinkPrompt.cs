using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Icons;

namespace PulseOffice.UI.Controls.Links;

public interface IHyperlinkPrompt
{
    Task<LinkChoice?> AskAsync(LinkRequest request, CancellationToken ct = default);
}

public sealed class HyperlinkPrompt(
    IDialogService dialogs,
    ILinkAddresses addresses,
    ITextCatalog texts,
    IIconCatalog icons,
    IFileDialogService files) : IHyperlinkPrompt
{
    public async Task<LinkChoice?> AskAsync(LinkRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var view = new HyperlinkView(request, new HyperlinkServices(addresses, texts, icons, BrowseAsync));
        return await dialogs.ShowAsync<LinkChoice>(view,
            new DialogOptions { TitleKey = request.TitleKey, PreferredWidth = 640, AccessibleDescription = texts["link.explanation"] }, ct).ConfigureAwait(true);
    }

    private async Task<string?> BrowseAsync(CancellationToken ct)
    {
        var chosen = await files.OpenFileAsync(texts["link.file.browse"], [], ct).ConfigureAwait(true);
        if (chosen is null) return null;
        await using (chosen.ConfigureAwait(true)) return chosen.Path;
    }
}
