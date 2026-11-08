using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;

namespace PulseOffice.Presentations.UI;

public sealed class DeckArrangeCommands : DeckCommandSet
{
    private readonly IDeckGrouping grouping;
    private readonly PulseOffice.Drawing.Shapes.IContentFlipping flipping;
    private readonly ISlideGroupUnits units;
    private readonly ISlideGroupEditing editing;
    private readonly ISlideGroupDecomposition decomposition;
    private DeckTarget? _watched;

    public DeckArrangeCommands(IActiveDeckContext context, IDeckGrouping grouping, PulseOffice.Drawing.Shapes.IContentFlipping flipping, ISlideGroupUnits units, ISlideGroupEditing editing, ISlideGroupDecomposition decomposition) : base(context)
    {
        this.grouping = grouping;
        this.units = units;
        this.editing = editing;
        this.decomposition = decomposition;
        this.flipping = flipping;
        context.Changed += (_, _) => Watch(context.Target);
    }

    private void Watch(DeckTarget? target)
    {
        if (!ReferenceEquals(target, _watched))
        {
            if (_watched is not null) _watched.Actions.History.Changed -= OnHistoryChanged;
            _watched = target;
            if (target is not null) target.Actions.History.Changed += OnHistoryChanged;
        }
        Reconcile(target);
    }

    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        if (_watched is { } target && target.Options.OpenGroup is not null && target.Actions.History.CanRedo)
            target.Options.OpenGroup = null;
        Reconcile(_watched);
    }

    private void Reconcile(DeckTarget? target)
    {
        if (target?.Options.OpenGroup is not { } open) return;
        if (!open.IsValidIn(target.Deck))
        {
            target.Options.OpenGroup = null;
            return;
        }
        if (target.CurrentSlide()?.Id != open.SlideId) Leave(target, open);
    }

    private void Leave(DeckTarget t, OpenGroup open)
    {
        t.Options.OpenGroup = null;
        if (t.Deck.Slides.FirstOrDefault(slide => slide.Id == open.SlideId) is not { } owner || !ReferenceEquals(t.CurrentSlide(), owner)) return;
        var group = owner.Groups.FirstOrDefault(candidate => candidate.Id == open.SlideGroupId);
        var leaves = group is null ? open.MemberIds : units.Leaves(owner, group);
        t.Select([.. leaves.Select(owner.Find).OfType<SlideElement>()]);
    }

    private enum Align { Left, CenterH, Right, Top, MiddleV, Bottom }

    private enum Order { Front, Forward, Backward, Back }

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        OrderCommand("deck.order.front", Order.Front),
        OrderCommand("deck.order.forward", Order.Forward),
        OrderCommand("deck.order.backward", Order.Backward),
        OrderCommand("deck.order.back", Order.Back),

        AlignCommand("deck.align.left", Align.Left),
        AlignCommand("deck.align.centerH", Align.CenterH),
        AlignCommand("deck.align.right", Align.Right),
        AlignCommand("deck.align.top", Align.Top),
        AlignCommand("deck.align.middleV", Align.MiddleV),
        AlignCommand("deck.align.bottom", Align.Bottom),

        RotateCommand("deck.object.rotateRight", 90),
        RotateCommand("deck.object.rotateLeft", -90),

        FlipCommand("deck.object.flipHorizontal", horizontal: true),
        FlipCommand("deck.object.flipVertical", horizontal: false),

        GroupCommand(),
        UngroupCommand(),
        EnterGroupCommand(),
        LeaveGroupCommand(),
        ReverseCommand(),
    ];

    private CommandDefinition FlipCommand(string id, bool horizontal) =>
        ObjectCommand(id,
            (slide, element) => Context.Target!.Actions.FlipElement(slide, element.Id, horizontal),
            enabled: (_, element) => element is ObjectElement obj && flipping.Flips(obj.Content));

    private CommandDefinition GroupCommand() =>
        Prototype("deck.object.group") with
        {
            Surface = CommandSurface.Advanced,
            DefaultGesture = "Ctrl+Shift+G",
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is { } current
                && units.Matching(current, t.SelectedMany()) is null
                && editing.Group(current, t.SelectedMany(), t.Options.OpenGroup?.SlideGroupId, "probe") is not null,
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.CurrentSlide() is { } slide
                    && editing.Group(slide, t.SelectedMany(), t.Options.OpenGroup?.SlideGroupId, t.Deck.NextId()) is { } groups)
                {
                    var selection = t.SelectedMany().ToList();
                    t.Actions.ReplaceGroups(slide, groups, "deck.action.group");
                    t.Select(selection);
                }
                return Task.CompletedTask;
            },
        };

    private CommandDefinition UngroupCommand() =>
        Prototype("deck.object.ungroup") with
        {
            Surface = CommandSurface.Advanced,
            DefaultGesture = "Ctrl+Alt+Shift+G",
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is { } current
                && (t.Selected() is ObjectElement { Content: PulseOffice.Drawing.Shapes.ShapeGroupObject } || units.Matching(current, t.SelectedMany()) is not null),
            Execute = (_, _) =>
            {
                if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return Task.CompletedTask;
                if (units.Matching(slide, t.SelectedMany()) is { } read)
                {
                    var selection = t.SelectedMany().ToList();
                    t.Actions.ReplaceGroups(slide, editing.Dissolve(slide, read), "deck.action.ungroup");
                    t.Select(selection);
                }
                else if (t.Selected() is ObjectElement { Content: PulseOffice.Drawing.Shapes.ShapeGroupObject } group
                    && grouping.Ungroup(group, t.Deck.NextId) is { } children)
                    t.Actions.UngroupObject(slide, group, children);
                return Task.CompletedTask;
            },
        };

    private CommandDefinition EnterGroupCommand() =>
        Prototype("deck.object.enterGroup") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            DefaultGesture = "F3",
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is { } current
                && (t.Options.OpenGroup is null && t.Selected() is ObjectElement { Content: PulseOffice.Drawing.Shapes.ShapeGroupObject }
                    || units.Matching(current, t.SelectedMany()) is not null),
            Execute = (_, _) =>
            {
                if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return Task.CompletedTask;
                if (units.Matching(slide, t.SelectedMany()) is { } read)
                {
                    Open(t, slide, read);
                    return Task.CompletedTask;
                }
                if (t.Selected() is ObjectElement { Content: PulseOffice.Drawing.Shapes.ShapeGroupObject } group)
                {
                    var layout = decomposition.Decompose(group);
                    t.Actions.ConvertGroup(slide, group, layout);
                    if (slide.Groups.FirstOrDefault(candidate => candidate.Id == group.Id) is { } converted) Open(t, slide, converted);
                }
                return Task.CompletedTask;
            },
        };

    private void Open(DeckTarget t, Slide slide, SlideGroup group)
    {
        t.Options.OpenGroup = new OpenGroup(slide.Id, units.Leaves(slide, group)) { SlideGroupId = group.Id };
        t.Select([]);
    }

    private CommandDefinition LeaveGroupCommand() =>
        Prototype("deck.object.leaveGroup") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            DefaultGesture = "Ctrl+F3",
            EnabledWhen = _ => Context.Target is { } t && t.Options.OpenGroup is { } open
                && open.SlideId == t.CurrentSlide()?.Id && open.IsValidIn(t.Deck),
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.Options.OpenGroup is { } open) Leave(t, open);
                return Task.CompletedTask;
            },
        };

    private CommandDefinition ReverseCommand() =>
        Prototype("deck.object.reverse") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null
                && t.SelectedMany().Count >= 2,
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.CurrentSlide() is { } slide)
                    ReverseOrder(slide, t);
                return Task.CompletedTask;
            },
        };

    private void ReverseOrder(Slide slide, DeckTarget target)
    {
        var selected = target.SelectedMany().Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var ordered = slide.InZOrder().ToList();
        var slots = ordered
            .Select((element, index) => (element, index))
            .Where(x => selected.Contains(x.element.Id))
            .ToList();
        if (slots.Count < 2) return;

        var reversed = slots.Select(s => s.element).Reverse().ToList();
        for (var k = 0; k < slots.Count; k++)
            ordered[slots[k].index] = reversed[k];

        target.Actions.ReorderElements(slide, ordered.Select((e, i) => (e.Id, i)).ToList());
    }

    private CommandDefinition OrderCommand(string id, Order order) =>
        SelectionCommand(id, (slide, moving) =>
        {
            var ids = moving.Select(element => element.Id).ToHashSet(StringComparer.Ordinal);
            var ordered = slide.InZOrder().ToList();
            var reordered = order switch
            {
                Order.Front => [.. ordered.Where(e => !ids.Contains(e.Id)), .. ordered.Where(e => ids.Contains(e.Id))],
                Order.Back => [.. ordered.Where(e => ids.Contains(e.Id)), .. ordered.Where(e => !ids.Contains(e.Id))],
                Order.Forward => Step(ordered, ids, forward: true),
                _ => Step(ordered, ids, forward: false),
            };
            Context.Target!.Actions.ReorderElements(slide, [.. reordered.Select((e, i) => (e.Id, i))]);
        },
        enabled: (slide, moving) =>
        {
            var ordered = slide.InZOrder().ToList();
            var indices = moving.Select(element => ordered.FindIndex(e => e.Id == element.Id)).Where(index => index >= 0).ToList();
            if (indices.Count == 0) return false;
            return order switch
            {
                Order.Front or Order.Forward => indices.Min() < ordered.Count - indices.Count,
                _ => indices.Max() >= indices.Count,
            };
        });

    private List<SlideElement> Step(List<SlideElement> ordered, IReadOnlySet<string> moving, bool forward)
    {
        var result = new List<SlideElement>(ordered);
        var indices = Enumerable.Range(0, result.Count).Where(index => moving.Contains(result[index].Id));
        foreach (var index in forward ? indices.Reverse().ToList() : indices.ToList())
        {
            var swap = forward ? index + 1 : index - 1;
            if (swap < 0 || swap >= result.Count || moving.Contains(result[swap].Id)) continue;
            (result[index], result[swap]) = (result[swap], result[index]);
        }

        return result;
    }

    private CommandDefinition AlignCommand(string id, Align align) =>
        SelectionCommand(id, (slide, elements) =>
        {
            var size = Context.Target!.Deck.Size;
            var (left, top, right, bottom) = elements.Count > 1
                ? (elements.Min(e => e.Bounds.Left.Value), elements.Min(e => e.Bounds.Top.Value),
                   elements.Max(e => e.Bounds.Left.Value + e.Bounds.Width.Value), elements.Max(e => e.Bounds.Top.Value + e.Bounds.Height.Value))
                : (0, 0, size.Width.Value, size.Height.Value);

            Context.Target!.Actions.AlignElements(slide, [.. elements.Select(element =>
            {
                var b = element.Bounds;
                var (x, y) = align switch
                {
                    Align.Left => (left, b.Top.Value),
                    Align.CenterH => (left + (right - left - b.Width.Value) / 2, b.Top.Value),
                    Align.Right => (right - b.Width.Value, b.Top.Value),
                    Align.Top => (b.Left.Value, top),
                    Align.MiddleV => (b.Left.Value, top + (bottom - top - b.Height.Value) / 2),
                    Align.Bottom => (b.Left.Value, bottom - b.Height.Value),
                    _ => (b.Left.Value, b.Top.Value),
                };
                return (element.Id, new DocumentRect(new Twip(x), new Twip(y), b.Width, b.Height));
            })]);
        });

    private CommandDefinition SelectionCommand(
        string id,
        Action<Slide, IReadOnlyList<SlideElement>> run,
        Func<Slide, IReadOnlyList<SlideElement>, bool>? enabled = null) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t
                && t.CurrentSlide() is { } slide
                && Selection(t) is { Count: > 0 } elements
                && (enabled?.Invoke(slide, elements) ?? true),
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.CurrentSlide() is { } slide && Selection(t) is { Count: > 0 } elements)
                    run(slide, elements);
                return Task.CompletedTask;
            },
        };

    private IReadOnlyList<SlideElement> Selection(DeckTarget target) =>
        target.SelectedMany() is { Count: > 0 } many ? many : target.Selected() is { } single ? [single] : [];

    private CommandDefinition RotateCommand(string id, double delta) =>
        ObjectCommand(id, (slide, element) =>
        {
            var target = Context.Target!;
            if (units.Matching(slide, target.SelectedMany()) is { } group && editing.Frame(slide, group) is { } frame)
            {
                target.Actions.TransformGroup(slide, group, frame.Bounds, ((frame.RotationDegrees + delta) % 360 + 360) % 360);
                target.Select([.. units.Leaves(slide, group).Select(slide.Find).OfType<SlideElement>()]);
                return;
            }
            var rotation = ((element.RotationDegrees + delta) % 360 + 360) % 360;
            Context.Target!.Actions.TransformElement(slide, element.Id, element.Bounds, rotation);
        });

    private CommandDefinition ObjectCommand(
        string id,
        Action<Slide, SlideElement> run,
        Func<Slide, SlideElement, bool>? enabled = null) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t
                && t.CurrentSlide() is { } slide
                && t.Selected() is { } element
                && (enabled?.Invoke(slide, element) ?? true),
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.CurrentSlide() is { } slide && t.Selected() is { } element)
                    run(slide, element);
                return Task.CompletedTask;
            },
        };
}
