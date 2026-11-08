using Microsoft.Extensions.Logging;
using PulseOffice.Document.Forms;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Icons;

namespace PulseOffice.UI.Controls.Macros;

public sealed class MacroEventsDialog(
    IDialogService dialogs,
    IMacroBindingRules rules,
    IIconCatalog icons,
    ITextCatalog texts,
    ILogger<MacroEventsDialog> logger) : IMacroEventsDialog
{
    public async Task<IReadOnlyDictionary<string, string?>?> ShowAsync(MacroEventsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var panel = CreatePanel(request.Slots, request.Bound, request.Context);
        var view = new MacroEventsView(panel, texts);

        return await dialogs.ShowAsync<IReadOnlyDictionary<string, string?>>(
            view,
            new DialogOptions { TitleKey = request.TitleKey, PreferredWidth = 980, PreferredHeight = 640 },
            ct).ConfigureAwait(true);
    }

    public MacroEventsPanel CreatePanel(
        IReadOnlyList<MacroEventSlot> slots, IReadOnlyDictionary<string, string?> bound, MacroEventsContext context)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(bound);
        ArgumentNullException.ThrowIfNull(context);

        return new MacroEventsPanel(
            new MacroAssignments(slots, bound), slots, context, rules, new MacroEventsChrome(texts, icons), logger);
    }
}
