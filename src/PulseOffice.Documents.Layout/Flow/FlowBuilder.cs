using System.Runtime.CompilerServices;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Inline;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Layout.Flow;

public sealed class NoteNumbers(IReadOnlyDictionary<string, string> citations, IReadOnlyList<string> endnotes)
{
    public static readonly NoteNumbers None = new(new Dictionary<string, string>(), []);

    public IReadOnlyDictionary<string, string> Citations { get; } = citations;

    public IReadOnlyList<string> Endnotes { get; } = endnotes;
}

public sealed record FlowContext(TextDocument Document, StoryKind Story, string? StoryId, bool Sections, IReadOnlyDictionary<string, ListPlacement?> Lists, NoteNumbers Notes)
{
    public Func<TextParagraph, TextParagraph>? Fields { get; init; }

    public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string>();

    public string? DefaultStyle { get; init; }

    public string PageCount { get; init; } = "1";
}

public sealed record ObjectSnapshot(string Id, string Anchor, double Width, double Height, double X, double Y, HorizontalRelation Horizontal, VerticalRelation Vertical, WrapMode Wrap, bool Background, int ZOrder)
{
    public PulseOffice.Foundation.Units.Margins Spacing { get; init; }

    public IReadOnlyList<TextParagraph> Text { get; init; } = [];

    public string Outline { get; init; } = string.Empty;

    public bool Equals(ObjectSnapshot? other) =>
        other is not null && Id == other.Id && Anchor == other.Anchor && Width == other.Width && Height == other.Height && X == other.X && Y == other.Y
        && Horizontal == other.Horizontal && Vertical == other.Vertical && Wrap == other.Wrap && Background == other.Background && ZOrder == other.ZOrder
        && Spacing == other.Spacing && Outline == other.Outline && Text.Count == other.Text.Count && Text.Zip(other.Text).All(pair => ReferenceEquals(pair.First, pair.Second));

    public override int GetHashCode() => HashCode.Combine(Id, Anchor, Width, Height, X, Y, Wrap, Text.Count);
}

public interface IFlowBuilder
{
    IReadOnlyList<FlowItem> Build(BlockContainer container, FlowContext context);

    IReadOnlyDictionary<string, ListPlacement?> Lists(TextDocument document, BlockContainer root);

    NoteNumbers Number(TextDocument document, IReadOnlyDictionary<string, int>? onPage = null);

    IReadOnlyList<FlowItem> Notes(string noteId, FlowContext context);

    ParagraphEntry PerPage(ParagraphEntry entry, string number, FlowContext context);

    void Clear();
}

public interface IHiddenParagraphs
{
    bool Show { get; set; }
}

public sealed class HiddenParagraphs : IHiddenParagraphs
{
    public bool Show { get; set; }
}

public sealed class FlowBuilder(IBlockTree tree, IParagraphEntries entries, IListLabeling labeling, IEnumerable<INumberFormatter> formatters, IPageFieldText fields, PageFlowOptions options, ITableAppearance appearance, IChangeMarkup markup, IHiddenParagraphs hidden, IPlaceholderMarkup placeholders) : IFlowBuilder
{
    private readonly IReadOnlyDictionary<NumberFormat, INumberFormatter> _formatters = formatters.ToDictionary(formatter => formatter.Format);
    private readonly ConditionalWeakTable<TextParagraph, TextParagraph> _styled = new();
    private readonly ConditionalWeakTable<TextParagraph, TextParagraph> _headers = new();
    private readonly AnchorParagraph _anchors = new();

    private readonly Dictionary<BlockContainer, (ParagraphBlock[] Blocks, TextParagraph[] Contents, IReadOnlyDictionary<string, ListPlacement?> Lists)> _lists = [];

    public void Clear()
    {
        entries.Clear();
        _lists.Clear();
    }

    public IReadOnlyDictionary<string, ListPlacement?> Lists(TextDocument document, BlockContainer root)
    {
        var blocks = tree.Paragraphs(root).ToList();
        if (_lists.TryGetValue(root, out var cached) && Unchanged(document, cached, blocks))
        {
            for (var i = 0; i < blocks.Count; i++) cached.Contents[i] = blocks[i].Content;
            return cached.Lists;
        }
        var lists = Place(document, blocks);
        _lists[root] = ([.. blocks], [.. blocks.Select(block => block.Content)], lists);
        return lists;
    }

