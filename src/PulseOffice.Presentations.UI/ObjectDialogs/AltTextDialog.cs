using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Presentations.UI.ObjectDialogs;

public sealed class AltTextDialog(IAltTextPrompt prompt, IActiveDeckContext context, IDialogService dialogs, ITextCatalog texts)
    : DeckObjectDialog(context, dialogs, texts)
{
    public override string Id => "deck.object.altText";

    protected override string HistoryKey => "deck.action.altText";

    protected override bool Accepts(SlideElement element) => element is ObjectElement;

    protected override async Task<SlideElement?> EditAsync(SlideElement element, Presentation deck, CancellationToken ct)
    {
        var target = (ObjectElement)element;
        var answer = await prompt.AskAsync(
            new AltText(target.Content.Title, target.Content.Description), "command.deck.object.altText.title", ct).ConfigureAwait(true);

        return answer is null
            ? null
            : target with { Content = target.Content with { Title = answer.Title, Description = answer.Description } };
    }
}
