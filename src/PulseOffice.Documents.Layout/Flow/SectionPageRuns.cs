using PulseOffice.Documents.Layout.Tree;

namespace PulseOffice.Documents.Layout.Flow;

public interface ISectionPageRuns
{
    IReadOnlyList<SectionPages> Sections(IReadOnlyList<PageBox> pages);
}

public sealed class SectionPageRuns : ISectionPageRuns
{
    public IReadOnlyList<SectionPages> Sections(IReadOnlyList<PageBox> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var result = new SectionPages[pages.Count];
        var start = 0;
        var section = 0;
        for (var i = 1; i <= pages.Count; i++)
        {
            if (i < pages.Count && string.Equals(pages[i - 1].StyleName, pages[i].StyleName, StringComparison.Ordinal)) continue;
            section++;
            var run = new SectionPages(section, i - start);
            for (var j = start; j < i; j++) result[j] = run;
            start = i;
        }
        return result;
    }
}