    private bool Unchanged(TextDocument document, (ParagraphBlock[] Blocks, TextParagraph[] Contents, IReadOnlyDictionary<string, ListPlacement?> Lists) cached, List<ParagraphBlock> current)
    {
        if (cached.Blocks.Length != current.Count) return false;
        for (var i = 0; i < current.Count; i++)
        {
            var block = current[i];
            if (!ReferenceEquals(cached.Blocks[i], block)) return false;
            if (ReferenceEquals(cached.Contents[i], block.Content)) continue;
            if (cached.Lists.GetValueOrDefault(block.Id) is not null || ListRelevant(cached.Contents[i])) return false;
            var effective = entries.Effective(document.Styles, block, block.Content);
            if (ListRelevant(effective) || ListRelevant(block.Content)) return false;
        }
        return true;
    }

    private bool ListRelevant(TextParagraph paragraph) => paragraph.ListStyleName is not null || paragraph.IsListItem || paragraph.ListRestart is not null || paragraph.ListId is not null || paragraph.OutlineLevel is not null;

    private IReadOnlyDictionary<string, ListPlacement?> Place(TextDocument document, List<ParagraphBlock> blocks)
    {
        var effective = blocks.Select(block => entries.Effective(document.Styles, block, block.Content)).ToList();
        var lists = labeling.Place(document.Styles, effective);
        var result = new Dictionary<string, ListPlacement?>(StringComparer.Ordinal);
        for (var i = 0; i < blocks.Count; i++) result[blocks[i].Id] = lists[i];
        return result;
    }

    public NoteNumbers Number(TextDocument document, IReadOnlyDictionary<string, int>? onPage = null)
    {
        if (document.Notes.All.Count == 0) return NoteNumbers.None;
        var citations = new Dictionary<string, string>(StringComparer.Ordinal);
        var endnotes = new List<string>();
        var footnoteCount = 0;
        var endnoteCount = 0;
        var footnoteSettings = document.Notes.FootnoteSettings;
        var endnoteSettings = document.Notes.EndnoteSettings;
        foreach (var paragraph in tree.Paragraphs(document.Body))
        {
            if ((footnoteSettings.Restart == NoteNumbering.Chapter || endnoteSettings.Restart == NoteNumbering.Chapter)
                && entries.Effective(document.Styles, paragraph, paragraph.Content).OutlineLevel == 1)
            {
                if (footnoteSettings.Restart == NoteNumbering.Chapter) footnoteCount = 0;
                if (endnoteSettings.Restart == NoteNumbering.Chapter) endnoteCount = 0;
            }
            foreach (var mark in paragraph.Marks.OfType<NoteMark>().OrderBy(mark => mark.Offset))
            {
                var note = document.Notes.Find(mark.NoteId);
                if (note is null || citations.ContainsKey(note.Id)) continue;
                var footnote = note.Kind == NoteKind.Footnote;
                var settings = footnote ? document.Notes.FootnoteSettings : document.Notes.EndnoteSettings;
                var value = settings.StartAt + (footnote ? footnoteCount++ : endnoteCount++);
                if (footnote && settings.Restart == NoteNumbering.Page && onPage is not null && onPage.TryGetValue(note.Id, out var position)) value = settings.StartAt + position;
                citations[note.Id] = note.Citation ?? Render(settings.FormatName, value);
                if (!footnote) endnotes.Add(note.Id);
            }
        }
        return new NoteNumbers(citations, endnotes);
    }

    public IReadOnlyList<FlowItem> Notes(string noteId, FlowContext context)
    {
        var note = context.Document.Notes.Find(noteId);
        if (note is null) return [];
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        var first = tree.Paragraphs(note.Content).FirstOrDefault();
        if (first is not null && context.Notes.Citations.TryGetValue(noteId, out var citation)) labels[first.Id] = citation;
        var settings = note.Kind == NoteKind.Footnote ? context.Document.Notes.FootnoteSettings : context.Document.Notes.EndnoteSettings;
        return Build(note.Content, context with
        {
            Story = note.Kind == NoteKind.Footnote ? StoryKind.Footnote : StoryKind.Endnote,
            StoryId = noteId,
            Sections = false,
            Labels = labels,
            DefaultStyle = settings.ParagraphStyleName,
        });
    }

