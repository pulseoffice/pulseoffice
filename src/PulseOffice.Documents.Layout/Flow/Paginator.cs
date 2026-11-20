using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Objects;

namespace PulseOffice.Documents.Layout.Flow;

public interface IConvergenceProbe
{
    IReadOnlyList<PageBox>? Remainder(int pageIndex, PageStart start);
}

public sealed record PaginationRequest(
    IReadOnlyList<FlowItem> Flow,
    Func<string, PageSide, PageGeometry> Geometry,
    string InitialStyle,
    IReadOnlyList<PageBox> Prefix,
    PageStart Resume,
    IConvergenceProbe? Probe)
{
    public Func<string, double, ComposedStory?> Note { get; init; } = (_, _) => null;

    public Func<string, double, ComposedStory?> Notice { get; init; } = (_, _) => null;

    public PulseOffice.Documents.Annotations.NoteSeparator Separator { get; init; } = new();

    public Func<string, PulseOffice.Documents.Annotations.NoteSeparator?>? PageSeparator { get; init; }

    public bool FootnotesBelowText { get; init; }

    public string? ContinuationForward { get; init; }

    public string? ContinuationBackward { get; init; }

    public Func<string, AnchoredObject?> Object { get; init; } = _ => null;

    public Func<int, IReadOnlyList<AnchoredObject>> PageObjects { get; init; } = _ => [];

    public Func<ParagraphEntry, string, int, ParagraphEntry> PerPage { get; init; } = (entry, _, _) => entry;

    public string PageCount { get; init; } = "00";

    public Func<string, int, string> PageNumber { get; init; } = (_, number) => number.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public Func<string, double> FrameInset { get; init; } = _ => 0;

    public Func<string, double> FrameAlignment { get; init; } = _ => 0;

    public Func<string, double, double, PulseOffice.Drawing.Shapes.ShapeTextMargins> FrameMargins { get; init; } = (_, _, _) => PulseOffice.Drawing.Shapes.ShapeTextMargins.None;

    public Func<string, ComposedStory?> Frame { get; init; } = _ => null;

    public Func<string, IReadOnlyList<MemberFrame>> MemberFrames { get; init; } = _ => [];
}

public sealed record MemberFrame(string MemberId, double X, double Y, double Width, double Height, double Inset, double Alignment, ComposedStory Story);

public interface IPaginator
{
    IReadOnlyList<PageBox> Paginate(PaginationRequest request);
}

public sealed class Paginator(ITableComposer tables, IStoryComposer stories, IParagraphDecorator decorator, IObjectPlacement objects, IWrapSegments wrap, IColumnBalancer balancer, IPageBackdrops backdrops, PageFlowOptions options, IObjectOutlines outlines) : IPaginator
{
    internal IObjectOutlines Outlines { get; } = outlines;

    internal IPageBackdrops Backdrops { get; } = backdrops;

    internal ITableComposer Tables { get; } = tables;

    internal IStoryComposer Stories { get; } = stories;

    internal IParagraphDecorator Decorator { get; } = decorator;

    internal IObjectPlacement Objects { get; } = objects;

    internal IWrapSegments Wrap { get; } = wrap;

    internal IColumnBalancer Balancer { get; } = balancer;

    internal PageFlowOptions Options { get; } = options;

    public IReadOnlyList<PageBox> Paginate(PaginationRequest request) => new PaginationRun(this, request).Run();
}
