using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents.Editing;

public interface IListThreads
{
    void Settle(TextDocument document);
}

public sealed class ListThreads(IStoryCatalog stories, IBlockTree tree, IStyleResolver resolver) : IListThreads
{
    public const string NamePrefix = "list";

    public void Settle(TextDocument document)
    {
        var names = new ThreadNames();
        foreach (var story in stories.Stories(document).ToList()) Settle(document, tree.Paragraphs(story).ToList(), names);
    }

    private void Settle(TextDocument document, List<ParagraphBlock> paragraphs, ThreadNames names)
    {
        var sheet = document.Styles;
        var members = new List<(ParagraphBlock Paragraph, string Style, string Raw)>();
        foreach (var paragraph in paragraphs)
        {
            if (paragraph.Content.ListId is not { } raw) continue;
            var style = paragraph.Content.ListStyleName ?? resolver.ResolveParagraphStyle(sheet, paragraph.Content.StyleName).ListStyleName;
            if (style is null)
            {
                paragraph.Content = paragraph.Content with { ListId = null };
                continue;
            }
            members.Add((paragraph, style, raw));
        }

        foreach (var family in members.GroupBy(member => member.Style, StringComparer.Ordinal))
        {
            var sequence = family.ToList();
            var last = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < sequence.Count; index++) last[sequence[index].Raw] = index;
            var settled = new Dictionary<string, string?>(StringComparer.Ordinal);
            string? holder = null;
            for (var index = 0; index < sequence.Count; index++)
            {
                var (paragraph, style, raw) = sequence[index];
                if (!settled.TryGetValue(raw, out var thread))
                {
                    if (holder is not null && last[holder] > index)
                    {
                        thread = names.Next();
                        Fresh(sheet, sequence, index, style);
                    }
                    else
                    {
                        thread = null;
                        if (holder is not null) Restart(sheet, sequence, index, style);
                        holder = raw;
                    }
                    settled[raw] = thread;
                }
                if (paragraph.Content.ListId != thread) paragraph.Content = paragraph.Content with { ListId = thread };
            }
        }
    }

    private void Fresh(StyleSheet sheet, List<(ParagraphBlock Paragraph, string Style, string Raw)> sequence, int from, string style)
    {
        if (First(sequence, from) is not { } first || first.Content.ListRestart is not { } restart) return;
        if (sheet.List.Find(style)?.Level(first.Content.BulletLevel).StartAt == restart) first.Content = first.Content with { ListRestart = null };
    }

    private ParagraphBlock? First(List<(ParagraphBlock Paragraph, string Style, string Raw)> sequence, int from)
    {
        var raw = sequence[from].Raw;
        return sequence.Skip(from).Where(member => member.Raw == raw).Select(member => member.Paragraph).FirstOrDefault(paragraph => !paragraph.Content.ListHeader);
    }

    private void Restart(StyleSheet sheet, List<(ParagraphBlock Paragraph, string Style, string Raw)> sequence, int from, string style)
    {
        if (First(sequence, from) is not { } first || first.Content.ListRestart is not null) return;
        var start = sheet.List.Find(style)?.Level(first.Content.BulletLevel).StartAt ?? 1;
        first.Content = first.Content with { ListRestart = start };
    }

    private sealed class ThreadNames
    {
        private int _count;

        public string Next() => NamePrefix + (++_count).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
