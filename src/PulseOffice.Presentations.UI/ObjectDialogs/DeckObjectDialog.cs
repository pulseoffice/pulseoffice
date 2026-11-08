using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Presentations.UI.ObjectDialogs;

public abstract class DeckObjectDialog(IActiveDeckContext context, IDialogService dialogs, ITextCatalog texts)
{
    protected IDialogService Dialogs => dialogs;

    protected ITextCatalog Texts => texts;

    public abstract string Id { get; }

    protected virtual CommandSurface Surface => CommandSurface.Advanced;

    public virtual bool EditsOnDoubleClick => false;

    public bool CanEditSelection => Selection() is { } element && Accepts(element);

    protected virtual string HistoryKey => "deck.action.objectProperties";

    public CommandDefinition ToCommand() => new()
    {
        Id = new CommandId(Id),
        Module = ModuleKind.Presentations,
        Surface = Surface,
        RequiredState = DocumentState.DocumentOpen,
        ForbiddenState = DocumentState.ReadOnly,
        EnabledWhen = _ => Selection() is { } element && Accepts(element),
        Execute = (_, ct) => RunAsync(ct),
    };

    private (Slide Slide, SlideElement Element, DeckTarget Target)? Current() =>
        context.Target is { } target && target.CurrentSlide() is { } slide && target.Selected() is { } element
            ? (slide, element, target)
            : null;

    private SlideElement? Selection() => Current()?.Element;

    public async Task RunAsync(CancellationToken ct)
    {
        if (Current() is not { } current || !Accepts(current.Element)) return;

        var updated = await EditAsync(current.Element, current.Target.Deck, ct).ConfigureAwait(true);
        if (updated is null || updated == current.Element) return;
        if (current.Target.Deck.IndexOf(current.Slide) < 0 || !ReferenceEquals(current.Slide.Find(current.Element.Id), current.Element)) return;

        current.Target.Actions.ReplaceElement(current.Slide, updated, HistoryKey);
    }

    protected abstract bool Accepts(SlideElement element);

    protected abstract Task<SlideElement?> EditAsync(SlideElement element, Presentation deck, CancellationToken ct);
}
