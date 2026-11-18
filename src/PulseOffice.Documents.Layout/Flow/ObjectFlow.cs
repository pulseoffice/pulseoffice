using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Objects;

namespace PulseOffice.Documents.Layout.Flow;

internal sealed record Obstacle(WrapObstacle Shape, LayoutRect Area)
{
    public LayoutRect Rect => Shape.Rect;

    public WrapMode Mode => Shape.Mode;

    public string? OnlyParagraph { get; init; }

    public Obstacle For(string paragraphId) => OnlyParagraph is { } only && only != paragraphId && Mode != WrapMode.Through ? this with { Shape = Shape with { Mode = WrapMode.None, Outline = null } } : this;
}

internal sealed class ObjectFlow(PaginationRun pagination, PaginationRequest request, IObjectPlacement placement, IStoryComposer stories, IObjectOutlines outlines)
{
    private readonly List<ObjectBox> _objects = [];
    private readonly List<Obstacle> _obstacles = [];
    private readonly HashSet<string> _placedObjects = new(StringComparer.Ordinal);

    public IReadOnlyList<ObjectBox> Boxes => _objects;

    public IEnumerable<Obstacle> Blocking(IReadOnlyList<Obstacle> candidates, string paragraphId) =>
        _obstacles.Concat(candidates.Where(Blocks)).Where(Blocks).Select(obstacle => obstacle.For(paragraphId));

    public void OpenPage(int number)
    {
        _obstacles.Clear();
        var geometry = pagination.Geometry;
        foreach (var item in request.PageObjects(number))
        {
            AddObject(item, placement.Place(item, new ObjectFrame(geometry, geometry.Body.X, geometry.Body.Width, geometry.Body.Y)));
        }
    }

    public void Clear()
    {
        _objects.Clear();
        _placedObjects.Clear();
    }

    public IReadOnlyList<Obstacle> Candidates(ParagraphEntry entry, double top)
    {
        var result = new List<Obstacle>();
        foreach (var id in entry.AnchoredObjects)
        {
            if (_placedObjects.Contains(id) || request.Object(id) is not { } item) continue;
            var rect = Grown(item, placement.Place(item, new ObjectFrame(pagination.Geometry, pagination.ColumnLeft, pagination.ColumnWidth, top)));
            var turned = RotatedRect.Around(rect, item.RotationDegrees);
            result.Add(Obstacle(item, rect, turned));
        }
        return result;
    }

    public bool Wrapped(IReadOnlyList<Obstacle> candidates)
    {
        var left = pagination.ColumnLeft;
        var band = new LayoutRect(left, pagination.Y, pagination.ColumnWidth, Math.Max(0, pagination.Bottom - pagination.Y));
        return _obstacles.Concat(candidates).Any(obstacle => Blocks(obstacle) && obstacle.Rect.Intersects(band));
    }

    private Obstacle Obstacle(AnchoredObject item, LayoutRect rect, LayoutRect turned) =>
        new(new WrapObstacle(Spaced(turned, item), item.Wrap) { Outline = outlines.Outline(item, rect), Spread = item.WrapSpacing }, rect)
        {
            OnlyParagraph = item.WrapFirstParagraph && item.Anchor.Accept(new AnchorParagraph()) is { } paragraph ? paragraph : null,
        };

    private bool Blocks(Obstacle obstacle) => obstacle.Mode != WrapMode.Through;

    public void Commit(ParagraphEntry entry, IReadOnlyList<Obstacle> candidates)
    {
        if (candidates.Count == 0) return;
        var index = 0;
        foreach (var id in entry.AnchoredObjects)
        {
            if (_placedObjects.Contains(id) || request.Object(id) is not { } item) continue;
            var obstacle = candidates[index++];
            AddObject(item, obstacle.Area);
        }
    }

    private void AddObject(AnchoredObject item, LayoutRect rect)
    {
        _placedObjects.Add(item.Id);
        var layer = item.InBackground ? ObjectLayer.Behind : item.Wrap == WrapMode.Through ? ObjectLayer.Front : ObjectLayer.Wrapped;
        rect = Grown(item, rect);
        var content = FrameContent(item.Id, rect);
        _objects.Add(new ObjectBox(item.Id, rect, layer) { Content = content, RotationDegrees = item.RotationDegrees, FlipHorizontal = item.FlipHorizontal, FlipVertical = item.FlipVertical });
        foreach (var line in content.OfType<LineBox>().Concat(content.OfType<TableBox>().SelectMany(table => table.Lines))) Inline(line);
        if (request.Frame(item.Id) is { } frame)
            foreach (var note in frame.Atoms.SelectMany(atom => atom.Notes).Distinct()) pagination.Footnotes.Reserve(note, pagination.Y);
        var turned = RotatedRect.Around(rect, item.RotationDegrees);
        if (layer == ObjectLayer.Wrapped) _obstacles.Add(Obstacle(item, rect, turned));
    }

    private LayoutRect Grown(AnchoredObject item, LayoutRect rect)
    {
        if (request.Frame(item.Id) is not { } frame) return rect;
        var height = frame.Height + 2 * request.FrameInset(item.Id);
        return rect.Height < height ? rect with { Height = height } : rect;
    }

    private LayoutRect Spaced(LayoutRect rect, AnchoredObject item)
    {
        var spacing = item.WrapSpacing;
        return new LayoutRect(
            rect.X - spacing.Left.ToPoints(),
            rect.Y - spacing.Top.ToPoints(),
            rect.Width + spacing.Left.ToPoints() + spacing.Right.ToPoints(),
            rect.Height + spacing.Top.ToPoints() + spacing.Bottom.ToPoints());
    }

    private IReadOnlyList<LayoutBox> FrameContent(string id, LayoutRect rect)
    {
        if (request.Frame(id) is { } frame) return StoryIn(frame, rect, request.FrameInset(id), request.FrameAlignment(id), id);
        return [.. request.MemberFrames(id).SelectMany(member => StoryIn(member.Story,
            new LayoutRect(rect.X + member.X * rect.Width, rect.Y + member.Y * rect.Height, member.Width * rect.Width, member.Height * rect.Height),
            member.Inset, member.Alignment, id))];
    }

    private IReadOnlyList<LayoutBox> StoryIn(ComposedStory frame, LayoutRect rect, double inset, double alignment, string id)
    {
        var slack = Math.Max(0, rect.Height - 2 * inset - frame.Height) * alignment;
        return stories.Emit(frame, 0, frame.Atoms.Count, rect.X + inset, rect.Y + inset + slack + (frame.Atoms.Count > 0 ? frame.Atoms[0].Top : 0), Math.Max(1, rect.Width - 2 * inset), StoryKind.Frame, id);
    }

    public void Inline(LineBox box)
    {
        foreach (var run in box.Line.Runs.Where(run => run.Kind == GlyphRunKind.Object && run.ObjectId is not null).ToList())
        {
            var height = run.Ascent;
            var item = request.Object(run.ObjectId!);
            var rect = new LayoutRect(box.Left + run.X, box.Baseline - height, run.Width, height);
            var content = item is null ? [] : FrameContent(item.Id, rect);
            _objects.Add(new ObjectBox(run.ObjectId!, rect, ObjectLayer.Inline)
            {
                Content = content,
                RotationDegrees = item?.RotationDegrees ?? 0,
                FlipHorizontal = item?.FlipHorizontal ?? false,
                FlipVertical = item?.FlipVertical ?? false,
            });
        }
    }
}
