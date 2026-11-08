using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Layout.Flow;

public sealed record PageFlowOptions
{
    public double TableCellPadding { get; init; } = 0.097 * 72 / 2.54;

    public double SeparatorBefore { get; init; } = 0.101 * 72 / 2.54;

    public double SeparatorAfter { get; init; } = 0.101 * 72 / 2.54;

    public double SeparatorWidth { get; init; } = 0.018 * 72 / 2.54;

    public double SeparatorRelativeWidth { get; init; } = 0.25;

    public double MinimumWrapWidth { get; init; } = 0.5 * 72 / 2.54;

    public int BalanceIterations { get; init; } = 40;

    public double BodyReserve { get; init; } = 0.2;
}

public sealed record PageDecor(uint? Background, BoxBorders? Borders, LayoutRect Frame, PageWatermark? Watermark)
{
    public static readonly PageDecor None = new(null, null, default, null);

    public bool IsEmpty => Background is null && Borders is not { HasLines: true } && Watermark is null;
}

public sealed record PageGeometry(string StyleName, double Width, double Height, LayoutRect Content, string NextStyleName)
{
    public PageDecor Decor { get; init; } = PageDecor.None;

    public LayoutRect Body { get; init; } = Content;

    public ColumnLayout Columns { get; init; } = ColumnLayout.Single;

    public double HeaderHeight { get; init; }

    public double FooterHeight { get; init; }
}

public interface IPageGeometries
{
    PageGeometry Of(StyleSheet sheet, string name);
}

public sealed class PageGeometries : IPageGeometries
{
    public PageGeometry Of(StyleSheet sheet, string name)
    {
        var style = sheet.Page.Find(name) ?? new PageStyle(name);
        var margins = style.Margins;
        var width = style.Width.ToPoints();
        var height = style.Height.ToPoints();
        var frame = new LayoutRect(
            margins.Left.ToPoints(),
            margins.Top.ToPoints(),
            Math.Max(1, width - margins.Left.ToPoints() - margins.Right.ToPoints()),
            Math.Max(1, height - margins.Top.ToPoints() - margins.Bottom.ToPoints()));
        var borders = style.Borders;
        var content = borders is null ? frame : new LayoutRect(
            frame.X + borders.Inset(BorderSide.Left),
            frame.Y + borders.Inset(BorderSide.Top),
            Math.Max(1, frame.Width - borders.Inset(BorderSide.Left) - borders.Inset(BorderSide.Right)),
            Math.Max(1, frame.Height - borders.Inset(BorderSide.Top) - borders.Inset(BorderSide.Bottom)));
        var columns = style.Columns;
        return new PageGeometry(name, width, height, content, style.NextStyleName ?? name)
        {
            Columns = new ColumnLayout(Math.Clamp(columns.Count, 1, SectionColumns.LargestCount), columns.Gap.ToPoints(), false, columns.Separator),
            Decor = new PageDecor(style.Background, borders, frame, style.Watermark),
        };
    }
}

public sealed record PageSide(int Number, bool First)
{
    public string Count { get; init; } = "00";

    public bool Even => Number % 2 == 0;
}

public interface IPageFrames
{
    PageGeometry Geometry(TextDocument document, PageGeometry basis, PageSide side);

    PageStories Stories(TextDocument document, PageBox page, int pageCount, bool first);

    FlowKey Signature(TextDocument document);

    void Clear();
}

public sealed class PageFrames(IFlowBuilder builder, IStoryComposer stories, IBlockTree tree, IPageFieldText fields, IPageNumberText numbers, IContinuousLayout continuous) : IPageFrames
{
    private readonly Dictionary<(string Style, bool First, bool Even, string Number, string Count), PageGeometry> _geometries = [];
    private readonly Dictionary<(BlockContainer Container, string Number, string Count, double Width), (FlowKey Key, ComposedStory Story)> _stories = [];

    public void Clear()
    {
        _geometries.Clear();
        _stories.Clear();
        builder.Clear();
    }

