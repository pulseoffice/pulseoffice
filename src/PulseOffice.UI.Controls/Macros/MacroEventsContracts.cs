using PulseOffice.Document.Forms;

namespace PulseOffice.UI.Controls.Macros;

public interface IMacroEditorGateway
{
    Task OpenAsync(string? macro, CancellationToken ct);
}

public sealed record MacroEventsContext(IMacroLibrary Library, IMacroEditorGateway Editor, bool ReadOnly = false);

public sealed record MacroEventsRequest(
    IReadOnlyList<MacroEventSlot> Slots,
    IReadOnlyDictionary<string, string?> Bound,
    MacroEventsContext Context,
    string TitleKey);

public interface IMacroEventsDialog
{
    Task<IReadOnlyDictionary<string, string?>?> ShowAsync(MacroEventsRequest request, CancellationToken ct);

    MacroEventsPanel CreatePanel(
        IReadOnlyList<MacroEventSlot> slots, IReadOnlyDictionary<string, string?> bound, MacroEventsContext context);
}