    public ParagraphEntry PerPage(ParagraphEntry entry, string number, FlowContext context) =>
        entry.Story == StoryKind.Body && fields.HasNumber(entry.Block.Content) ? Paragraph(entry.Block, context, number) : entry;

    public IReadOnlyList<FlowItem> Build(BlockContainer container, FlowContext context)
    {
        var visitor = new Visitor(this, context);
        return [.. container.Blocks.SelectMany(block => block.Accept(visitor))];
    }

    private string Render(string format, int value)
    {
        var kind = format switch
        {
            "i" => NumberFormat.LowerRoman,
            "I" => NumberFormat.UpperRoman,
            "a" => NumberFormat.LowerLetter,
            "A" => NumberFormat.UpperLetter,
            _ => NumberFormat.Arabic,
        };
        return _formatters.TryGetValue(kind, out var formatter) ? formatter.Render(value) : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private ParagraphEntry Paragraph(ParagraphBlock block, FlowContext context, string? number = null)
    {
        var document = context.Document;
        var marked = markup.Mark(document, block, block.Content);
        var source = marked?.Content ?? block.Content;
        var content = placeholders.Present(block, context.Fields?.Invoke(source) ?? source);
        if (context.Story == StoryKind.Body) content = fields.Resolve(content, number, context.PageCount);
        if (content.StyleName is null && context.DefaultStyle is { } style) content = _styled.GetValue(content, source => source with { StyleName = style });
        var list = context.Lists.GetValueOrDefault(block.Id);
        if (context.Labels.TryGetValue(block.Id, out var label))
        {
            var effective = entries.Effective(document.Styles, block, content);
            list = new ListPlacement(label, 0, effective.Indent ?? 0) { FollowedByTab = false };
        }
        if (block.Marks.Count == 0 && document.Objects.All.Count == 0 && marked is null)
        {
            return entries.Entry(document, new EntryRequest(block, content, list)
            {
                Story = context.Story,
                StoryId = context.StoryId,
                Variant = number ?? string.Empty,
            });
        }
        var inlines = new List<InlineInsertion>();
        var parts = new List<object>();
        if (marked is not null)
        {
            inlines.AddRange(marked.Deletions);
            parts.Add(marked.Key);
        }
        foreach (var mark in block.Marks)
        {
            if (mark is NoteMark note && context.Notes.Citations.TryGetValue(note.NoteId, out var citation))
            {
                inlines.Add(new InlineInsertion(mark.Offset, InlineKind.Citation, note.NoteId, citation, 0, 0));
                var found = document.Notes.Find(note.NoteId);
                if (found is not null) parts.AddRange(tree.Paragraphs(found.Content).Select(paragraph => (object)paragraph.Content));
            }
            else if (mark is ObjectMark objectMark && document.Objects.Find(objectMark.ObjectId) is { Anchor: AsCharacterAnchor } inline)
            {
                inlines.Add(new InlineInsertion(mark.Offset, InlineKind.Object, inline.Id, string.Empty, inline.Size.Width.ToPoints(), inline.Size.Height.ToPoints()));
            }
        }
        var objects = new List<string>();
        foreach (var item in document.Objects.AnchoredTo(block.Id))
        {
            parts.Add(Snapshot(item));
            if (item.Anchor is AsCharacterAnchor)
            {
                if (inlines.All(inline => inline.Id != item.Id)) inlines.Add(new InlineInsertion(0, InlineKind.Object, item.Id, string.Empty, item.Size.Width.ToPoints(), item.Size.Height.ToPoints()));
            }
            else
            {
                objects.Add(item.Id);
            }
        }
        return entries.Entry(document, new EntryRequest(block, content, list)
        {
            Story = context.Story,
            StoryId = context.StoryId,
            Inlines = inlines,
            Objects = objects,
            Parts = parts,
            Variant = number ?? string.Empty,
        });
    }

    private ObjectSnapshot Snapshot(AnchoredObject item) => new(
        item.Id,
        item.Anchor.GetType().Name + ":" + item.Anchor.Accept(_anchors),
        item.Size.Width.ToPoints(),
        item.Size.Height.ToPoints(),
        item.Placement.X.ToPoints(),
        item.Placement.Y.ToPoints(),
        item.Placement.Horizontal,
        item.Placement.Vertical,
        item.Wrap,
        item.InBackground,
        item.ZOrder)
    {
        Spacing = item.WrapSpacing,
        Outline = $"{item.WrapContour}:{item.Contour?.GetHashCode()}:{item.WrapFirstParagraph}:{item.FlipHorizontal}:{item.FlipVertical}:{item.RotationDegrees}:{(item.Content as TextBoxContent)?.NextFrameName}",
        Text = item.Content.Story is { } story ? [.. tree.Paragraphs(story).Select(paragraph => paragraph.Content)] : [],
    };

    private TableItem Table(TableBlock table, FlowContext context)
    {
        var properties = appearance.Style(context.Document.Styles, table);
        var style = new TableStyleValues(
            properties.CellPadding ?? options.TableCellPadding,
            properties.VerticalCellPadding ?? properties.CellPadding ?? options.TableCellPadding,
            properties.BorderWidth ?? 0,
            properties.BorderColor,
            properties.Background,
            properties.HeaderBackground,
            properties.BandBackground);
        var cellContext = context with { Sections = false, Story = context.Story == StoryKind.Body ? StoryKind.Cell : context.Story };
        var rows = table.Rows.Select((row, r) => new TableRowItem(row, [.. row.Cells.Select((cell, column) => Cell(table, properties, cell, r, column, cellContext))])).ToList();
        return new TableItem(table, [.. table.Columns.Select(column => column.ToPoints())], rows, style) { Story = context.Story };
    }

    private TableCellItem Cell(TableBlock table, TableProperties properties, TableCell cell, int row, int column, FlowContext context)
    {
        if (cell.Covered) return new TableCellItem(cell, column, Build(cell.Content, context));
        var look = appearance.Cell(table, properties, row, column);
        var styled = look.Ink is null && !look.Bold ? context : context with { Fields = Header(context.Fields, look) };
        return new TableCellItem(cell, column, Build(cell.Content, styled)) { Appearance = look };
    }

    private Func<TextParagraph, TextParagraph> Header(Func<TextParagraph, TextParagraph>? inner, CellAppearance look) => paragraph =>
    {
        var source = inner?.Invoke(paragraph) ?? paragraph;
        return _headers.GetValue(source, content =>
        {
            var runs = content.Runs.Count == 0 ? [new TextRun(content.Text)] : content.Runs;
            return content.WithRuns([.. runs.Select(run => run with { Bold = run.Bold || look.Bold, Color = run.Color ?? look.Ink })]);
        });
    };

    private bool Hides(ParagraphBlock paragraph) =>
        !hidden.Show && paragraph.Content.Runs.Any(run => run.Field == TextFieldKind.HiddenParagraph && run.Hidden == true);

    private sealed class Visitor(FlowBuilder owner, FlowContext context) : IBlockVisitor<IEnumerable<FlowItem>>
    {
        public IEnumerable<FlowItem> VisitParagraph(ParagraphBlock paragraph) =>
            owner.Hides(paragraph) ? [] : [owner.Paragraph(paragraph, context)];

        public IEnumerable<FlowItem> VisitTable(TableBlock table) => [owner.Table(table, context)];

        public IEnumerable<FlowItem> VisitSection(SectionBlock section)
        {
            if (section.Hidden) return [];
            var content = section.Content.Blocks.SelectMany(block => block.Accept(this));
            if (!context.Sections) return [.. content];
            var columns = new ColumnLayout(Math.Clamp(section.Columns.Count, 1, SectionColumns.LargestCount), section.Columns.Gap.ToPoints(), section.Columns.Balanced, section.Columns.Separator);
            return [new SectionStartItem(section, columns), .. content, new SectionEndItem(section)];
        }
    }
}
