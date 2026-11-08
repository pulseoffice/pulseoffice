using PulseOffice.Presentations;

namespace PulseOffice.Formats;

public sealed class SlideBackgroundNormalizer
{
    public void Normalize(Presentation deck)
    {
        ArgumentNullException.ThrowIfNull(deck);

        foreach (var slide in deck.Slides)
        {
            if (slide.Background is not { } background || !slide.ShowMasterBackground) continue;

            var master = deck.MasterOf(slide);
            var inherited = master.BackgroundOf(deck.ThemeOf(slide), slide.Layout);
            if ((background | 0xFF000000u) == (inherited | 0xFF000000u)) slide.Background = null;
        }
    }
}
