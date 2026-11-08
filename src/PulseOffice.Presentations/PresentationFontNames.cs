using PulseOffice.Drawing.Text;

namespace PulseOffice.Presentations;

public interface IPresentationFontNames
{
    IReadOnlyCollection<string> Used(Presentation deck);
}

public sealed class PresentationFontNames(IObjectFontNames objects) : IPresentationFontNames
{
    public PresentationFontNames() : this(new ObjectFontNames())
    {
    }

    public IReadOnlyCollection<string> Used(Presentation deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var master in deck.Masters)
        {
            names.Add(master.Theme.Fonts.Major);
            names.Add(master.Theme.Fonts.Minor);
        }

        foreach (var element in deck.Slides.SelectMany(slide => slide.Elements))
        {
            switch (element)
            {
                case TextElement text:
                    foreach (var run in text.Text.Paragraphs.SelectMany(paragraph => paragraph.EffectiveRuns()))
                    {
                        if (run.Font is { Length: > 0 } font) names.Add(font);
                        if (run.AsianFont is { Length: > 0 } asian) names.Add(asian);
                    }
                    break;
                case ObjectElement placed:
                    foreach (var name in objects.Of(placed.Content)) names.Add(name);
                    break;
            }
        }

        if (deck.DefaultScriptFonts.Asian is { } defaultAsian) names.Add(defaultAsian);
        if (deck.DefaultScriptFonts.Complex is { } defaultComplex) names.Add(defaultComplex);
        names.RemoveWhere(string.IsNullOrWhiteSpace);
        return names;
    }
}
