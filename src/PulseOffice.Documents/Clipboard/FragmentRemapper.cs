using System.Globalization;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents.Clipboard;

public interface IFragmentRemapper
{
    DocumentFragment Remap(TextDocument target, DocumentFragment fragment);
}

public sealed class FragmentRemapper(IStoryCatalog stories, IBlockTree tree) : IFragmentRemapper
{
    public DocumentFragment Remap(TextDocument target, DocumentFragment fragment)
    {
        var session = new RemapSession(target, Used(target), Bookmarks(target), ObjectNames(target), Threads(target));
        foreach (var note in fragment.Notes) session.Notes[note.Id] = session.Fresh(note.Id);
        foreach (var comment in fragment.Comments) session.Comments[comment.Id] = session.Fresh(comment.Id);
        foreach (var item in fragment.Objects) session.Objects[item.Id] = session.Fresh(item.Id);
        foreach (var change in fragment.Changes) session.Changes[change.Id] = session.Fresh(change.Id);
        var blocks = session.Container(fragment.Blocks);
        var notes = fragment.Notes.Select(note => new Note(session.Notes[note.Id], note.Kind, session.Container(note.Content)) { Citation = note.Citation }).ToList();
        var comments = fragment.Comments.Select(comment => new Comment(session.Comments[comment.Id], comment.Author, comment.Date, session.Container(comment.Content))
        {
            Initials = comment.Initials,
            ParentId = comment.ParentId is null ? null : session.Comments.GetValueOrDefault(comment.ParentId),
            Resolved = comment.Resolved,
        }).ToList();
        var styles = fragment.Styles.Clone();
        foreach (var page in styles.Page.All)
        {
            foreach (var part in new[] { page.Header, page.Footer })
            {
                foreach (var content in new[] { part.Default, part.First, part.Even })
                {
                    if (content is not null) content.Replace(session.Container(content).Blocks);
                }
            }
        }
        var objects = fragment.Objects.Select(item => session.Object(item)).ToList();
        var changes = fragment.Changes.Select(change => change.CopyAs(session.Changes[change.Id], session.Changes, session.Container)).ToList();
        return new DocumentFragment(blocks, styles, notes, comments, objects, changes);
    }