    public FlowKey Signature(TextDocument document)
    {
        var parts = new List<object>();
        foreach (var style in document.Styles.Page.All)
        {
            parts.Add(style.Name);
            foreach (var content in new[] { style.Header, style.Footer })
            {
                parts.Add($"{content.Enabled}:{content.Spacing}:{content.MinimumHeight}:{style.FirstPageDifferent}:{style.EvenPagesDifferent}");
                foreach (var container in Containers(content)) parts.AddRange(tree.Paragraphs(container).Select(paragraph => (object)paragraph.Content));
            }
        }
        HashSet<string>? body = null;
        var linked = document.Objects.All.Select(item => (item.Content as TextBoxContent)?.NextFrameName).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var item in document.Objects.All)
        {
            if (item.Content is not TextBoxContent { NextFrameName: not null } && item.Anchor.Accept(new AnchorParagraph()) is { } anchor && (item.Name is null || !linked.Contains(item.Name)))
            {
                body ??= [.. tree.Paragraphs(document.Body).Select(paragraph => paragraph.Id)];
                if (body.Contains(anchor)) continue;
            }
            parts.Add($"{item.Id}:{item.Anchor}:{item.Placement}:{item.Size}:{item.RotationDegrees}:{item.Wrap}:{item.InBackground}:{item.WrapContour}:{item.Contour?.GetHashCode()}:{item.WrapFirstParagraph}:{item.FlipHorizontal}:{item.FlipVertical}:{(item.Content as TextBoxContent)?.NextFrameName}");
            if (item.Content is TextBoxContent { NextFrameName: not null } chained) parts.AddRange(tree.Paragraphs(chained.Content).Select(paragraph => (object)paragraph.Content));
        }
        return new FlowKey("page-frames", parts, string.Empty);
    }

    public PageGeometry Geometry(TextDocument document, PageGeometry basis, PageSide side)
    {
        var style = document.Styles.Page.Find(basis.StyleName);
        if (continuous.Page is not null || style is null || !style.Header.Enabled && !style.Footer.Enabled) return basis;
        var rendered = numbers.Render(style.NumberFormat, side.Number);
        var number = style.NumberFormat == NumberFormat.Arabic ? new string('0', rendered.Length) : rendered;
        var key = (basis.StyleName, side.First && style.FirstPageDifferent, side.Even && style.EvenPagesDifferent, number, side.Count);
        if (_geometries.TryGetValue(key, out var cached)) return cached;
        var count = style.NumberFormat != NumberFormat.Arabic && int.TryParse(side.Count, System.Globalization.CultureInfo.InvariantCulture, out var total) ? numbers.Render(style.NumberFormat, total) : side.Count;
        var header = Height(document, style, style.Header, side, number, count, basis.Content.Width);
        var footer = Height(document, style, style.Footer, side, number, count, basis.Content.Width);
        var content = basis.Content;
        var body = new LayoutRect(content.X, content.Y + header, content.Width, Math.Max(1, content.Height - header - footer));
        cached = basis with { Body = body, HeaderHeight = header, FooterHeight = footer };
        _geometries[key] = cached;
        return cached;
    }

    public PageStories Stories(TextDocument document, PageBox page, int pageCount, bool first)
    {
        var style = document.Styles.Page.Find(page.StyleName);
        if (continuous.Page is not null || style is null || !style.Header.Enabled && !style.Footer.Enabled) return PageStories.None;
        var side = new PageSide(page.Number, first);
        var number = numbers.Render(style.NumberFormat, page.Number);
        var count = numbers.Render(style.NumberFormat, pageCount);
        var content = page.Content;
        AreaBox? header = null;
        AreaBox? footer = null;
        if (style.Header.Enabled)
        {
            var story = Story(document, Choose(style, style.Header, side), number, count, content.Width);
            var height = Math.Max(style.Header.MinimumHeight.ToPoints(), story.Height);
            header = new AreaBox(AreaKind.Header, new LayoutRect(content.X, content.Y, content.Width, height),
                stories.Emit(story, 0, story.Atoms.Count, content.X, content.Y + (story.Atoms.Count > 0 ? story.Atoms[0].Top : 0), content.Width, StoryKind.Header, page.StyleName));
        }
        if (style.Footer.Enabled)
        {
            var story = Story(document, Choose(style, style.Footer, side), number, count, content.Width);
            var height = Math.Max(style.Footer.MinimumHeight.ToPoints(), story.Height);
            var top = content.Bottom - height;
            footer = new AreaBox(AreaKind.Footer, new LayoutRect(content.X, top, content.Width, height),
                stories.Emit(story, 0, story.Atoms.Count, content.X, top + (story.Atoms.Count > 0 ? story.Atoms[0].Top : 0), content.Width, StoryKind.Footer, page.StyleName));
        }
        return new PageStories(header, footer);
    }

    private IEnumerable<BlockContainer> Containers(HeaderFooterContent content)
    {
        yield return content.Default;
        if (content.First is not null) yield return content.First;
        if (content.Even is not null) yield return content.Even;
    }

    private BlockContainer Choose(PageStyle style, HeaderFooterContent content, PageSide side)
    {
        if (side.First && style.FirstPageDifferent) return content.First ?? content.Default;
        if (side.Even && style.EvenPagesDifferent) return content.Even ?? content.Default;
        return content.Default;
    }

    private double Height(TextDocument document, PageStyle style, HeaderFooterContent content, PageSide side, string number, string count, double width)
    {
        if (!content.Enabled) return 0;
        var story = Story(document, Choose(style, content, side), number, count, width);
        return Math.Max(content.MinimumHeight.ToPoints(), story.Height) + content.Spacing.ToPoints();
    }

    private ComposedStory Story(TextDocument document, BlockContainer container, string number, string count, double width)
    {
        var parts = tree.Paragraphs(container).Select(paragraph => (object)paragraph.Content).ToList();
        var key = new FlowKey("story", parts, string.Empty);
        if (_stories.TryGetValue((container, number, count, width), out var cached) && cached.Key.Matches(key)) return cached.Story;
        var context = new FlowContext(document, StoryKind.Header, null, false, builder.Lists(document, container), NoteNumbers.None)
        {
            Fields = paragraph => fields.Resolve(paragraph, number, count),
        };
        var story = stories.Compose(builder.Build(container, context), width);
        _stories[(container, number, count, width)] = (key, story);
        return story;
    }
}

