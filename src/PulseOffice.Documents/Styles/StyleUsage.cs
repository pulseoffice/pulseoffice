using PulseOffice.Documents.Blocks;

namespace PulseOffice.Documents.Styles;

public sealed class StyleUsageReport
{
    private readonly Dictionary<StyleFamily, HashSet<string>> _used = [];

    public bool Uses(StyleFamily family, string name) => _used.TryGetValue(family, out var names) && names.Contains(name);

    public IReadOnlyCollection<string> Of(StyleFamily family) => _used.TryGetValue(family, out var names) ? names : [];

    public void Note(StyleFamily family, string? name)
    {
        if (string.IsNullOrEmpty(name)) return;
        if (!_used.TryGetValue(family, out var names)) _used[family] = names = new HashSet<string>(StringComparer.Ordinal);
        names.Add(name);
    }
}

public interface IStyleUsage
{
    StyleUsageReport Collect(TextDocument document);
}

public sealed class StyleUsage(IBlockTree tree) : IStyleUsage
{
    public StyleUsageReport Collect(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var report = new StyleUsageReport();
        report.Note(StyleFamily.Page, document.InitialPageStyle);
        foreach (var item in document.Objects.All)
        {
            report.Note(StyleFamily.Frame, item.StyleName);
            foreach (var story in item.Content.Stories) Visit(story, report);
        }
        Visit(document.Body, report);
        return report;
    }

    private void Visit(BlockContainer root, StyleUsageReport report)
    {
        foreach (var block in tree.Blocks(root))
        {
            switch (block)
            {
                case ParagraphBlock paragraph:
                    report.Note(StyleFamily.Paragraph, paragraph.Content.StyleName ?? DefaultStyleNames.Standard);
                    report.Note(StyleFamily.List, paragraph.Content.ListStyleName);
                    report.Note(StyleFamily.Page, paragraph.Content.PageStyleName);
                    foreach (var run in paragraph.Content.Runs) report.Note(StyleFamily.Character, run.StyleName);
                    break;
                case TableBlock table:
                    report.Note(StyleFamily.Table, table.StyleName);
                    break;
            }
        }
    }
}
