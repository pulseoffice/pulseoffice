using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations;

public interface ISlideFactory
{
    Slide Create(string id, SlideMaster master, SlideLayout layout);

    Slide Clone(Slide source, string newId);

    Slide Relayout(Slide source, SlideMaster master, SlideLayout layout);

    Slide Create(string id, SlideLayoutTemplate template);

    Slide Relayout(Slide source, SlideMaster master, SlideLayoutTemplate template);
}

public sealed class SlideFactory : ISlideFactory
{
    public Slide Create(string id, SlideMaster master, SlideLayout layout) => Create(id, master.Template(layout));

    public Slide Create(string id, SlideLayoutTemplate template)
    {
        var slide = new Slide(id, template.Kind) { LayoutId = template.Id };

        var order = 0;
        foreach (var placeholder in template.Placeholders)
        {
            slide.Add(new TextElement
            {
                Id = $"{id}.{placeholder.Role.ToString().ToLowerInvariant()}{order}",
                Bounds = placeholder.Bounds,
                ZOrder = order,
                Text = new RichText(placeholder.Role, [placeholder.Seed]) { Frame = placeholder.Frame },
            });
            order++;
        }

        return slide;
    }

    public Slide Clone(Slide source, string newId)
    {
        var clone = new Slide(newId, source.Layout)
        {
            LayoutId = source.LayoutId,
            Background = source.Background,
            BackgroundImage = source.BackgroundImage,
            BackgroundLayout = source.BackgroundLayout,
            BackgroundFill = source.BackgroundFill,
            Notes = source.Notes,
            Hidden = source.Hidden,
            MasterId = source.MasterId,
            ShowMasterBackground = source.ShowMasterBackground,
            ShowMasterObjects = source.ShowMasterObjects,
            Transition = source.Transition,
        };

        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in source.Elements.Select(element => element.Id).Concat(source.Groups.Select(group => group.Id)))
            if (!ids.ContainsKey(id)) ids[id] = Unique(Swap(id, source.Id, newId), taken);
        var renamed = source.Elements.Select(element => (Old: element, New: Reid(element, ids[element.Id]))).ToList();
        var remap = new ElementIdRemap(ids);
        var copies = new Dictionary<SlideElement, SlideElement>(ReferenceEqualityComparer.Instance);
        foreach (var (old, element) in renamed)
        {
            var copy = remap.Apply(element);
            copies.TryAdd(old, copy);
            clone.Add(copy);
        }

        foreach (var diagram in source.IntactDiagrams())
            clone.AddDiagram(diagram with
            {
                Members = [.. diagram.Members.Select(member => copies[member])],
                PointsByMember = diagram.PointsByMember.ToDictionary(pair => ids.TryGetValue(pair.Key, out var mapped) ? mapped : pair.Key, pair => pair.Value, StringComparer.Ordinal),
            });

        foreach (var group in source.Groups)
            clone.AddGroup(group with
            {
                Id = ids[group.Id],
                Members = [.. group.Members.Select(member => ids.TryGetValue(member, out var mapped) ? mapped : member)],
            });

        foreach (var animation in source.Animations)
            clone.AddAnimation(animation with
            {
                Id = $"{newId}.{animation.Id}",
                TargetId = Mapped(ids, animation.TargetId, source.Id, newId),
            });

        foreach (var interaction in source.Interactions)
            clone.AddInteraction(interaction with { TargetId = Mapped(ids, interaction.TargetId, source.Id, newId) });

        foreach (var comment in source.Comments)
            clone.AddComment(comment with { Id = $"{newId}.{comment.Id}" });

