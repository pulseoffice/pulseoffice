using DocumentFormat.OpenXml;

namespace PulseOffice.Formats.OpenXml;

internal sealed class PptxAlternateContent
{
    public void Unwrap(OpenXmlElement? tree)
    {
        if (tree is null) return;
        while (tree.Descendants<AlternateContent>().Where(Outermost).ToList() is { Count: > 0 } outermost)
            foreach (var parent in outermost.Select(alternate => alternate.Parent).OfType<OpenXmlElement>().Distinct().ToList())
                Flatten(parent);
    }

    private void Flatten(OpenXmlElement parent)
    {
        var current = parent.ChildElements.ToList();
        parent.RemoveAllChildren();
        foreach (var child in current)
            if (child is AlternateContent alternate) parent.Append(Chosen(alternate));
            else parent.Append(child);
    }

    private IEnumerable<OpenXmlElement> Chosen(AlternateContent alternate)
    {
        var chosen = (OpenXmlElement?)alternate.GetFirstChild<AlternateContentFallback>() ?? alternate.GetFirstChild<AlternateContentChoice>();
        if (chosen is null) return [];
        var children = chosen.ChildElements.ToList();
        chosen.RemoveAllChildren();
        return children;
    }

    private bool Outermost(AlternateContent alternate) => !alternate.Ancestors<AlternateContent>().Any();
}
