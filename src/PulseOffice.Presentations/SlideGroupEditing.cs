using PulseOffice.Foundation.Units;

namespace PulseOffice.Presentations;

public sealed record SlideGroupFrame(DocumentRect Bounds, double RotationDegrees);

public sealed record SlideGroupTransform(IReadOnlyList<SlideElement> Elements, IReadOnlyList<SlideGroup> Groups);

public interface ISlideGroupEditing
{
    IReadOnlyList<SlideGroup>? Group(Slide slide, IReadOnlyList<SlideElement> selection, string? openGroupId, string groupId);

    IReadOnlyList<SlideGroup> Dissolve(Slide slide, SlideGroup group);

    SlideGroupFrame? Frame(Slide slide, SlideGroup group);

    SlideGroupTransform? Transform(Slide slide, SlideGroup group, DocumentRect bounds, double rotationDegrees);

    IReadOnlyList<SlideGroup> Within(IEnumerable<SlideGroup> groups, IReadOnlyCollection<string> elementIds);

    IReadOnlyList<SlideGroup> Outermost(IReadOnlyList<SlideGroup> groups);

    IReadOnlyList<SlideGroup> Renamed(IReadOnlyList<SlideGroup> groups, IReadOnlyDictionary<string, string> elementIds, Func<string> nextId);
}

public sealed class SlideGroupEditing(ISlideGroupUnits units) : ISlideGroupEditing
{
    public SlideGroupEditing() : this(new SlideGroupUnits())
    {
    }

    public IReadOnlyList<SlideGroup>? Group(Slide slide, IReadOnlyList<SlideElement> selection, string? openGroupId, string groupId)
    {
        ArgumentNullException.ThrowIfNull(slide);
        ArgumentNullException.ThrowIfNull(selection);
        var members = new List<string>();
        foreach (var element in selection.Where(element => slide.Find(element.Id) is not null).OrderBy(element => element.ZOrder))
        {
            var unit = units.UnitGroup(slide, element.Id, openGroupId)?.Id ?? element.Id;
            if (!members.Contains(unit, StringComparer.Ordinal)) members.Add(unit);
        }
        if (members.Count < 2) return null;
        var created = new SlideGroup(groupId, members);
        var result = slide.Groups.Select(group => Absorbing(group, members, groupId)).ToList();
        result.Add(created);
        return result;
    }

    public IReadOnlyList<SlideGroup> Dissolve(Slide slide, SlideGroup group)
    {
        ArgumentNullException.ThrowIfNull(slide);
        ArgumentNullException.ThrowIfNull(group);
        return slide.Groups
            .Where(candidate => !string.Equals(candidate.Id, group.Id, StringComparison.Ordinal))
            .Select(candidate => candidate.Members.Contains(group.Id, StringComparer.Ordinal)
                ? candidate with { Members = [.. candidate.Members.SelectMany(member => string.Equals(member, group.Id, StringComparison.Ordinal) ? group.Members : [member])] }
                : candidate)
            .ToList();
    }

    public SlideGroupFrame? Frame(Slide slide, SlideGroup group)
    {
        if (Layout(slide, group) is not { } layout) return null;
        return new SlideGroupFrame(Rect(layout.Pivot.X - layout.Width / 2, layout.Pivot.Y - layout.Height / 2, layout.Width, layout.Height), group.RotationDegrees);
    }

    public SlideGroupTransform? Transform(Slide slide, SlideGroup group, DocumentRect bounds, double rotationDegrees)
    {
        if (Layout(slide, group) is not { } layout) return null;
        var width = Math.Max(1.0, bounds.Width.Value);
        var height = Math.Max(1.0, bounds.Height.Value);
        var scaleX = width / layout.Width;
        var scaleY = height / layout.Height;
        var centre = (X: bounds.Left.Value + width / 2, Y: bounds.Top.Value + height / 2);
        var turn = Radians(rotationDegrees);
        var delta = rotationDegrees - group.RotationDegrees;
        var elements = layout.Leaves.Select(leaf =>
        {
            var offset = Turned((leaf.Centre.X * scaleX, leaf.Centre.Y * scaleY), Math.Cos(turn), Math.Sin(turn));
            var w = leaf.Element.Bounds.Width.Value * scaleX;
            var h = leaf.Element.Bounds.Height.Value * scaleY;
            return leaf.Element with
            {
                Bounds = Rect(centre.X + offset.X - w / 2, centre.Y + offset.Y - h / 2, w, h),
                RotationDegrees = Normalized(leaf.Element.RotationDegrees + delta),
            };
        }).ToList();
        var inside = Nested(slide, group);
        var groups = slide.Groups.Select(candidate => inside.Contains(candidate.Id)
            ? candidate with { RotationDegrees = Normalized(candidate.RotationDegrees + delta) }
            : candidate).ToList();
        return new SlideGroupTransform(elements, groups);
    }

