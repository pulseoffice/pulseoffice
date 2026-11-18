using System.Runtime.CompilerServices;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents.Layout.Queries;

public interface ILineNumbers
{
    IReadOnlyDictionary<LineBox, int> Number(DocumentLayout layout, LineNumbering numbering);
}

public sealed class LineNumbers : ILineNumbers
{
    private sealed record Numbered(LineNumbering Numbering, IReadOnlyDictionary<LineBox, int> Numbers);

    private readonly ConditionalWeakTable<DocumentLayout, Numbered> _cache = [];

    public IReadOnlyDictionary<LineBox, int> Number(DocumentLayout layout, LineNumbering numbering)
    {
        if (_cache.TryGetValue(layout, out var cached) && cached.Numbering == numbering) return cached.Numbers;
        var numbers = new Dictionary<LineBox, int>(ReferenceEqualityComparer.Instance);
        var start = Math.Max(1, numbering.Start) - 1;
        var count = start;
        string? section = null;
        foreach (var page in layout.Pages)
        {
            if (numbering.RestartEachPage || numbering.RestartEachSection && section is not null && section != page.StyleName) count = start;
            section = page.StyleName;
            foreach (var line in page.Lines.Where(line => line.Story == StoryKind.Body).OrderBy(line => line.Top).ThenBy(line => line.Left))
            {
                if (!numbering.CountBlankLines && line.Line.Runs.All(run => run.Text.Trim().Length == 0)) continue;
                numbers[line] = ++count;
            }
        }
        _cache.AddOrUpdate(layout, new Numbered(numbering, numbers));
        return numbers;
    }
}
