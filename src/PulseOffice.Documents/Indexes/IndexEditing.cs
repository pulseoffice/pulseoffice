using System.Globalization;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Indexes;

public interface IIndexStyles
{
    string Heading(IndexKind kind);

    string Entry(IndexKind kind, int level);

    string Heading(IndexDefinition definition);

    string Entry(IndexDefinition definition, int level);

    string SectionName(IndexKind kind);
}

public sealed class IndexStyles : IIndexStyles
{
    public string Heading(IndexKind kind) => kind switch
    {
        IndexKind.Contents => DefaultStyleNames.ContentsHeading,
        IndexKind.Alphabetical => DefaultStyleNames.IndexHeading,
        IndexKind.Illustrations => DefaultStyleNames.IllustrationIndexHeading,
        IndexKind.Tables => DefaultStyleNames.TableIndexHeading,
        IndexKind.Bibliography => DefaultStyleNames.BibliographyHeading,
        IndexKind.Objects => DefaultStyleNames.ObjectIndexHeading,
        _ => DefaultStyleNames.UserIndexHeading,
    };

    public string Entry(IndexKind kind, int level) => kind switch
    {
        IndexKind.Contents => DefaultStyleNames.ContentsLevel(Math.Clamp(level, 1, 10)),
        IndexKind.Alphabetical => level == 0 ? DefaultStyleNames.IndexSeparator : DefaultStyleNames.IndexLevel(Math.Clamp(level, 1, 3)),
        IndexKind.Illustrations => DefaultStyleNames.IllustrationIndex1,
        IndexKind.Tables => DefaultStyleNames.TableIndex1,
        IndexKind.Bibliography => DefaultStyleNames.Bibliography1,
        IndexKind.Objects => DefaultStyleNames.ObjectIndex1,
        _ => DefaultStyleNames.UserIndexLevel(Math.Clamp(level, 1, 10)),
    };

    public string Heading(IndexDefinition definition) => definition.LevelStyles.Heading ?? Heading(definition.Kind);

    public string Entry(IndexDefinition definition, int level) => definition.LevelStyles.Entry(level) ?? Entry(definition.Kind, level);

    public string SectionName(IndexKind kind) => kind switch
    {
        IndexKind.Contents => "Table of Contents",
        IndexKind.Alphabetical => "Alphabetical Index",
        IndexKind.Illustrations => "Illustration Index",
        IndexKind.Tables => "Index of Tables",
        IndexKind.Bibliography => "Bibliography",
        IndexKind.Objects => "Table of Objects",
        _ => "User-Defined",
    };
}

public interface IIndexComposer
{
    IReadOnlyList<Block> Compose(TextDocument document, IndexDefinition definition, IFieldLayoutFacts facts);
}

public sealed class IndexComposer(IEnumerable<IIndexEntrySource> sources, IIndexStyles styles) : IIndexComposer
{
    private readonly IReadOnlyDictionary<IndexKind, IIndexEntrySource> _sources =
        sources.SelectMany(source => source.Kinds.Select(kind => (kind, source))).ToDictionary(pair => pair.kind, pair => pair.source);

    public IReadOnlyList<Block> Compose(TextDocument document, IndexDefinition definition, IFieldLayoutFacts facts)
    {
        var blocks = new List<Block>();
        if (definition.Title.Length > 0)
            blocks.Add(new ParagraphBlock(document.NextId("p"), definition.Title, styles.Heading(definition)));
        foreach (var entry in _sources[definition.Kind].Collect(document, definition))
            blocks.Add(Paragraph(document, definition, entry, facts));
        if (blocks.Count == 0) blocks.Add(new ParagraphBlock(document.NextId("p"), string.Empty, styles.Entry(definition, 1)));
        return blocks;
    }

    private ParagraphBlock Paragraph(TextDocument document, IndexDefinition definition, IndexEntry entry, IFieldLayoutFacts facts)
    {
        var runs = new List<TextRun>();
        var link = definition.Hyperlinks && entry.Link is { } target ? target : null;
        if (entry.Prefix is { } prefix) runs.Add(new TextRun(prefix + " "));
        runs.Add(new TextRun(entry.Text) { Hyperlink = link, StyleName = link is null ? null : DefaultStyleNames.IndexLink });
        if (definition.PageNumbers && entry.ParagraphIds.Count > 0)
        {
            var pages = entry.ParagraphIds.Select(id => facts.PageOf(id) ?? "?").Distinct(StringComparer.Ordinal).ToList();
            var alphabetical = definition.Kind is IndexKind.Alphabetical;
            runs.Add(new TextRun(alphabetical ? ", " : "\t"));
            runs.Add(new TextRun(string.Join(", ", pages)) { Hyperlink = link, StyleName = link is null ? null : DefaultStyleNames.IndexLink });
        }
        var content = new TextParagraph(string.Empty) { StyleName = styles.Entry(definition, entry.Level) }.WithRuns(runs);
        return new ParagraphBlock(document.NextId("p"), content);
    }
}

public interface IIndexEditing
{
    IReadOnlyList<SectionBlock> Indexes(TextDocument document);

    SectionBlock? Around(TextDocument document, string paragraphId);

    DocumentEdit Insert(TextDocument document, UndoStack history, DocumentPosition at, IndexDefinition definition, IFieldLayoutFacts facts);

    DocumentEdit Update(TextDocument document, UndoStack history, IFieldLayoutFacts facts, string? sectionId = null);

