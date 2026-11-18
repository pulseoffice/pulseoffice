using PulseOffice.Documents.Blocks;

namespace PulseOffice.Documents.Annotations;

public interface ISectionNoteCollection
{
    bool CollectsEndnotes(TextDocument document, SectionBlock section);

    void Settle(TextDocument document);
}

public sealed class SectionNoteCollection : ISectionNoteCollection
{
    public bool CollectsEndnotes(TextDocument document, SectionBlock section)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(section);
        return section.CollectsEndnotes ?? document.Notes.EndnoteSettings.Placement == NotePlacement.SectionEnd;
    }

    public void Settle(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var sections = document.Body.Blocks.OfType<SectionBlock>().Where(section => section.Index is null).ToList();
        if (sections.Count > 0 && sections.All(section => section.CollectsEndnotes == true))
            document.Notes.EndnoteSettings = document.Notes.EndnoteSettings with { Placement = NotePlacement.SectionEnd };
        var collects = document.Notes.EndnoteSettings.Placement == NotePlacement.SectionEnd;
        foreach (var section in sections)
            if (section.CollectsEndnotes == collects) section.CollectsEndnotes = null;
    }
}
