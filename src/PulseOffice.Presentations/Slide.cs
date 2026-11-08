using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations;

public enum SlideLayout
{
    TitleSlide,
    TitleAndContent,
    SectionHeader,
    TwoContent,
    Comparison,
    TitleOnly,
    Blank,
    ContentWithCaption,
    PictureWithCaption,
    VerticalText,
    VerticalTitleAndText,
    CenteredText,
    ContentAndTwoContent,
    TwoContentAndContent,
    ContentOverContent,
    TwoContentOverContent,
    FourContent,
    SixContent,
    VerticalTitleTextChart,
    TitleTwoVerticalText,
}

public enum BackgroundImageMode { Stretch, Tile, Natural }

public sealed record BackgroundImageLayout(BackgroundImageMode Mode, PulseOffice.Foundation.Units.Twip Width, PulseOffice.Foundation.Units.Twip Height)
{
    public double Opacity { get; init => field = double.IsNaN(value) ? 1 : Math.Clamp(value, 0, 1); } = 1;

    public static readonly BackgroundImageLayout Stretched = new(BackgroundImageMode.Stretch, PulseOffice.Foundation.Units.Twip.Zero, PulseOffice.Foundation.Units.Twip.Zero);
}

public sealed record NativeDiagram(IReadOnlyList<SlideElement> Members, string FrameXml, IReadOnlyList<NativeDiagramPart> Parts)
{
    public IReadOnlyDictionary<string, string> PointsByMember { get; init; } = new Dictionary<string, string>();
}

public sealed record NativeDiagramPart(string Role, string ContentType, byte[] Data);

public sealed class Slide
{
    private readonly List<SlideElement> _elements = [];
    private readonly List<AnimationEffect> _animations = [];
    private readonly List<InteractiveAction> _interactions = [];
    private readonly List<SlideComment> _comments = [];
    private readonly List<NativeDiagram> _diagrams = [];
    private readonly List<SlideGroup> _groups = [];

    public Slide(string id, SlideLayout layout)
    {
        Id = id;
        Layout = layout;
    }

    public string Id { get; }

    public SlideLayout Layout { get; set; }

    public string? LayoutId { get; set; }

    public uint? Background { get; set; }

    public PulseOffice.Drawing.Assets.RasterImageObject? BackgroundImage { get; set; }

    public BackgroundImageLayout BackgroundLayout { get; set; } = BackgroundImageLayout.Stretched;

    public PulseOffice.Drawing.Shapes.ShapeFill? BackgroundFill { get; set; }

    public string? Name { get; set; }

    public string Notes { get; set; } = string.Empty;

    public bool Hidden { get; set; }

    public string? MasterId { get; set; }

    public bool ShowMasterBackground { get; set; } = true;

    public bool ShowMasterObjects { get; set; } = true;

    public string? SectionName { get; set; }

    public string? SectionId { get; set; }

    public SlideTransition Transition { get; set; } = SlideTransition.None;

    public IReadOnlyList<SlideElement> Elements => _elements;

    public IReadOnlyList<AnimationEffect> Animations => _animations;

    public IReadOnlyList<InteractiveAction> Interactions => _interactions;

    public IReadOnlyList<SlideComment> Comments => _comments;

    public IReadOnlyList<NativeDiagram> Diagrams => _diagrams;

    public IReadOnlyList<SlideGroup> Groups => _groups;

    public void AddGroup(SlideGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        _groups.Add(group);
    }

    public void ReplaceGroups(IEnumerable<SlideGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        var replaced = groups.ToList();
        _groups.Clear();
        _groups.AddRange(replaced);
    }

    public bool RemoveGroup(string id) => _groups.RemoveAll(group => string.Equals(group.Id, id, StringComparison.Ordinal)) > 0;

    public void AddDiagram(NativeDiagram diagram)
    {
        ArgumentNullException.ThrowIfNull(diagram);
        _diagrams.Add(diagram);
    }

    public IEnumerable<NativeDiagram> IntactDiagrams() =>
        _diagrams.Where(diagram => diagram.Members.Count > 0
            && diagram.Members.All(member => _elements.Any(element => ReferenceEquals(element, member))));

    public IEnumerable<NativeDiagram> TextEditedDiagrams() =>
        _diagrams.Where(diagram => diagram.Members.Count > 0
            && !diagram.Members.All(member => _elements.Any(element => ReferenceEquals(element, member)))
            && diagram.Members.All(member => Current(member) is { } current && current.Bounds == member.Bounds && current.GetType() == member.GetType()
                && (ReferenceEquals(current, member) || diagram.PointsByMember.ContainsKey(member.Id))));