    private HashSet<string> Used(TextDocument document)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var story in stories.Stories(document))
        {
            foreach (var block in tree.Blocks(story)) used.Add(block.Id);
        }
        foreach (var note in document.Notes.All) used.Add(note.Id);
        foreach (var comment in document.Comments.All) used.Add(comment.Id);
        foreach (var item in document.Objects.All) used.Add(item.Id);
        foreach (var change in document.Changes.All) used.Add(change.Id);
        return used;
    }

    private HashSet<string> Bookmarks(TextDocument document) =>
        stories.Paragraphs(document).SelectMany(paragraph => paragraph.Marks).OfType<BookmarkMark>().Select(mark => mark.Name).ToHashSet(StringComparer.Ordinal);

    private HashSet<string> Threads(TextDocument document) =>
        stories.Paragraphs(document).Select(paragraph => paragraph.Content.ListId).OfType<string>().ToHashSet(StringComparer.Ordinal);

    private HashSet<string> ObjectNames(TextDocument document) =>
        document.Objects.All.Select(item => item.Name).OfType<string>().ToHashSet(StringComparer.Ordinal);

    private sealed class RemapSession(TextDocument target, HashSet<string> used, HashSet<string> bookmarks, HashSet<string> objectNames, HashSet<string> threads)
        : IBlockVisitor<Block>
    {
        private readonly Dictionary<string, string> _threads = new(StringComparer.Ordinal);

        private readonly Dictionary<string, string> _bookmarks = new(StringComparer.Ordinal);

        private readonly Dictionary<string, string> _controls = new(StringComparer.Ordinal);

        private readonly Dictionary<string, string> _fields = new(StringComparer.Ordinal);

        public Dictionary<string, string> Notes { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Comments { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Objects { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Paragraphs { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Changes { get; } = new(StringComparer.Ordinal);

        public string Fresh(string old)
        {
            var digits = old.Reverse().TakeWhile(char.IsAsciiDigit).Count();
            var prefix = old[..(old.Length - digits)];
            if (prefix.Length == 0) prefix = "b";
            string candidate;
            do
            {
                candidate = target.NextId(prefix);
            }
            while (!used.Add(candidate));
            return candidate;
        }

        private string Thread(string source)
        {
            if (_threads.TryGetValue(source, out var mapped)) return mapped;
            var number = threads.Count;
            do
            {
                mapped = Editing.ListThreads.NamePrefix + (++number).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            while (!threads.Add(mapped));
            _threads[source] = mapped;
            return mapped;
        }

        public BlockContainer Container(BlockContainer source) => new(source.Blocks.Select(block => block.Accept(this)));

        public Block VisitParagraph(ParagraphBlock paragraph)
        {
            var id = Fresh(paragraph.Id);
            Paragraphs[paragraph.Id] = id;
            var content = paragraph.Content.ListId is { } thread ? paragraph.Content with { ListId = Thread(thread) } : paragraph.Content;
            return new ParagraphBlock(id, content) { Marks = [.. paragraph.Marks.Select(Mark).OfType<InlineMark>()] };
        }

        public Block VisitTable(TableBlock table) =>
            new TableBlock(Fresh(table.Id), table.Rows.Select(Row), table.Columns)
            {
                Name = table.Name,
                StyleName = table.StyleName,
                HeaderRows = table.HeaderRows,
                RepeatHeaderRows = table.RepeatHeaderRows,
                Direct = table.Direct,
                PageStyleName = table.PageStyleName,
                PageBreakBefore = table.PageBreakBefore,
                Alignment = table.Alignment,
                Indent = table.Indent,
            };

        public Block VisitSection(SectionBlock section) =>
            new SectionBlock(Fresh(section.Id), section.Name, Container(section.Content))
            {
                Columns = section.Columns,
                Protected = section.Protected,
                ProtectionKey = section.ProtectionKey,
                Hidden = section.Hidden,
                Condition = section.Condition,
                LinkedFile = section.LinkedFile,
                Index = section.Index,
            };

        public AnchoredObject Object(AnchoredObject item)
        {
            var anchor = item.Anchor.Accept(new AnchorParagraph()) is { } paragraph && Paragraphs.TryGetValue(paragraph, out var mapped)
                ? item.Anchor.Accept(new AnchorRetarget(mapped))
                : item.Anchor;
            var content = item.Content.Story is { } story ? item.Content.WithStory(Container(story)) : item.Content.Clone();
            var copy = item.CopyAs(Objects[item.Id], anchor, content);
            copy.Name = item.Name is null ? null : Unique(item.Name, objectNames);
            return copy;
        }

        private TableRow Row(TableRow row) =>
            new(row.Cells.Select(cell => new TableCell(Container(cell.Content))
            {
                ColumnSpan = cell.ColumnSpan,
                RowSpan = cell.RowSpan,
                Covered = cell.Covered,
                StyleName = cell.StyleName,
                Background = cell.Background,
                VerticalAlign = cell.VerticalAlign,
                Borders = cell.Borders,
                Formula = cell.Formula,
                Protected = cell.Protected,
                NumberFormat = cell.NumberFormat,
                TextDirection = cell.TextDirection,
                Padding = cell.Padding,
            }))
            {
                Height = row.Height,
                MinimumHeight = row.MinimumHeight,
                AllowBreakAcrossPages = row.AllowBreakAcrossPages,
            };

        private InlineMark? Mark(InlineMark mark) => mark switch
        {
            ChangeMark change => Changes.TryGetValue(change.ChangeId, out var id) ? change with { ChangeId = id } : null,
            NoteMark note => Notes.TryGetValue(note.NoteId, out var id) ? note with { NoteId = id } : null,
            CommentMark comment => Comments.TryGetValue(comment.CommentId, out var id) ? comment with { CommentId = id } : null,
            ObjectMark item => Objects.TryGetValue(item.ObjectId, out var id) ? item with { ObjectId = id } : null,
            BookmarkMark bookmark => bookmark with { Name = Bookmark(bookmark.Name) },
            ContentControlMark control => control with { ControlId = Control(control.ControlId) },
            FieldCodeMark field => field with { FieldId = Field(field.FieldId) },
            _ => mark,
        };

        private string Control(string id) => Renamed(_controls, id, "cc");

        private string Field(string id) => Renamed(_fields, id, "field");

        private string Renamed(Dictionary<string, string> renames, string id, string prefix)
        {
            if (renames.TryGetValue(id, out var renamed)) return renamed;
            renamed = target.NextId(prefix);
            renames[id] = renamed;
            return renamed;
        }

        private string Bookmark(string name)
        {
            if (_bookmarks.TryGetValue(name, out var renamed)) return renamed;
            renamed = Unique(name, bookmarks);
            _bookmarks[name] = renamed;
            return renamed;
        }

        private string Unique(string name, HashSet<string> taken)
        {
            var candidate = name;
            for (var counter = 1; !taken.Add(candidate); counter++)
            {
                candidate = name + counter.ToString(CultureInfo.InvariantCulture);
            }
            return candidate;
        }
    }
}
