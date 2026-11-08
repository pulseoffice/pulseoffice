using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Presentations.UI.ObjectDialogs;

public sealed class ShapeStyleDialog(
    string id,
    int section,
    CommandSurface surface,
    IShapeStyling styling,
    PulseOffice.UI.Controls.Shapes.IShapeStyleEditors editors,
    IActiveDeckContext context,
    IDialogService dialogs,
    ITextCatalog texts) : DeckObjectDialog(context, dialogs, texts)
{
    public override string Id => id;

    protected override CommandSurface Surface => surface;

    protected override string HistoryKey => "deck.action.shapeStyle";

    protected override bool Accepts(SlideElement element) =>
        element is ObjectElement { Content: var content } && styling.Editable(content) is not null;

    protected override async Task<SlideElement?> EditAsync(SlideElement element, Presentation deck, CancellationToken ct)
    {
        var target = (ObjectElement)element;
        var editable = styling.Editable(target.Content)!;

        var updated = await Dialogs.ShowAsync<StyledOutline>(
            editors.Create(editable, section),
            new DialogOptions { TitleKey = "dialog.shapeEdit.title", PreferredWidth = 720 }, ct)
            .ConfigureAwait(true);

        return updated is not null && styling.Apply(target.Content, updated) is { } content
            ? target with { Content = content, RotationDegrees = updated.RotationDegrees }
            : null;
    }
}
