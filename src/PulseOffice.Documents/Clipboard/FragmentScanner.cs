using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents.Clipboard;

public interface IFragmentScanner
{
    void Scan(BlockContainer container, FragmentReferences references);
}

public sealed class FragmentScanner(IBlockTree tree) : IFragmentScanner
{
    public void Scan(BlockContainer container, FragmentReferences references)
    {
        foreach (var block in tree.Blocks(container))
        {
            if (block is TableBlock table && table.StyleName is not null) references.TableStyles.Add(table.StyleName);
            if (block is not ParagraphBlock paragraph) continue;
            references.Paragraphs.Add(paragraph.Id);
            Add(references.ParagraphStyles, paragraph.Content.StyleName);
            Add(references.ListStyles, paragraph.Content.ListStyleName);
            Add(references.PageStyles, paragraph.Content.PageStyleName);
            foreach (var run in paragraph.Content.Runs) Add(references.CharacterStyles, run.StyleName);
            foreach (var mark in paragraph.Marks)
            {
                if (mark is NoteMark note && !references.Notes.Contains(note.NoteId)) references.Notes.Add(note.NoteId);
                if (mark is CommentMark comment && !references.Comments.Contains(comment.CommentId)) references.Comments.Add(comment.CommentId);
                if (mark is ObjectMark item && !references.Objects.Contains(item.ObjectId)) references.Objects.Add(item.ObjectId);
                if (mark is ChangeMark change && !references.Changes.Contains(change.ChangeId)) references.Changes.Add(change.ChangeId);
            }
        }
    }

    private void Add(HashSet<string> names, string? name)
    {
        if (name is not null) names.Add(name);
    }
}

public interface IStyleClosure
{
    StyleSheet Collect(StyleSheet source, FragmentReferences references);
}

public sealed class StyleClosure(IFragmentScanner scanner) : IStyleClosure
{
    public StyleSheet Collect(StyleSheet source, FragmentReferences references)
    {
        var result = new StyleSheet { DefaultCharacter = source.DefaultCharacter, DefaultParagraph = source.DefaultParagraph };
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var style in Pending(source.Paragraph, result.Paragraph, references.ParagraphStyles))
            {
                Add(references.ParagraphStyles, style.ParentName);
                Add(references.ParagraphStyles, style.NextStyleName);
                Add(references.ListStyles, style.ListStyleName);
                Add(references.PageStyles, style.Paragraph.PageStyleName);
                changed = true;
            }
            foreach (var style in Pending(source.Character, result.Character, references.CharacterStyles))
            {
                Add(references.CharacterStyles, style.ParentName);
                changed = true;
            }
            foreach (var style in Pending(source.List, result.List, references.ListStyles))
            {
                Add(references.ListStyles, style.ParentName);
                foreach (var level in style.Levels) Add(references.CharacterStyles, level.CharacterStyleName);
                changed = true;
            }
            foreach (var style in Pending(source.Page, result.Page, references.PageStyles))
            {
                Add(references.PageStyles, style.ParentName);
                Add(references.PageStyles, style.NextStyleName);
                foreach (var part in new[] { style.Header, style.Footer })
                {
                    foreach (var content in new[] { part.Default, part.First, part.Even })
                    {
                        if (content is not null) scanner.Scan(content, references);
                    }
                }
                changed = true;
            }
            foreach (var style in Pending(source.Frame, result.Frame, references.FrameStyles))
            {
                Add(references.FrameStyles, style.ParentName);
                changed = true;
            }
            foreach (var style in Pending(source.Table, result.Table, references.TableStyles))
            {
                Add(references.TableStyles, style.ParentName);
                changed = true;
            }
        }
        return result;
    }

    private List<TStyle> Pending<TStyle>(StyleCollection<TStyle> source, StyleCollection<TStyle> target, HashSet<string> names) where TStyle : DocumentStyle
    {
        var added = new List<TStyle>();
        foreach (var name in names.ToList())
        {
            if (target.Contains(name) || source.Find(name) is not { } style) continue;
            target.Add((TStyle)style.Clone());
            added.Add(style);
        }
        return added;
    }

    private void Add(HashSet<string> names, string? name)
    {
        if (name is not null) names.Add(name);
    }
}
