using PulseOffice.Document.Forms;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Forms;

namespace PulseOffice.Presentations.UI;

public sealed class DeckFormCommands(IActiveDeckContext context, IFormControlDialogs dialogs, IControlTabOrder tabOrder, DeckMacroEventsSource macroEvents) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Form("deck.form.properties", () => Selected() is not null, PropertiesAsync, edits: true),
        Form("deck.form.tabOrder", () => Controls().Count > 1, TabOrderAsync, edits: true),
    ];

    private CommandDefinition Form(string id, Func<bool> enabled, Func<CancellationToken, Task> run, bool edits) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = edits ? DocumentState.ReadOnly : DocumentState.None,
            EnabledWhen = _ => Context.Target is not null && enabled(),
            Execute = (_, ct) => run(ct),
        };

    private ObjectElement? Selected() =>
        Context.Target?.Selected() is ObjectElement { Content: FormControlObject } element ? element : null;

    private List<ObjectElement> Controls() =>
        Context.Target?.CurrentSlide() is { } slide
            ? [.. slide.Elements.OfType<ObjectElement>().Where(element => element.Content is FormControlObject)]
            : [];

    private async Task PropertiesAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return;
        if (Selected() is not { Content: FormControlObject control } element) return;
        if (macroEvents.Create(readOnly: false) is not { } events) return;
        if (await dialogs.EditAsync(control, events, "command.deck.form.properties.title", ct).ConfigureAwait(true) is not { } properties) return;
        if (!Holds(t, slide, element)) return;

        t.Actions.ReplaceElement(slide, element with { Content = control with { Properties = properties } }, "history.form.control");
    }

    private async Task TabOrderAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return;
        var controls = Controls();
        var names = await dialogs.TabOrderAsync(
            [.. controls.Select(element => ((FormControlObject)element.Content).Properties)],
            "command.deck.form.tabOrder.title", ct).ConfigureAwait(true);
        if (names is null || controls.Any(control => !Holds(t, slide, control))) return;

        var updated = tabOrder.Apply(controls, names);
        t.Actions.ReplaceElements(slide, updated,
            "history.form.control");
    }

}
