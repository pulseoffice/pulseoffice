using PulseOffice.Foundation.Modules;
using PulseOffice.Exchange;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Exchange;

namespace PulseOffice.Presentations.UI;

public sealed class DeckClipboardCommands(
    IActiveDeckContext context,
    IObjectExchange exchange,
    IExchangePasteChooser chooser) : DeckCommandSet(context)
{
    private const int PasteOffset = 200;

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Edit("deck.edit.selectAll", "Ctrl+A", _ => HasSlide(), SelectAll, edits: false),
        Edit("deck.edit.duplicate", "Ctrl+D", _ => HasSelection(), Duplicate),
        Edit("deck.clipboard.copy", "Ctrl+C", _ => HasSelection(), () => exchange.CopyAsync(ModuleKind.Presentations), edits: false),
        Edit("deck.clipboard.cut", "Ctrl+X", _ => HasSelection(), () => exchange.CutAsync(ModuleKind.Presentations)),
        Edit("deck.clipboard.paste", "Ctrl+V", _ => exchange.CanPaste(ModuleKind.Presentations), () => exchange.PasteAsync(ModuleKind.Presentations)),
        Prototype("deck.clipboard.pasteUnformatted") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            DefaultGesture = "Ctrl+Alt+Shift+V",
            EnabledWhen = _ => HasSlide(),
            Execute = (_, _) => PasteTextAsync(),
        },
        Prototype("deck.clipboard.pasteSpecial") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            DefaultGesture = "Ctrl+Shift+V",
            EnabledWhen = _ => HasSlide(),
            Execute = (_, ct) => PasteSpecialAsync(ct),
        },
    ];

    private Task PasteTextAsync() => exchange.PasteAsync(ModuleKind.Presentations, ExchangePasteMode.UnformattedText);

    private async Task PasteSpecialAsync(CancellationToken ct)
    {
        var package = await exchange.CurrentAsync(ct).ConfigureAwait(true);
        var modes = exchange.Modes(ModuleKind.Presentations, package);
        if (await chooser.ChooseAsync(modes, "command.deck.clipboard.pasteSpecial.title", ct).ConfigureAwait(true) is { } mode)
            exchange.Receive(ModuleKind.Presentations, package, mode);
    }

    private bool HasSlide() => Context.Target is { } t && t.CurrentSlide() is not null;

    private bool HasSelection() => Context.Target is { } t && t.SelectedMany().Count > 0;

    private void SelectAll()
    {
        if (Context.Target is { } t && t.CurrentSlide() is { } slide)
            t.Select([.. slide.InZOrder()]);
    }

    private void Duplicate()
    {
        if (Context.Target is { } t && t.CurrentSlide() is { } slide)
        {
            var selection = t.SelectedMany();
            var clones = t.Actions.AddClones(slide, selection, PasteOffset, PasteOffset, "deck.action.duplicateObject", slide.Groups,
                t.Actions.BehavioursOf(slide, selection));
            t.Select(clones);
        }
    }

    private CommandDefinition Edit(string id, string gesture, Func<CommandContext, bool> enabled, Func<Task> run, bool edits = true) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = edits ? DocumentState.ReadOnly : DocumentState.None,
            DefaultGesture = gesture,
            EnabledWhen = enabled,
            Execute = (_, _) => run(),
        };

    private CommandDefinition Edit(string id, string gesture, Func<CommandContext, bool> enabled, Action run, bool edits = true) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = edits ? DocumentState.ReadOnly : DocumentState.None,
            DefaultGesture = gesture,
            EnabledWhen = enabled,
            Execute = (_, _) => { run(); return Task.CompletedTask; },
        };
}