        return clone;
    }

    public Slide Relayout(Slide source, SlideMaster master, SlideLayout layout) =>
        Relayout(source, master, master.Template(layout));

    public Slide Relayout(Slide source, SlideMaster master, SlideLayoutTemplate template)
    {
        var fresh = Create(source.Id, template);

        var target = new Slide(source.Id, template.Kind)
        {
            LayoutId = template.Id,
            Background = source.Background,
            BackgroundImage = source.BackgroundImage,
            BackgroundLayout = source.BackgroundLayout,
            BackgroundFill = source.BackgroundFill,
            Name = source.Name,
            Notes = source.Notes,
            Hidden = source.Hidden,
            SectionName = source.SectionName,
            Transition = source.Transition,
            MasterId = source.MasterId,
            ShowMasterBackground = source.ShowMasterBackground,
            ShowMasterObjects = source.ShowMasterObjects,
        };

        var waiting = source.Elements.OfType<TextElement>()
            .Where(text => text.Text.Role != PlaceholderRole.Free && !text.Text.IsEmpty)
            .GroupBy(text => text.Text.Role)
            .ToDictionary(group => group.Key, group => new Queue<TextElement>(group));
        var surviving = source.Elements.Where(Survives).Select(element => element.Id).ToHashSet(StringComparer.Ordinal);
        var taken = new HashSet<string>(StringComparer.Ordinal);
        var placed = new Dictionary<string, SlideElement>(StringComparer.Ordinal);
        foreach (var element in fresh.Elements)
        {
            if (element is TextElement placeholder && waiting.TryGetValue(placeholder.Text.Role, out var queue) && queue.TryDequeue(out var existing))
            {
                taken.Add(existing.Id);
                var moved = placeholder with { Id = existing.Id, Text = existing.Text with { Frame = placeholder.Text.Frame } };
                placed[existing.Id] = moved;
                continue;
            }
            var id = FreeId(element.Id, surviving, taken);
            placed[id] = element with { Id = id };
        }

        var order = new List<SlideElement>(source.Elements.Count + fresh.Elements.Count);
        order.AddRange(placed.Values);
        foreach (var element in source.Elements)
        {
            if (placed.ContainsKey(element.Id) || !Survives(element) || !taken.Add(element.Id)) continue;
            order.Add(element is TextElement { Text.Role: not PlaceholderRole.Free } orphan
                ? orphan with { Text = orphan.Text with { Role = PlaceholderRole.Free } }
                : element);
        }
        foreach (var element in order) target.Add(element);

        var kept = order.Select(element => element.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var animation in source.Animations.Where(animation => kept.Contains(animation.TargetId)))
            target.AddAnimation(animation);
        foreach (var interaction in source.Interactions.Where(interaction => kept.Contains(interaction.TargetId)))
            target.AddInteraction(interaction);
        foreach (var comment in source.Comments)
            target.AddComment(comment);
        var groups = source.Groups.Select(group => group.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var group in source.Groups.Where(group => group.Members.All(member => kept.Contains(member) || groups.Contains(member))))
            target.AddGroup(group);
        var instances = order.ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (var diagram in source.IntactDiagrams().Where(diagram => diagram.Members.All(instances.Contains)))
            target.AddDiagram(diagram);

        return target;
    }

    private string FreeId(string id, HashSet<string> surviving, HashSet<string> taken)
    {
        var candidate = id;
        for (var suffix = 2; surviving.Contains(candidate) || !taken.Add(candidate); suffix++)
            candidate = $"{id}.{suffix}";
        return candidate;
    }

    private bool Survives(SlideElement element) =>
        element is not TextElement text || text.Text.Role == PlaceholderRole.Free || !text.Text.IsEmpty;

    private SlideElement Reid(SlideElement element, string id) =>
        element switch
        {
            TextElement text => text with { Id = id },
            ObjectElement obj => obj with { Id = id },
            MediaElement media => media with { Id = id },
            _ => element,
        };

    private string Mapped(Dictionary<string, string> ids, string id, string oldSlideId, string newSlideId) =>
        ids.TryGetValue(id, out var mapped) ? mapped : Swap(id, oldSlideId, newSlideId);

    private string Unique(string candidate, HashSet<string> taken)
    {
        var unique = candidate;
        for (var suffix = 2; !taken.Add(unique); suffix++)
            unique = $"{candidate}.{suffix}";
        return unique;
    }

    private string Swap(string id, string oldSlideId, string newSlideId) =>
        id.StartsWith(oldSlideId + ".", StringComparison.Ordinal)
            ? newSlideId + id[oldSlideId.Length..]
            : $"{newSlideId}.{id}";
}
