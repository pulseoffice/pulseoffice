using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Blocks;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Documents.Objects;

public enum WrapMode
{
    None,
    Parallel,
    Left,
    Right,
    Optimal,
    Through,
    TopAndBottom,
}

public enum HorizontalRelation
{
    Paragraph,
    PageArea,
    Page,
    Margin,
    Character,
    Column,
}

public enum VerticalRelation
{
    Paragraph,
    PageArea,
    Page,
    Margin,
    Line,
    Character,
}

public abstract record ObjectAnchor
{
    public abstract T Accept<T>(IObjectAnchorVisitor<T> visitor);
}

public sealed record PageAnchor(int PageNumber) : ObjectAnchor
{
    public override T Accept<T>(IObjectAnchorVisitor<T> visitor) => visitor.VisitPage(this);
}

public sealed record ParagraphAnchor(string ParagraphId) : ObjectAnchor
{
    public override T Accept<T>(IObjectAnchorVisitor<T> visitor) => visitor.VisitParagraph(this);
}

public sealed record CharacterAnchor(string ParagraphId) : ObjectAnchor
{
    public override T Accept<T>(IObjectAnchorVisitor<T> visitor) => visitor.VisitCharacter(this);
}

public sealed record AsCharacterAnchor(string ParagraphId) : ObjectAnchor
{
    public override T Accept<T>(IObjectAnchorVisitor<T> visitor) => visitor.VisitAsCharacter(this);
}

public interface IObjectAnchorVisitor<out T>
{
    T VisitPage(PageAnchor anchor);

    T VisitParagraph(ParagraphAnchor anchor);

    T VisitCharacter(CharacterAnchor anchor);

    T VisitAsCharacter(AsCharacterAnchor anchor);
}

public abstract class AnchoredContent
{
    public virtual BlockContainer? Story => null;

    public virtual IEnumerable<BlockContainer> Stories => Story is { } story ? [story] : [];

    public abstract AnchoredContent WithStory(BlockContainer story);

    public abstract AnchoredContent Clone();
}

public sealed class EmbeddedContent(EmbeddedObject value, BlockContainer? story = null, IReadOnlyDictionary<string, BlockContainer>? memberStories = null) : AnchoredContent
{
    public EmbeddedObject Value { get; } = value;

    public override BlockContainer? Story { get; } = story;

    public IReadOnlyDictionary<string, BlockContainer> MemberStories { get; } = memberStories ?? new Dictionary<string, BlockContainer>(StringComparer.Ordinal);

    public override IEnumerable<BlockContainer> Stories => base.Stories.Concat(MemberStories.Values);

    public EmbeddedContent With(EmbeddedObject value) => new(value, Story, MemberStories);

    public override AnchoredContent WithStory(BlockContainer story) => new EmbeddedContent(Value, story, ClonedMembers());

    public override AnchoredContent Clone() => new EmbeddedContent(Value, Story?.Clone(), ClonedMembers());

    private Dictionary<string, BlockContainer> ClonedMembers() =>
        MemberStories.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);
}

public sealed class TextBoxContent(BlockContainer content) : AnchoredContent
{
    public BlockContainer Content { get; } = content;

    public bool AutoGrowHeight { get; set; } = true;

    public string? NextFrameName { get; set; }

    public Styles.FrameProperties? Look { get; set; }

    public override BlockContainer? Story => Content;

    public override AnchoredContent WithStory(BlockContainer story) => new TextBoxContent(story)
    {
        AutoGrowHeight = AutoGrowHeight,
        NextFrameName = NextFrameName,
        Look = Look,
    };

    public override AnchoredContent Clone() => new TextBoxContent(Content.Clone())
    {
        AutoGrowHeight = AutoGrowHeight,
        NextFrameName = NextFrameName,
        Look = Look,
    };
}

public sealed record ObjectPlacement(Twip X, Twip Y)
{
    public HorizontalRelation Horizontal { get; init; } = HorizontalRelation.Paragraph;

    public VerticalRelation Vertical { get; init; } = VerticalRelation.Paragraph;
}

public readonly record struct ContourPoint(double X, double Y);

public sealed record ObjectContour(IReadOnlyList<ContourPoint> Points)
{
    public bool Equals(ObjectContour? other) => other is not null && Points.SequenceEqual(other.Points);

    public override int GetHashCode() => Points.Aggregate(Points.Count, (hash, point) => HashCode.Combine(hash, point));
}

public sealed class AnchoredObject(string id, ObjectAnchor anchor, AnchoredContent content, DocumentSize size)
{
    public string Id { get; } = id;

    public ObjectAnchor Anchor
    {
        get;
        set
        {
            if (Equals(field, value)) return;
            field = value;
            AnchorChanged?.Invoke();
        }
    } = anchor;

    internal event Action? AnchorChanged;

    public AnchoredContent Content { get; } = content;

    public DocumentSize Size { get; set; } = size;

    public ObjectPlacement Placement { get; set; } = new(Twip.Zero, Twip.Zero);

    public WrapMode Wrap { get; set; } = WrapMode.Parallel;

    public Margins WrapSpacing { get; set; } = Margins.Zero;

    public string? StyleName { get; set; }

    public string? Name { get; set; }

    public int ZOrder { get; set; }

    public double RotationDegrees { get; set; }

    public bool InBackground { get; set; }

    public bool WrapContour { get; set; }

    public ObjectContour? Contour { get; set; }

    public bool WrapFirstParagraph { get; set; }

    public bool FlipHorizontal { get; set; }

    public bool FlipVertical { get; set; }

