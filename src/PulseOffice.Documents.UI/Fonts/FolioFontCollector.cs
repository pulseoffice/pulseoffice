using PulseOffice.Documents.Editing;
using PulseOffice.Typography.Embedding;

namespace PulseOffice.Documents.UI.Fonts;

public sealed class FolioFontCollector(IStoryCatalog stories) : IUsedFontCollector
{
    public bool Handles(object model) => model is TextDocument;

    public IReadOnlyCollection<string> Families(object model)
    {
        if (model is not TextDocument document) return [];
        var families = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? family)
        {
            if (!string.IsNullOrWhiteSpace(family)) families.Add(family);
        }

        Add(document.Styles.DefaultCharacter.Font);
        foreach (var style in document.Styles.Paragraph.All) Add(style.Character.Font);
        foreach (var style in document.Styles.Character.All) Add(style.Character.Font);
        foreach (var paragraph in stories.Paragraphs(document))
            foreach (var run in paragraph.Content.Runs) Add(run.Font);
        return families;
    }
}
