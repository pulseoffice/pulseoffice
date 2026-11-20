using PulseOffice.Documents.UI;
using PulseOffice.Foundation.Modules;
using PulseOffice.Presentations.UI;
using PulseOffice.Sheets.UI;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Desktop.Composition;

public sealed class ObjectNudgeCommandSets(
    IObjectNudgeCommands nudges,
    IActiveSheetContext sheets,
    IActiveDeckContext decks,
    IActiveFolioContext folios)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        .. nudges.For(ModuleKind.Sheets, "ledger", () => sheets.HasTarget && sheets.SelectedObjects.Count > 0, sheets.NudgeObjects),
        .. nudges.For(ModuleKind.Presentations, "deck",
            () => decks.Target is { } deck && deck.SelectedMany().Count > 0 && !deck.IsTyping(),
            nudge => decks.Target?.NudgeObjects(nudge) ?? false),
        .. nudges.For(ModuleKind.Documents, "folio",
            () => folios.Target?.Editor.SelectedObjectId is not null,
            nudge => folios.Target?.NudgeObject(nudge) ?? false),
    ];
}
