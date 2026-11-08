using PulseOffice.Exchange;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls.Exchange;

public interface IExchangePasteChooser
{
    Task<ExchangePasteMode?> ChooseAsync(IReadOnlyList<ExchangePasteMode> modes, string titleKey, CancellationToken ct = default);
}

public sealed class ExchangePasteChooser(IDialogService dialogs, ITextCatalog texts) : IExchangePasteChooser
{
    public async Task<ExchangePasteMode?> ChooseAsync(IReadOnlyList<ExchangePasteMode> modes, string titleKey, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(modes);
        if (modes.Count == 0)
        {
            await dialogs.ShowAsync<object>(new FormPromptView(texts["exchange.paste.empty"], [], texts),
                new DialogOptions { TitleKey = titleKey }, ct).ConfigureAwait(true);
            return null;
        }

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(texts["exchange.paste.explanation"],
                [new ChoiceField("format", texts["exchange.paste.format"], [.. modes.Select(Label)])], texts),
            new DialogOptions { TitleKey = titleKey, PreferredWidth = 420 }, ct).ConfigureAwait(true);
        if (values is null) return null;
        var index = values["format"] is int chosen ? chosen : 0;
        return modes[Math.Clamp(index, 0, modes.Count - 1)];
    }

    public string Label(ExchangePasteMode mode) => texts["exchange.paste.mode." + char.ToLowerInvariant(mode.ToString()[0]) + mode.ToString()[1..]];
}
