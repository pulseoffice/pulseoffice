using PulseOffice.Documents.Layout.Tree;

namespace PulseOffice.Documents.Layout.Flow;

public interface ISectionPageRuns
{
    IReadOnlyList<SectionPages> Sections(IReadOnlyList<PageBox> pages, IReadOnlyList<FlowItem> flow);
}

public sealed class SectionPageRuns : ISectionPageRuns
{
    public IReadOnlyList<SectionPages> Sections(IReadOnlyList<PageBox> pages, IReadOnlyList<FlowItem> flow)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(flow);
        var styleStarts = new HashSet<int>();
        for (var i = 1; i < pages.Count; i++)
            if (!string.Equals(pages[i - 1].StyleName, pages[i].StyleName, StringComparison.Ordinal)) styleStarts.Add(pages[i].Start.FlowIndex);
        var boundaries = Boundaries(flow).Distinct().Where(index => !styleStarts.Contains(index)).ToList();
        var numbers = new int[pages.Count];
        var styleChanges = 0;
        for (var i = 0; i < pages.Count; i++)
        {
            if (i > 0 && !string.Equals(pages[i - 1].StyleName, pages[i].StyleName, StringComparison.Ordinal)) styleChanges++;
            var top = pages[i].Start.FlowIndex;
            numbers[i] = 1 + styleChanges + boundaries.Count(index => index <= top);
        }

        var result = new SectionPages[pages.Count];
        var start = 0;
        for (var i = 1; i <= pages.Count; i++)
        {
            if (i < pages.Count && numbers[i] == numbers[i - 1]) continue;
            var run = new SectionPages(numbers[start], i - start);
            for (var j = start; j < i; j++) result[j] = run;
            start = i;
        }
        return result;
    }

    private IEnumerable<int> Boundaries(IReadOnlyList<FlowItem> flow)
    {
        var depth = 0;
        var counted = false;
        for (var index = 0; index < flow.Count; index++)
        {
            switch (flow[index])
            {
                case SectionStartItem start:
                    if (depth++ != 0) break;
                    counted = Counts(start.Section);
                    if (counted && index > 0) yield return index;
                    break;
                case SectionEndItem:
                    if (--depth == 0 && counted && index < flow.Count - 1) yield return index + 1;
                    break;
            }
        }
    }

    private bool Counts(PulseOffice.Documents.Blocks.SectionBlock section) => section.Index is null || section.Columns.Count > 1;
}
