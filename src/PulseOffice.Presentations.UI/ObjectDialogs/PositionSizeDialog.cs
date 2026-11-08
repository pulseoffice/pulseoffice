using System.Globalization;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Presentations.UI.ObjectDialogs;

public sealed class PositionSizeDialog(IActiveDeckContext context, IDialogService dialogs, ITextCatalog texts, PulseOffice.UI.Icons.IIconCatalog? icons = null)
    : DeckObjectDialog(context, dialogs, texts)
{
    public override string Id => "deck.object.positionSize";

    protected override string HistoryKey => "deck.action.transform";

    protected override bool Accepts(SlideElement element) => true;

    protected override async Task<SlideElement?> EditAsync(SlideElement element, Presentation deck, CancellationToken ct)
    {
        var bounds = element.Bounds;
        var limit = Math.Max(deck.Size.Width.ToCentimeters(), deck.Size.Height.ToCentimeters()) * 3;

        var values = await Dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(
                Texts["deck.position.explanation"],
                [
                    new NumberField("x", Texts["deck.position.x"], Round(bounds.Left), -limit, limit, 0.1),
                    new NumberField("y", Texts["deck.position.y"], Round(bounds.Top), -limit, limit, 0.1),
                    new SizeLockField("size", Texts["size.title"], new LockedSize(Round(bounds.Width), Round(bounds.Height), element is ObjectElement { Content: PulseOffice.Drawing.Assets.RasterImageObject } or MediaElement), "cm", 0.1, limit, 0.1, icons),
                    new NumberField("rotation", Texts["deck.position.rotation"], Math.Round(element.RotationDegrees, 1), -360, 360, 1),
                ],
                Texts),
            new DialogOptions { TitleKey = "command.deck.object.positionSize.title", PreferredWidth = 420 },
            ct).ConfigureAwait(true);

        if (values is null) return null;

        return element with
        {
            Bounds = values["size"] is LockedSize size
                ? new DocumentRect(Cm(values["x"]), Cm(values["y"]), Twip.FromCentimeters(size.Width), Twip.FromCentimeters(size.Height))
                : bounds,
            RotationDegrees = Convert.ToDouble(values["rotation"], CultureInfo.InvariantCulture),
        };
    }

    private static double Round(Twip value) => Math.Round(value.ToCentimeters(), 2);

    private static Twip Cm(object value) => Twip.FromCentimeters(Convert.ToDouble(value, CultureInfo.InvariantCulture));
}
