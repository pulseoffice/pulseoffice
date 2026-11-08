using System.Globalization;
using System.Text;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents.Tests;

public sealed class Fixture
{
    public Fixture()
    {
        Tree = new BlockTree();
        Stories = new StoryCatalog(Tree);
        Text = new ParagraphText();
        Trails = new ContentTrails(Stories);
        Deletion = new RangeDeletion(Text, Tree, Trails, Anchoring, Stories);
        Outline = new Fields.DocumentOutline(Tree, Resolver);
        Bookmarks = new Fields.BookmarkCatalog(Stories);
        var conditions = new Fields.FieldCondition();
        var words = new Fields.InvariantFieldWords();
        Fields.IFieldValueSource[] sources =
        [
            new Fields.PropertyFieldSource(Cultures), new Fields.ClockFieldSource(Cultures), new Fields.StatisticFieldSource(new DocumentStatisticsCounter(), Tree, Cultures),
            new Fields.VariableFieldSource(conditions), new Fields.ConditionalFieldSource(conditions), new Fields.SequenceFieldSource(),
            new Fields.ChapterFieldSource(), new Fields.ReferenceFieldSource(Bookmarks, Stories, Outline, words),
            new Fields.NoteReferenceFieldSource(words), new Fields.CitationFieldSource(),
        ];
        FieldUpdater = new Fields.FieldUpdater(sources, new Fields.DocumentReadingOrder(Tree, Stories), new Fields.FieldRunRewriter(), Outline);
        Sequences = new SequenceFields(FieldUpdater);
        var scope = new Indexes.IndexScope(new Fields.DocumentReadingOrder(Tree, Stories), Tree);
        Indexes.IIndexEntrySource[] entries =
        [
            new Indexes.ContentsEntrySource(scope, Outline), new Indexes.AlphabeticalEntrySource(scope),
            new Indexes.CaptionEntrySource(scope), new Indexes.BibliographyEntrySource(scope), new Indexes.ObjectEntrySource(scope, new Indexes.IndexObjectClassifier()),
        ];
        var styles = new Indexes.IndexStyles();
        IndexEditing = new Indexes.IndexEditing(Stories, Tree, new Indexes.IndexComposer(entries, styles), styles);
        Editor = new DocumentEditor(Stories, Text, new DirectFormatting(), Trails, Deletion, Tree, Anchoring, Sequences, new TableEditing(Stories, Deletion),
            new Clipboard.FragmentPaster(Stories, Text, new Clipboard.FragmentAdoption(new Clipboard.FragmentRemapper(Stories, Tree), new Clipboard.StyleMerger()), Anchoring, Sequences));
        Factory = new TextDocumentFactory(new DefaultStyleSheetFactory());
        Document = Factory.Create();
        History = new UndoStack();
    }

    public ObjectAnchoring Anchoring { get; } = new();

    public SequenceFields Sequences { get; }

    public Fields.DocumentOutline Outline { get; }

    public Fields.BookmarkCatalog Bookmarks { get; }

    public Fields.FieldUpdater FieldUpdater { get; }

    public Indexes.IndexEditing IndexEditing { get; }

    public BlockTree Tree { get; }

    public PulseOffice.Text.Rich.ITextCultures Cultures { get; } = new PulseOffice.Text.Rich.TextCultures();

    public StoryCatalog Stories { get; }

    public DocumentEditor Editor { get; }

    public ParagraphText Text { get; }

    public ContentTrails Trails { get; }

    public RangeDeletion Deletion { get; }

    public TextDocumentFactory Factory { get; }

    public StyleResolver Resolver { get; } = new();

    public TextDocument Document { get; }

    public UndoStack History { get; }

    public ParagraphBlock First => (ParagraphBlock)Document.Body[0];

    public ParagraphBlock Paragraph(int index) => (ParagraphBlock)Document.Body[index];

    public DocumentPosition At(int paragraph, int offset) => new(Paragraph(paragraph).Id, offset);

    public void Type(params string[] paragraphs) =>
        Editor.InsertText(Document, History, new DocumentPosition(First.Id, 0), string.Join("\n", paragraphs));

    public string Describe() => Describe(Document.Body);

    public string Snapshot()
    {
        var builder = new StringBuilder();
        foreach (var story in Stories.Stories(Document)) builder.Append(Deep(story, 0)).Append("--\n");
        builder.Append("notes:").AppendJoin(',', Document.Notes.All.Select(note => note.Id)).Append('\n');
        builder.Append("comments:").AppendJoin(',', Document.Comments.All.Select(comment => comment.Id + "^" + comment.ParentId)).Append('\n');
        builder.Append("objects:").AppendJoin(',', Document.Objects.All.Select(item => item.Id + "@" + item.Anchor)).Append('\n');
        builder.Append("pstyles:").AppendJoin(',', Document.Styles.Paragraph.All.Select(style => style.Name)).Append('\n');
        builder.Append("cstyles:").AppendJoin(',', Document.Styles.Character.All.Select(style => style.Name)).Append('\n');
        return builder.ToString();
    }

    public string Deep(BlockContainer container, int depth)
    {
        var builder = new StringBuilder();
        var indent = new string(' ', depth * 2);
        foreach (var block in container.Blocks)
        {
            switch (block)
            {
                case ParagraphBlock paragraph:
                    builder.Append(indent).Append(Describe(new BlockContainer([paragraph])).TrimEnd('\n'))
                        .Append(" page=").Append(paragraph.Content.PageStyleName).Append('/').Append(paragraph.Content.PageNumberRestart).Append('\n');
                    break;
                case TableBlock table:
                    builder.Append(indent).Append(table.Id).Append(" table\n");
                    foreach (var cell in table.Rows.SelectMany(row => row.Cells)) builder.Append(indent).Append(" cell\n").Append(Deep(cell.Content, depth + 1));
                    break;
                case SectionBlock section:
                    builder.Append(indent).Append(section.Id).Append(" section\n").Append(Deep(section.Content, depth + 1));
                    break;
            }
        }
        return builder.ToString();
    }

    public string Describe(BlockContainer container)
    {
        var builder = new StringBuilder();
        foreach (var block in container.Blocks)
        {
            switch (block)
            {
                case ParagraphBlock paragraph:
                    builder.Append(paragraph.Id).Append('[').Append(paragraph.StyleName).Append("]{");
                    foreach (var run in paragraph.Content.Runs)
                    {
                        builder.Append('(').Append(run.Text).Append(run.Bold ? "|b" : string.Empty)
                            .Append(run.StyleName is null ? string.Empty : "|" + run.StyleName)
                            .Append(run.Field == default ? string.Empty : "|" + run.Field).Append(')');
                    }
                    builder.Append('}');
                    foreach (var mark in paragraph.Marks) builder.Append('<').Append(mark.Key).Append('@').Append(mark.Offset.ToString(CultureInfo.InvariantCulture)).Append('>');
                    builder.Append('\n');
                    break;
                case TableBlock table:
                    builder.Append(table.Id).Append("[table ").Append(table.Rows.Count).Append('x').Append(table.ColumnCount).Append("]\n");
                    break;
                default:
                    builder.Append(block.Id).Append('\n');
                    break;
            }
        }
        return builder.ToString();
    }
}