public sealed record ContinuousPage(double? Width);

public interface IContinuousLayout
{
    ContinuousPage? Page { get; set; }
}

public sealed class ContinuousLayout : IContinuousLayout
{
    public ContinuousPage? Page { get; set; }
}

public interface IPrintLayouts
{
    DocumentLayout Layout(TextDocument document);

    DocumentLayout Of(TextDocument document, DocumentLayout display);
}

public sealed class PrintLayouts(Func<IDocumentLayoutEngine> engines, IContinuousLayout continuous) : IPrintLayouts
{
    public DocumentLayout Layout(TextDocument document)
    {
        var view = continuous.Page;
        continuous.Page = null;
        try
        {
            return engines().Layout(document);
        }
        finally
        {
            continuous.Page = view;
        }
    }

    public DocumentLayout Of(TextDocument document, DocumentLayout display) =>
        continuous.Page is null ? display : Layout(document);
}

public sealed class ViewPageGeometries(PageGeometries pages, IContinuousLayout continuous) : IPageGeometries
{
    private const double Edge = 0.5 * 72 / 2.54;
    private const double Endless = 1_000_000;

    public PageGeometry Of(StyleSheet sheet, string name)
    {
        var page = pages.Of(sheet, name);
        if (continuous.Page is not { } view) return page;
        var width = view.Width ?? page.Width;
        var body = view.Width is null
            ? new LayoutRect(page.Content.X, Edge, page.Content.Width, Endless - Edge * 2)
            : new LayoutRect(Edge, Edge, Math.Max(36, width - Edge * 2), Endless - Edge * 2);
        return page with { Width = width, Height = Endless, Content = body, Body = body, Columns = ColumnLayout.Single, Decor = PageDecor.None, HeaderHeight = 0, FooterHeight = 0 };
    }
}
