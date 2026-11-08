using PulseOffice.Foundation.Modules;
using PulseOffice.Presentations;
using PulseOffice.Shell;

namespace PulseOffice.Presentations.UI;

public sealed class DeckStateWitness(IActiveDeckContext context) : IDocumentStateWitness
{
    public IDocumentStateMark? Mark(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Module != ModuleKind.Presentations || context.Target is not { } target) return null;
        return new SlideMark(target.Deck, target.CurrentSlide(), target.ViewMode() == DeckViewMode.Normal ? [.. target.SelectedMany().Select(element => element.Id)] : []);
    }

    private sealed class SlideMark(Presentation deck, Slide? slide, IReadOnlyList<string> selected) : IDocumentStateMark
    {
        public string? Broken()
        {
            if (slide is null) return null;
            if (!deck.Slides.Contains(slide)) return "status.dialog.stale.target";
            var present = slide.Elements.Select(element => element.Id).ToHashSet(StringComparer.Ordinal);
            return selected.All(present.Contains) ? null : "status.dialog.stale.target";
        }
    }
}