    public SlideElement? Current(SlideElement member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return _elements.FirstOrDefault(element => element.Id == member.Id);
    }

    public void AddComment(SlideComment comment)
    {
        ArgumentNullException.ThrowIfNull(comment);
        _comments.Add(comment);
    }

    public void InsertComment(int index, SlideComment comment)
    {
        ArgumentNullException.ThrowIfNull(comment);
        _comments.Insert(Math.Clamp(index, 0, _comments.Count), comment);
    }

    public int CommentIndex(string id) => _comments.FindIndex(c => c.Id == id);

    public bool ReplaceComment(SlideComment comment)
    {
        ArgumentNullException.ThrowIfNull(comment);
        var index = _comments.FindIndex(c => c.Id == comment.Id);
        if (index < 0) return false;
        _comments[index] = comment;
        return true;
    }

    public bool RemoveComment(string id)
    {
        var index = _comments.FindIndex(c => c.Id == id);
        if (index < 0) return false;
        _comments.RemoveAt(index);
        return true;
    }

    public string NextAnimationId()
    {
        var index = _animations.Count;
        string candidate;
        do candidate = $"{Id}.anim{++index}";
        while (_animations.Any(effect => string.Equals(effect.Id, candidate, StringComparison.Ordinal)));
        return candidate;
    }

    public void AddAnimation(AnimationEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        _animations.Add(effect);
    }

    public void InsertAnimation(int index, AnimationEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        _animations.Insert(Math.Clamp(index, 0, _animations.Count), effect);
    }

    public bool RemoveAnimation(string id)
    {
        var index = _animations.FindIndex(a => a.Id == id);
        if (index < 0) return false;

        _animations.RemoveAt(index);
        return true;
    }

    public void AddInteraction(InteractiveAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _interactions.Add(action);
        _interactionIndex?.TryAdd(action.TargetId, _interactions.Count - 1);
    }

    public void SetInteraction(InteractiveAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (InteractionIndex.TryGetValue(action.TargetId, out var index)) _interactions[index] = action;
        else AddInteraction(action);
    }

    public bool RemoveInteraction(string targetId)
    {
        if (!InteractionIndex.TryGetValue(targetId, out var index)) return false;
        _interactions.RemoveAt(index);
        _interactionIndex = null;
        return true;
    }

    public InteractiveAction? Interaction(string targetId) =>
        InteractionIndex.TryGetValue(targetId, out var index) ? _interactions[index] : null;

    private Dictionary<string, int>? _interactionIndex;

    private Dictionary<string, int> InteractionIndex
    {
        get
        {
            if (_interactionIndex is { } known) return known;
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var position = 0; position < _interactions.Count; position++) index.TryAdd(_interactions[position].TargetId, position);
            return _interactionIndex = index;
        }
    }

    public void Add(SlideElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        _elements.Add(element);
    }

    public bool Remove(string id)
    {
        var index = _elements.FindIndex(e => e.Id == id);
        if (index < 0) return false;

        _elements.RemoveAt(index);
        return true;
    }

    public void Replace(SlideElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        var index = _elements.FindIndex(e => e.Id == element.Id);
        if (index < 0) _elements.Add(element);
        else _elements[index] = element;
    }

    public Slide Copy()
    {
        var copy = new Slide(Id, Layout)
        {
            LayoutId = LayoutId,
            Background = Background,
            BackgroundImage = BackgroundImage,
            BackgroundLayout = BackgroundLayout,
            BackgroundFill = BackgroundFill,
            Name = Name,
            Notes = Notes,
            Hidden = Hidden,
            MasterId = MasterId,
            ShowMasterBackground = ShowMasterBackground,
            ShowMasterObjects = ShowMasterObjects,
            SectionName = SectionName,
            SectionId = SectionId,
            Transition = Transition,
        };
        copy._elements.AddRange(_elements);
        copy._animations.AddRange(_animations);
        copy._interactions.AddRange(_interactions);
        copy._comments.AddRange(_comments);
        copy._diagrams.AddRange(_diagrams);
        copy._groups.AddRange(_groups);
        return copy;
    }

    public SlideElement? Find(string id) => _elements.FirstOrDefault(e => e.Id == id);

    public TextElement? Placeholder(PlaceholderRole role) =>
        _elements.OfType<TextElement>().FirstOrDefault(t => t.Text.Role == role);

    public IEnumerable<SlideElement> InZOrder() => _elements.OrderBy(e => e.ZOrder);
}

public sealed record SlideBackgroundChoice(
    PulseOffice.Drawing.Shapes.ShapeFill? Fill,
    PulseOffice.Drawing.Assets.RasterImageObject? Image,
    BackgroundImageLayout Layout,
    bool HideGraphics);
