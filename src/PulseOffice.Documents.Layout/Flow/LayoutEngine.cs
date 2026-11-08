using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents.Layout.Flow;

public interface IDocumentLayoutEngine
{
    DocumentLayout Layout(TextDocument document);

    DocumentLayout Relayout(TextDocument document, DocumentLayout previous);

    void Invalidate();
}

public sealed class DocumentLayoutEngine(
    IFlowBuilder builder,
    IStoryComposer stories,
    IPageGeometries geometries,
    IPageFrames frames,
    IPaginator paginator,
    IPageCountFields counts,
    IPageNumberText numbers,
    IStyleResolver resolver,
    IFrameChains chains,
    PulseOffice.Drawing.Shapes.IShapeTextInk ink) : IDocumentLayoutEngine
{
    private const int CountPasses = 4;

    private readonly Dictionary<string, PageGeometry> _geometries = new(StringComparer.Ordinal);
    private (StyleSheet? Sheet, long Version) _styles;

    public void Invalidate()
    {
        builder.Clear();
        frames.Clear();
        _geometries.Clear();
    }

    public DocumentLayout Layout(TextDocument document) => Layout(document, 1);

    private DocumentLayout Layout(TextDocument document, int estimate)
    {
        Synchronize(document.Styles);
        var usesCount = counts.Present(document);
        var perPage = PageNotes(document);
        var count = estimate;
        IReadOnlyDictionary<string, int>? onPage = null;
        List<FlowItem> flow;
        IReadOnlyList<PageBox> pages;
        var pass = 0;
        while (true)
        {
            flow = Flow(document, Text(document, count), onPage, out var notes, out var body);
            pages = paginator.Paginate(Request(document, flow, notes, body, [], new PageStart(0, 0, document.InitialPageStyle), null, count));
            var changed = false;
            if (usesCount && pages.Count != count)
            {
                count = pages.Count;
                changed = true;
            }
            if (perPage)
            {
                var positions = NotePositions(pages);
                if (onPage is null || positions.Count != onPage.Count || positions.Any(pair => !onPage.TryGetValue(pair.Key, out var value) || value != pair.Value))
                {
                    onPage = positions;
                    changed = true;
                }
            }
            if (!changed || ++pass >= CountPasses) break;
        }
        return Finish(document, pages, [.. flow.Select(item => item.Key)], frames.Signature(document));
    }

    private bool PageNotes(TextDocument document) => document.Notes.FootnoteSettings.Restart == NoteNumbering.Page && document.Notes.All.Any(note => note.Kind == NoteKind.Footnote);

    private Dictionary<string, int> NotePositions(IReadOnlyList<PageBox> pages)
    {
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            var index = 0;
            foreach (var id in page.Lines.SelectMany(line => line.Line.Notes))
            {
                if (positions.TryAdd(id, index)) index++;
            }
        }
        return positions;
    }

    private string Text(int count) => count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private string Text(TextDocument document, int count) => numbers.Render(numbers.FormatOf(document.Styles, document.InitialPageStyle), count);

    private double Inset(TextDocument document, AnchoredObject item)
    {
        if (item.Content is EmbeddedContent { Value: PulseOffice.Drawing.Shapes.ShapeObject shape }) return shape.Style.Text.Inset.ToPoints();
        var frame = resolver.ResolveFrameStyle(document.Styles, item.StyleName);
        return (frame.Padding ?? 0) + (frame.BorderWidth ?? 0);
    }

    public DocumentLayout Relayout(TextDocument document, DocumentLayout previous)
    {
        var estimate = Math.Max(1, previous.Pages.Count);
        if (!Synchronize(document.Styles) || PageNotes(document)) return Layout(document, estimate);
        var signature = frames.Signature(document);
        if (previous.Signature is null || !previous.Signature.Matches(signature))
        {
            frames.Clear();
            _geometries.Clear();
            return Layout(document, estimate);
        }
        var flow = Flow(document, Text(document, estimate), null, out var notes, out var body);
        var keys = flow.Select(entry => entry.Key).ToList();
        var old = previous.Flow;
        var prefix = 0;
        while (prefix < keys.Count && prefix < old.Count && keys[prefix].Matches(old[prefix])) prefix++;
        if (prefix == keys.Count && prefix == old.Count) return previous;
        var suffix = 0;
        while (suffix < keys.Count - prefix && suffix < old.Count - prefix && keys[^(suffix + 1)].Matches(old[^(suffix + 1)])) suffix++;
        var boundary = SectionStart(flow, prefix);
        var resumePage = 0;
        for (var page = 0; page < previous.Pages.Count; page++)
        {
            if (previous.Pages[page].Start.FlowIndex < boundary) resumePage = page;
        }
        resumePage = Math.Max(0, resumePage - 1);
        var resume = previous.Pages.Count == 0 ? new PageStart(0, 0, document.InitialPageStyle) : previous.Pages[resumePage].Start;
        var stable = !document.Styles.Page.All.Any(style => style.EvenPagesDifferent) && !document.Objects.All.Any(item => item.Anchor is PageAnchor);
        var probe = new SuffixProbe(previous, keys.Count - suffix, keys.Count - old.Count, stable);
        var pages = paginator.Paginate(Request(document, flow, notes, body, [.. previous.Pages.Take(resumePage)], resume, probe, estimate));
        if (pages.Count != estimate && counts.Present(document)) return Layout(document, pages.Count);
        return Finish(document, pages, keys, signature);
    }

    private int SectionStart(IReadOnlyList<FlowItem> flow, int index)
    {
        var depth = 0;
        var start = index;
        for (var i = Math.Min(index, flow.Count) - 1; i >= 0; i--)
        {
            if (flow[i] is SectionEndItem) depth++;
            else if (flow[i] is SectionStartItem)
            {
                if (depth == 0) start = i;
                else depth--;
            }
        }
        return start;
    }

    private DocumentLayout Finish(TextDocument document, IReadOnlyList<PageBox> pages, IReadOnlyList<FlowKey> keys, FlowKey signature)
    {
        var decorated = new List<PageBox>(pages.Count);
        for (var i = 0; i < pages.Count; i++)
        {
            var first = i == 0 || pages[i - 1].StyleName != pages[i].StyleName;
            var stories = frames.Stories(document, pages[i], pages.Count, first);
            decorated.Add(stories == PageStories.None && pages[i].Stories == PageStories.None ? pages[i] : pages[i].WithStories(stories));
        }
        return new DocumentLayout(decorated, keys) { Signature = signature };
    }

    private PaginationRequest Request(TextDocument document, IReadOnlyList<FlowItem> flow, FlowContext notes, FlowContext body, IReadOnlyList<PageBox> prefix, PageStart resume, IConvergenceProbe? probe, int count)
    {
        var noteStories = new Dictionary<(string, double), ComposedStory?>();
        var frameStories = new Dictionary<string, ComposedStory?>(StringComparer.Ordinal);
        var memberStories = new Dictionary<string, IReadOnlyList<MemberFrame>>(StringComparer.Ordinal);
        var pageObjects = document.Objects.All.Where(item => item.Anchor is PageAnchor).ToList();
        return new PaginationRequest(flow, (name, side) => frames.Geometry(document, Geometry(document.Styles, name), side), document.InitialPageStyle, prefix, resume, probe)
        {
            Note = (id, width) =>
            {
                if (noteStories.TryGetValue((id, width), out var story)) return story;
                var note = document.Notes.Find(id);
                story = note is null || note.Kind != NoteKind.Footnote ? null : stories.Compose(builder.Notes(id, notes), width);
                noteStories[(id, width)] = story;
                return story;
            },
            Object = document.Objects.Find,
            PageObjects = number => [.. pageObjects.Where(item => ((PageAnchor)item.Anchor).PageNumber == number)],
            PerPage = (entry, number) => builder.PerPage(entry, number, body),
            Frame = id =>
            {
                if (frameStories.TryGetValue(id, out var story)) return story;
                var found = document.Objects.Find(id);
                if (found is { Content: TextBoxContent } && chains.Chained(document, found))
                {
                    Chain(document, found, body, frameStories);
                    return frameStories.GetValueOrDefault(id);
                }
                story = found is { Content.Story: { } content } item
                    ? stories.Compose(builder.Build(content, Inked(body with { Story = StoryKind.Frame, StoryId = id, Sections = false, Lists = builder.Lists(document, content) }, item.Content)), Math.Max(1, item.Size.Width.ToPoints() - 2 * Inset(document, item)))
                    : null;
                frameStories[id] = story;
                return story;
            },
            PageCount = Text(count),
            PageNumber = (style, number) => numbers.Render(numbers.FormatOf(document.Styles, style), number),
            FrameInset = id => document.Objects.Find(id) is { } item ? Inset(document, item) : 0,
            FrameAlignment = id => document.Objects.Find(id) is { Content: EmbeddedContent { Value: PulseOffice.Drawing.Shapes.ShapeObject shape } } ? Alignment(shape) : 0,
            MemberFrames = id =>
            {
                if (memberStories.TryGetValue(id, out var members)) return members;
                members = document.Objects.Find(id) is { Content: EmbeddedContent { Value: PulseOffice.Drawing.Shapes.ShapeGroupObject group } content } item && content.MemberStories.Count > 0
                    ? Members(group, content, 0, 0, 1, 1, item.Size.Width.ToPoints(), body with { Story = StoryKind.Frame, StoryId = id, Sections = false })
                    : [];
                memberStories[id] = members;
                return members;
            },
        };
    }

    private FlowContext Inked(FlowContext context, AnchoredContent content) =>
        content is EmbeddedContent { Value: PulseOffice.Drawing.Shapes.ShapeObject shape } ? Inked(context, shape.Style) : context;

    private FlowContext Inked(FlowContext context, PulseOffice.Drawing.Shapes.ShapeStyle style)
    {
        var automatic = style with { Text = style.Text with { AutomaticColor = true } };
        var resolved = ink.Resolve(automatic, PulseOffice.Drawing.Shapes.ShapeTextInk.PaperHex);
        if (!string.Equals(resolved, LightInk, StringComparison.OrdinalIgnoreCase)) return context;
        var inner = context.Fields;
        return context with
        {
            Fields = paragraph =>
            {
                var source = inner?.Invoke(paragraph) ?? paragraph;
                var runs = source.Runs.Count == 0 ? [new PulseOffice.Text.Rich.TextRun(source.Text)] : source.Runs;
                return source.WithRuns([.. runs.Select(run => run is { Color: null, ColorRole: null, StyleName: null } ? run with { Color = LightArgb } : run)]);
            },
        };
    }

    private const string LightInk = "#FFFFFF";

    private const uint LightArgb = 0xFFFFFFFF;

    private double Alignment(PulseOffice.Drawing.Shapes.ShapeObject shape) => shape.Style.Text.Vertical switch
    {
        PulseOffice.Drawing.Shapes.TextVerticalPlacement.Top => 0,
        PulseOffice.Drawing.Shapes.TextVerticalPlacement.Middle => 0.5,
        _ => 1,
    };

    private List<MemberFrame> Members(PulseOffice.Drawing.Shapes.ShapeGroupObject group, EmbeddedContent content, double x, double y, double width, double height, double points, FlowContext context)
    {
        var frames = new List<MemberFrame>();
        foreach (var child in group.Children)
        {
            var (left, top, across, down) = (x + child.X * width, y + child.Y * height, child.Width * width, child.Height * height);
            if (child.Content is PulseOffice.Drawing.Shapes.ShapeGroupObject nested)
                frames.AddRange(Members(nested, content, left, top, across, down, points, context));
            else if (child.Content is PulseOffice.Drawing.Shapes.ShapeObject shape && content.MemberStories.TryGetValue(shape.Id, out var story))
            {
                var inset = shape.Style.Text.Inset.ToPoints();
                var composed = stories.Compose(builder.Build(story, Inked(context with { Lists = builder.Lists(context.Document, story) }, shape.Style)), Math.Max(1, across * points - 2 * inset));
                frames.Add(new MemberFrame(shape.Id, left, top, across, down, inset, Alignment(shape), composed));
            }
        }
        return frames;
    }

    private void Chain(TextDocument document, AnchoredObject member, FlowContext body, Dictionary<string, ComposedStory?> frameStories)
    {
        var chain = chains.Chain(document, member);
        var head = chain[0];
        var content = ((TextBoxContent)head.Content).Content;
        var items = builder.Build(content, body with { Story = StoryKind.Frame, StoryId = head.Id, Sections = false, Lists = builder.Lists(document, content) });
        var cursor = StoryCursor.Start;
        for (var index = 0; index < chain.Count; index++)
        {
            var frame = chain[index];
            var inset = Inset(document, frame);
            var width = Math.Max(1, frame.Size.Width.ToPoints() - 2 * inset);
            var last = index == chain.Count - 1;
            var limit = last && frame.Content is TextBoxContent { AutoGrowHeight: true } ? double.PositiveInfinity : Math.Max(1, frame.Size.Height.ToPoints() - 2 * inset);
            frameStories[frame.Id] = stories.ComposeFrom(items, width, cursor, limit, out cursor);
        }
    }

    private bool Synchronize(StyleSheet sheet)
    {
        if (ReferenceEquals(_styles.Sheet, sheet) && _styles.Version == sheet.Version) return true;
        Invalidate();
        _styles = (sheet, sheet.Version);
        return false;
    }

    private PageGeometry Geometry(StyleSheet sheet, string name)
    {
        if (_geometries.TryGetValue(name, out var geometry)) return geometry;
        geometry = geometries.Of(sheet, name);
        _geometries[name] = geometry;
        return geometry;
    }

    private List<FlowItem> Flow(TextDocument document, string count, IReadOnlyDictionary<string, int>? onPage, out FlowContext notes, out FlowContext body)
    {
        var numbers = builder.Number(document, onPage);
        var context = new FlowContext(document, StoryKind.Body, null, true, builder.Lists(document, document.Body), numbers) { PageCount = count };
        var flow = builder.Build(document.Body, context).ToList();
        body = context;
        notes = context with { Lists = new Dictionary<string, ListPlacement?>() };
        if (numbers.Endnotes.Count > 0)
        {
            if (!document.Notes.ContinuousEndnotes) flow.Add(new EndnotesItem(document.Styles.Page.Find(DefaultStyleNames.Endnote)?.Name));
            foreach (var id in numbers.Endnotes) flow.AddRange(builder.Notes(id, notes));
        }
        return flow;
    }

    private sealed class SuffixProbe : IConvergenceProbe
    {
        private readonly DocumentLayout _previous;
        private readonly int _unchangedFrom;
        private readonly int _shift;
        private readonly Dictionary<PageStart, int> _starts = [];

        private readonly bool _stable;

        public SuffixProbe(DocumentLayout previous, int unchangedFrom, int shift, bool stable)
        {
            _stable = stable;
            _previous = previous;
            _unchangedFrom = unchangedFrom;
            _shift = shift;
            for (var i = 0; i < previous.Pages.Count; i++) _starts.TryAdd(previous.Pages[i].Start, i);
        }

        public IReadOnlyList<PageBox>? Remainder(int pageIndex, PageStart start)
        {
            if (start.FlowIndex < _unchangedFrom) return null;
            if (!_starts.TryGetValue(start with { FlowIndex = start.FlowIndex - _shift }, out var oldIndex)) return null;
            if (!_stable && oldIndex != pageIndex) return null;
            var pages = new List<PageBox>();
            for (var i = oldIndex; i < _previous.Pages.Count; i++)
            {
                var page = _previous.Pages[i];
                pages.Add(page.Renumbered(pageIndex + i - oldIndex, page.Start with { FlowIndex = page.Start.FlowIndex + _shift }));
            }
            return pages;
        }
    }
}