    public IReadOnlyList<SlideGroup> Within(IEnumerable<SlideGroup> groups, IReadOnlyCollection<string> elementIds)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(elementIds);
        var candidates = groups.ToList();
        var inside = new HashSet<string>(elementIds, StringComparer.Ordinal);
        var found = new List<SlideGroup>();
        bool grew;
        do
        {
            grew = false;
            foreach (var candidate in candidates)
            {
                if (found.Contains(candidate) || candidate.Members.Count == 0 || !candidate.Members.All(inside.Contains)) continue;
                found.Add(candidate);
                inside.Add(candidate.Id);
                grew = true;
            }
        }
        while (grew);
        return found;
    }

    public IReadOnlyList<SlideGroup> Outermost(IReadOnlyList<SlideGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        var nested = groups.SelectMany(group => group.Members).ToHashSet(StringComparer.Ordinal);
        return [.. groups.Where(group => !nested.Contains(group.Id))];
    }

    public IReadOnlyList<SlideGroup> Renamed(IReadOnlyList<SlideGroup> groups, IReadOnlyDictionary<string, string> elementIds, Func<string> nextId)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(elementIds);
        ArgumentNullException.ThrowIfNull(nextId);
        var map = new Dictionary<string, string>(elementIds, StringComparer.Ordinal);
        foreach (var group in groups) map[group.Id] = nextId();
        return [.. groups.Select(group => group with
        {
            Id = map[group.Id],
            Members = [.. group.Members.Select(member => map.GetValueOrDefault(member, member))],
        })];
    }

    private SlideGroup Absorbing(SlideGroup group, List<string> members, string groupId)
    {
        if (!group.Members.Any(member => members.Contains(member, StringComparer.Ordinal))) return group;
        var replaced = new List<string>();
        foreach (var member in group.Members)
        {
            if (!members.Contains(member, StringComparer.Ordinal)) replaced.Add(member);
            else if (!replaced.Contains(groupId, StringComparer.Ordinal)) replaced.Add(groupId);
        }
        return group with { Members = replaced };
    }

    private HashSet<string> Nested(Slide slide, SlideGroup group)
    {
        var byId = slide.Groups.GroupBy(candidate => candidate.Id, StringComparer.Ordinal).ToDictionary(set => set.Key, set => set.First(), StringComparer.Ordinal);
        var found = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<SlideGroup>([group]);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!found.Add(current.Id)) continue;
            foreach (var member in current.Members)
                if (byId.TryGetValue(member, out var nested)) pending.Push(nested);
        }
        return found;
    }

    private GroupLayout? Layout(Slide slide, SlideGroup group)
    {
        ArgumentNullException.ThrowIfNull(slide);
        ArgumentNullException.ThrowIfNull(group);
        var leaves = units.Leaves(slide, group).Select(slide.Find).OfType<SlideElement>().ToList();
        if (leaves.Count == 0) return null;
        var turn = Radians(group.RotationDegrees);
        var (cos, sin) = (Math.Cos(turn), Math.Sin(turn));
        var back = leaves.Select(leaf => (Leaf: leaf, Centre: Turned(Centre(leaf), cos, -sin))).ToList();
        var left = back.Min(item => item.Centre.X - item.Leaf.Bounds.Width.Value / 2.0);
        var right = back.Max(item => item.Centre.X + item.Leaf.Bounds.Width.Value / 2.0);
        var top = back.Min(item => item.Centre.Y - item.Leaf.Bounds.Height.Value / 2.0);
        var bottom = back.Max(item => item.Centre.Y + item.Leaf.Bounds.Height.Value / 2.0);
        var middle = ((left + right) / 2, (top + bottom) / 2);
        var pivot = Turned(middle, cos, sin);
        var placed = back.Select(item => new PlacedLeaf(item.Leaf, (item.Centre.X - middle.Item1, item.Centre.Y - middle.Item2))).ToList();
        return new GroupLayout(placed, pivot, Math.Max(1.0, right - left), Math.Max(1.0, bottom - top));
    }

    private (double X, double Y) Centre(SlideElement element) =>
        (element.Bounds.Left.Value + element.Bounds.Width.Value / 2.0, element.Bounds.Top.Value + element.Bounds.Height.Value / 2.0);

    private (double X, double Y) Turned((double X, double Y) point, double cos, double sin) =>
        (point.X * cos - point.Y * sin, point.X * sin + point.Y * cos);

    private double Radians(double degrees) => double.IsFinite(degrees) ? degrees * Math.PI / 180 : 0;

    private DocumentRect Rect(double left, double top, double width, double height) =>
        new(new Twip((int)Math.Round(left)), new Twip((int)Math.Round(top)), new Twip((int)Math.Round(width)), new Twip((int)Math.Round(height)));

    private double Normalized(double degrees)
    {
        if (!double.IsFinite(degrees)) return 0;
        var angle = degrees % 360;
        if (angle < 0) angle += 360;
        return Math.Abs(angle - 360) < 1e-9 ? 0 : angle;
    }

    private sealed record PlacedLeaf(SlideElement Element, (double X, double Y) Centre);

    private sealed record GroupLayout(IReadOnlyList<PlacedLeaf> Leaves, (double X, double Y) Pivot, double Width, double Height);
}
