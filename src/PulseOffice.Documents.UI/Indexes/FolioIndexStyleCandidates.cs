using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.StyleCatalog;

namespace PulseOffice.Documents.UI.Indexes;

public interface IFolioIndexStyleCandidates
{
    IReadOnlyList<ParagraphStyle> Candidates(TextDocument document, IndexSourceStyles assigned);

    IReadOnlyList<ParagraphStyle> Others(TextDocument document, IndexSourceStyles assigned);

    string Title(ParagraphStyle style);
}

public sealed class FolioIndexStyleCandidates(IFolioStyleNames names, IStyleUsage usage) : IFolioIndexStyleCandidates
{
    public IReadOnlyList<ParagraphStyle> Candidates(TextDocument document, IndexSourceStyles assigned)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(assigned);
        var used = usage.Collect(document);
        return [.. document.Styles.Paragraph.All
            .Where(style => assigned.LevelOf(style.Name) is not null || !style.Hidden && used.Uses(StyleFamily.Paragraph, style.Name))
            .OrderBy(Title, StringComparer.CurrentCultureIgnoreCase)];
    }

    public IReadOnlyList<ParagraphStyle> Others(TextDocument document, IndexSourceStyles assigned)
    {
        var listed = Candidates(document, assigned).Select(style => style.Name).ToHashSet(StringComparer.Ordinal);
        return [.. document.Styles.Paragraph.All
            .Where(style => !style.Hidden && !listed.Contains(style.Name))
            .OrderBy(Title, StringComparer.CurrentCultureIgnoreCase)];
    }

    public string Title(ParagraphStyle style) => names.Title(style);
}