    public string? Hyperlink { get; set; }

    public AnchoredObject Clone() => CopyAs(Id, Anchor, Content.Clone());

    public void Assign(AnchoredObject source)
    {
        Anchor = source.Anchor;
        Size = source.Size;
        Placement = source.Placement;
        Wrap = source.Wrap;
        WrapSpacing = source.WrapSpacing;
        StyleName = source.StyleName;
        Name = source.Name;
        ZOrder = source.ZOrder;
        RotationDegrees = source.RotationDegrees;
        InBackground = source.InBackground;
        WrapContour = source.WrapContour;
        Contour = source.Contour;
        WrapFirstParagraph = source.WrapFirstParagraph;
        FlipHorizontal = source.FlipHorizontal;
        FlipVertical = source.FlipVertical;
        Hyperlink = source.Hyperlink;
    }

    public bool SameState(AnchoredObject other) =>
        Anchor == other.Anchor && Size == other.Size && Placement == other.Placement && Wrap == other.Wrap && WrapSpacing == other.WrapSpacing
        && StyleName == other.StyleName && Name == other.Name && ZOrder == other.ZOrder && RotationDegrees.Equals(other.RotationDegrees)
        && InBackground == other.InBackground && WrapContour == other.WrapContour && Equals(Contour, other.Contour)
        && WrapFirstParagraph == other.WrapFirstParagraph && FlipHorizontal == other.FlipHorizontal && FlipVertical == other.FlipVertical && Hyperlink == other.Hyperlink;

    public AnchoredObject CopyAs(string id, ObjectAnchor anchor, AnchoredContent content) => new(id, anchor, content, Size)
    {
        Placement = Placement,
        Wrap = Wrap,
        WrapSpacing = WrapSpacing,
        StyleName = StyleName,
        Name = Name,
        ZOrder = ZOrder,
        RotationDegrees = RotationDegrees,
        InBackground = InBackground,
        WrapContour = WrapContour,
        Contour = Contour,
        WrapFirstParagraph = WrapFirstParagraph,
        FlipHorizontal = FlipHorizontal,
        FlipVertical = FlipVertical,
        Hyperlink = Hyperlink,
    };
}

public sealed class AnchoredObjectCollection
{
    private readonly List<AnchoredObject> _objects = [];

    private readonly AnchorParagraph _anchorParagraph = new();

    private Dictionary<string, AnchoredObject>? _byId;

    private Dictionary<string, List<AnchoredObject>>? _byParagraph;

    public void Replace(IEnumerable<AnchoredObject> items)
    {
        var copy = items.ToList();
        Clear();
        foreach (var item in copy) Add(item);
    }

    public IReadOnlyList<AnchoredObject> All => _objects;

    public AnchoredObject? Find(string id)
    {
        if (_byId is null)
        {
            _byId = new Dictionary<string, AnchoredObject>(StringComparer.Ordinal);
            foreach (var item in _objects) _byId.TryAdd(item.Id, item);
        }
        return _byId.GetValueOrDefault(id);
    }

    public void Add(AnchoredObject item)
    {
        _objects.Add(item);
        item.AnchorChanged += Invalidate;
        Invalidate();
    }

    public bool Remove(string id)
    {
        var removed = _objects.Where(item => item.Id == id).ToList();
        foreach (var item in removed)
        {
            item.AnchorChanged -= Invalidate;
            _objects.Remove(item);
        }
        Invalidate();
        return removed.Count > 0;
    }

    public IEnumerable<AnchoredObject> AnchoredTo(string paragraphId)
    {
        if (_byParagraph is null)
        {
            _byParagraph = new Dictionary<string, List<AnchoredObject>>(StringComparer.Ordinal);
            foreach (var item in _objects)
            {
                if (item.Anchor.Accept(_anchorParagraph) is not { } paragraph) continue;
                if (!_byParagraph.TryGetValue(paragraph, out var anchored)) _byParagraph[paragraph] = anchored = [];
                anchored.Add(item);
            }
        }
        return _byParagraph.TryGetValue(paragraphId, out var found) ? found : [];
    }

    public void CopyFrom(AnchoredObjectCollection other)
    {
        var copies = other._objects.Select(item => item.Clone()).ToList();
        Clear();
        foreach (var item in copies) Add(item);
    }

    private void Clear()
    {
        foreach (var item in _objects) item.AnchorChanged -= Invalidate;
        _objects.Clear();
        Invalidate();
    }

    private void Invalidate()
    {
        _byId = null;
        _byParagraph = null;
    }
}

public sealed class AnchorParagraph : IObjectAnchorVisitor<string?>
{
    public string? VisitPage(PageAnchor anchor) => null;

    public string? VisitParagraph(ParagraphAnchor anchor) => anchor.ParagraphId;

    public string? VisitCharacter(CharacterAnchor anchor) => anchor.ParagraphId;

    public string? VisitAsCharacter(AsCharacterAnchor anchor) => anchor.ParagraphId;
}

public sealed class AnchorRetarget(string paragraphId) : IObjectAnchorVisitor<ObjectAnchor>
{
    public ObjectAnchor VisitPage(PageAnchor anchor) => anchor;

    public ObjectAnchor VisitParagraph(ParagraphAnchor anchor) => new ParagraphAnchor(paragraphId);

    public ObjectAnchor VisitCharacter(CharacterAnchor anchor) => new CharacterAnchor(paragraphId);

    public ObjectAnchor VisitAsCharacter(AsCharacterAnchor anchor) => new AsCharacterAnchor(paragraphId);
}