    void Refresh(TextDocument document, EditRecorder recorder, IFieldLayoutFacts facts, string? sectionId = null);

    DocumentEdit Change(TextDocument document, UndoStack history, string sectionId, IndexDefinition definition, IFieldLayoutFacts facts);

    DocumentEdit Remove(TextDocument document, UndoStack history, string sectionId);
}

public sealed class IndexEditing(IStoryCatalog stories, IBlockTree tree, IIndexComposer composer, IIndexStyles styles) : IIndexEditing
{
    public IReadOnlyList<SectionBlock> Indexes(TextDocument document) =>
        [.. tree.Blocks(document.Body).OfType<SectionBlock>().Where(section => section.IsIndex)];

    public SectionBlock? Around(TextDocument document, string paragraphId) =>
        Indexes(document).FirstOrDefault(section => tree.Paragraphs(section.Content).Any(paragraph => paragraph.Id == paragraphId));

    public DocumentEdit Insert(TextDocument document, UndoStack history, DocumentPosition at, IndexDefinition definition, IFieldLayoutFacts facts)
    {
        var found = stories.Find(document, at.ParagraphId);
        var container = found.Location.Container;
        var index = found.Location.Index + (at.Offset > 0 && at.Offset >= found.Paragraph.Length ? 1 : 0);
        var recorder = new EditRecorder(document, "insert-index").Touch(container);
        var count = Indexes(document).Count(section => section.Index!.Kind == definition.Kind) + 1;
        var section = new SectionBlock(document.NextId("s"), styles.SectionName(definition.Kind) + count.ToString(CultureInfo.InvariantCulture),
            new BlockContainer(composer.Compose(document, definition, facts)))
        {
            Index = definition,
            Protected = true,
        };
        container.Insert(index, section);
        recorder.Record(new BlockRestored(section.Id));
        return recorder.Commit(history);
    }

    public DocumentEdit Update(TextDocument document, UndoStack history, IFieldLayoutFacts facts, string? sectionId = null)
    {
        var recorder = new EditRecorder(document, "update-index");
        Refresh(document, recorder, facts, sectionId);
        return recorder.Commit(history);
    }

    public void Refresh(TextDocument document, EditRecorder recorder, IFieldLayoutFacts facts, string? sectionId = null)
    {
        foreach (var section in Indexes(document).Where(section => sectionId is null || section.Id == sectionId))
        {
            var fresh = composer.Compose(document, section.Index!, facts);
            if (Same(section.Content.Blocks, fresh)) continue;
            recorder.Touch(section.Content);
            section.Content.Replace(fresh);
        }
    }

    public DocumentEdit Change(TextDocument document, UndoStack history, string sectionId, IndexDefinition definition, IFieldLayoutFacts facts)
    {
        var recorder = new EditRecorder(document, "change-index");
        if (Locate(document, sectionId) is not { } location) return recorder.Commit(history);
        var old = (SectionBlock)location.Container[location.Index];
        recorder.Touch(location.Container);
        var section = new SectionBlock(old.Id, old.Name, new BlockContainer(composer.Compose(document, definition, facts)))
        {
            Index = definition,
            Protected = old.Protected,
            Columns = old.Columns,
        };
        location.Container.RemoveAt(location.Index);
        location.Container.Insert(location.Index, section);
        return recorder.Commit(history);
    }

    public DocumentEdit Remove(TextDocument document, UndoStack history, string sectionId)
    {
        var recorder = new EditRecorder(document, "remove-index");
        if (Locate(document, sectionId) is not { } location) return recorder.Commit(history);
        recorder.Touch(location.Container);
        location.Container.RemoveAt(location.Index);
        if (location.Container.Count == 0) location.Container.Add(new ParagraphBlock(document.NextId("p"), string.Empty, DefaultStyleNames.TextBody));
        return recorder.Commit(history);
    }

    private bool Same(IReadOnlyList<Block> current, IReadOnlyList<Block> fresh) =>
        current.Count == fresh.Count && current.Zip(fresh).All(pair =>
            pair.First is ParagraphBlock a && pair.Second is ParagraphBlock b && a.Content.Runs.SequenceEqual(b.Content.Runs) && a.StyleName == b.StyleName);

    private (BlockContainer Container, int Index)? Locate(TextDocument document, string sectionId) =>
        tree.Locate(document.Body, sectionId) is { Block: SectionBlock } location ? (location.Container, location.Index) : null;
}

public interface IGeneratedRegions
{
    bool Contains(TextDocument document, string paragraphId);
}

public sealed class GeneratedRegions(IBlockTree tree) : IGeneratedRegions
{
    public bool Contains(TextDocument document, string paragraphId)
    {
        if (tree.Locate(document.Body, paragraphId) is not { } location) return false;
        var container = document.Body;
        var nested = new NestedContainers();
        var steps = location.Path.Steps;
        var position = 0;
        while (position < steps.Count)
        {
            var block = container[steps[position++]];
            if (block is SectionBlock { IsIndex: true }) return true;
            if (position == steps.Count) return false;
            var match = block.Accept(nested).FirstOrDefault(inner => inner.Steps.Select((step, i) => position + i < steps.Count && steps[position + i] == step).All(equal => equal));
            if (match is null) return false;
            position += match.Steps.Count;
            container = match.Container;
        }
        return false;
    }
}
