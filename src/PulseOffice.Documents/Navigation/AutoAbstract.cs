using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents.Navigation;

public sealed record AbstractLine(int Level, string Text, bool Heading);

public interface IAutoAbstract
{
    IReadOnlyList<AbstractLine> Lines(TextDocument document, int levels, int paragraphs);

    TextDocument Document(IReadOnlyList<AbstractLine> lines);
}

public sealed class AutoAbstract(IDocumentOutline outline, IBlockTree tree, ITextDocumentFactory documents) : IAutoAbstract
{
    public IReadOnlyList<AbstractLine> Lines(TextDocument document, int levels, int paragraphs)
    {
        ArgumentNullException.ThrowIfNull(document);
        var lines = new List<AbstractLine>();
        var remaining = 0;
        var level = 0;
        foreach (var paragraph in tree.Paragraphs(document.Body))
        {
            if (outline.LevelOf(document, paragraph) is { } heading)
            {
                if (heading <= levels)
                {
                    lines.Add(new AbstractLine(heading, paragraph.Text, true));
                    remaining = paragraphs;
                    level = heading + 1;
                }
                else remaining = 0;
                continue;
            }
            if (remaining <= 0 || paragraph.Text.Trim().Length == 0) continue;
            lines.Add(new AbstractLine(level, paragraph.Text, false));
            remaining--;
        }
        return lines;
    }

    public TextDocument Document(IReadOnlyList<AbstractLine> lines)
    {
        var created = documents.Create();
        if (lines.Count == 0) return created;
        created.Body.Replace(lines.Select(line => new ParagraphBlock(created.NextId("p"), line.Text,
            line.Heading ? DefaultStyleNames.HeadingLevel(line.Level) : DefaultStyleNames.TextBody)));
        return created;
    }
}
