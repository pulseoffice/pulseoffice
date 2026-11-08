using PulseOffice.Document.Commands;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public sealed class DeckElementActions(Presentation deck, ITextCatalog texts, DeckActionRecorder recorder, ISlideGroupEditing _groupEditing, PulseOffice.Drawing.Shapes.IContentFlipping _flipping, IFreeformRefit _refit, PulseOffice.Document.Forms.IOptionGroups _optionGroups, IElementBehaviourCloning _behaviourCloning)
{
    public void InsertMedia(Slide slide, MediaElement media)
    {
        var top = Top(slide);
        if (media.ZOrder < top) media = media with { ZOrder = top };
        Do(texts["deck.action.insertMedia"],
            () => { slide.Add(media); deck.Raise(); },
            () => { slide.Remove(media.Id); deck.Raise(); });
    }

    public void ReplaceElementsWith(Slide slide, IReadOnlyList<string> removed, SlideElement added, string labelKey)
    {
        var before = removed.Select(slide.Find).OfType<SlideElement>().ToList();
        if (before.Count == 0) return;
        var behaviours = new ElementBehaviours(slide, before.Select(e => e.Id).Where(id => id != added.Id));
        Do(texts[labelKey],
            () => { foreach (var element in before) slide.Remove(element.Id); behaviours.Detach(); slide.Add(added); deck.Raise(); },
            () => { slide.Remove(added.Id); foreach (var element in before) slide.Add(element); behaviours.Attach(); deck.Raise(); });
    }

    public void AddFormFields(Slide slide, IReadOnlyList<ObjectElement> elements, PulseOffice.Document.Forms.FormDocumentSettings settings)
    {
        var before = deck.FormSettings;
        Do(texts["deck.action.addField"],
            () => { foreach (var element in elements) slide.Add(element); deck.FormSettings = settings; deck.Raise(); },
            () => { foreach (var element in elements) slide.Remove(element.Id); deck.FormSettings = before; deck.Raise(); });
    }

    public void InsertObject(Slide slide, ObjectElement element)
    {
        var top = Top(slide);
        if (element.ZOrder < top) element = element with { ZOrder = top };
        Do(texts["deck.action.insertObject"],
            () => { slide.Add(element); deck.Raise(); },
            () => { slide.Remove(element.Id); deck.Raise(); });
    }

    public void EditTable(Slide slide, string elementId, Func<PulseOffice.Document.Embedding.DataTableObject, PulseOffice.Document.Embedding.DataTableObject> change)
    {
        if (slide.Find(elementId) is not ObjectElement { Content: PulseOffice.Document.Embedding.DataTableObject table } element) return;

        var changed = change(table);
        if (ReferenceEquals(changed, table)) return;
        var before = element;
        var after = element with { Content = changed };

        Do(texts["deck.action.editTable"],
            () => { slide.Replace(after); deck.Raise(); },
            () => { slide.Replace(before); deck.Raise(); });
    }

    public void ReplaceTable(Slide slide, string elementId,
        PulseOffice.Document.Embedding.DataSnapshot snapshot, IReadOnlyList<PulseOffice.Document.Embedding.TableMerge> merges,
        IReadOnlyList<PulseOffice.Document.Embedding.TableCellFormat> cellFormats)
    {
        if (slide.Find(elementId) is not ObjectElement { Content: PulseOffice.Document.Embedding.DataTableObject table } element) return;

        var before = element;
        var after = element with { Content = table with { Snapshot = snapshot, Merges = merges, CellFormats = cellFormats } };

        Do(texts["deck.action.editTable"],
            () => { slide.Replace(after); deck.Raise(); },
            () => { slide.Replace(before); deck.Raise(); });
    }

    public void InsertElement(Slide slide, SlideElement element)
    {
        var top = Top(slide);
        if (element.ZOrder < top) element = element with { ZOrder = top };
        element = Fitted(slide, element);
        Do(texts["deck.action.insertObject"],
            () => { slide.Add(element); deck.Raise(); },
            () => { slide.Remove(element.Id); deck.Raise(); });
    }

    public void DeleteObject(Slide slide, SlideElement element)
    {
        var behaviours = new ElementBehaviours(slide, [element.Id]);
        Do(texts["deck.action.deleteObject"],
            () => { slide.Remove(element.Id); behaviours.Detach(); deck.Raise(); },
            () => { slide.Add(element); behaviours.Attach(); deck.Raise(); });
    }

    public void DeleteElements(Slide slide, IReadOnlyList<SlideElement> elements, string labelKey)
    {
        if (elements.Count == 0) return;
        var removed = elements.Where(e => slide.Find(e.Id) is not null).ToList();
        if (removed.Count == 0) return;

        var behaviours = new ElementBehaviours(slide, removed.Select(e => e.Id));
        Do(texts[labelKey],
            () => { foreach (var e in removed) slide.Remove(e.Id); behaviours.Detach(); deck.Raise(); },
            () => { foreach (var e in removed) slide.Add(e); behaviours.Attach(); deck.Raise(); });
    }

    public IReadOnlyList<SlideElement> AddClones(
        Slide slide, IReadOnlyList<SlideElement> sources, int dx, int dy, string labelKey, IReadOnlyList<SlideGroup>? groups = null,
        ElementBehaviourSet? behaviours = null)
    {
        if (sources.Count == 0 || IsReadOnly) return [];

        var baseZ = slide.Elements.Count == 0 ? 0 : slide.Elements.Max(e => e.ZOrder);
        var reided = sources.Select((source, i) => Reid(source, deck.NextId(), dx, dy, baseZ + i + 1)).ToList();
        var remap = new ElementIdRemap(sources.Select((source, i) => (source.Id, reided[i].Id))
            .GroupBy(pair => pair.Item1, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Item2, StringComparer.Ordinal));
        var clones = reided.Select(remap.Apply).ToList();
        var ids = sources.Select((source, i) => (source.Id, reided[i].Id))
            .GroupBy(pair => pair.Item1, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Item2, StringComparer.Ordinal);
        var cloned = _groupEditing.Renamed(_groupEditing.Within(groups ?? [], ids.Keys), ids, deck.NextId);
        var groupsBefore = slide.Groups.ToList();
        var groupsAfter = groupsBefore.Concat(cloned).ToList();
        var carried = behaviours is { IsEmpty: false } ? _behaviourCloning.Cloned(deck, slide, behaviours, ids) : null;

        Do(texts[labelKey],
            () =>
            {
                foreach (var clone in clones) slide.Add(clone);
                slide.ReplaceGroups(groupsAfter);
                if (carried is not null) Carry(slide, carried);
                deck.Raise();
            },
            () =>
            {
                if (carried is not null) Uncarry(slide, carried);
                foreach (var clone in clones) slide.Remove(clone.Id);
                slide.ReplaceGroups(groupsBefore);
                deck.Raise();
            });

        return clones;
    }

    public ElementBehaviourSet BehavioursOf(Slide slide, IReadOnlyList<SlideElement> elements) =>
        _behaviourCloning.Of(deck, slide, elements.Select(element => element.Id));

    private void Carry(Slide slide, ElementBehaviourSet carried)
    {
        foreach (var effect in carried.Animations) slide.AddAnimation(effect);
        foreach (var action in carried.Interactions) slide.SetInteraction(action);
        foreach (var sound in carried.Sounds) deck.Sounds.Add(sound);
    }

    private void Uncarry(Slide slide, ElementBehaviourSet carried)
    {
        foreach (var effect in carried.Animations) slide.RemoveAnimation(effect.Id);
        foreach (var action in carried.Interactions) slide.RemoveInteraction(action.TargetId);
        foreach (var sound in carried.Sounds) deck.Sounds.Remove(sound.Path);
    }

    private SlideElement Reid(SlideElement source, string id, int dx, int dy, int z)
    {
        var b = source.Bounds;
        var moved = source with
        {
            Id = id,
            ZOrder = z,
            Bounds = new DocumentRect(b.Left + new Twip(dx), b.Top + new Twip(dy), b.Width, b.Height),
        };
        return moved is ObjectElement placed ? placed with { Content = placed.Content with { Id = id } } : moved;
    }

    public void EditImage(Slide slide, string elementId,
        Func<PulseOffice.Drawing.Assets.RasterImageObject, PulseOffice.Drawing.Assets.RasterImageObject> change, string labelKey)
    {
        if (deck.IndexOf(slide) < 0) return;
        if (slide.Find(elementId) is not ObjectElement { Content: PulseOffice.Drawing.Assets.RasterImageObject image } element) return;

        var before = element;
        var after = element with { Content = change(image) };
        if (after == before) return;

        Do(texts[labelKey],
            () => { slide.Replace(after); deck.Raise(); },
            () => { slide.Replace(before); deck.Raise(); });
    }

    public void FlipElement(Slide slide, string elementId, bool horizontal)
    {
        if (slide.Find(elementId) is not ObjectElement element) return;
        if (Flip(element.Content, horizontal) is not { } flipped) return;

        var before = element;
        var after = element with { Content = flipped };

        Do(texts[horizontal ? "deck.action.flipHorizontal" : "deck.action.flipVertical"],
            () => { slide.Replace(after); deck.Raise(); },
            () => { slide.Replace(before); deck.Raise(); });
    }

    private PulseOffice.Document.Embedding.EmbeddedObject? Flip(
        PulseOffice.Document.Embedding.EmbeddedObject content, bool horizontal) =>
        _flipping.Flips(content) ? _flipping.Flip(content, horizontal) : null;

    public void ReplaceGroups(Slide slide, IReadOnlyList<SlideGroup> groups, string labelKey)
    {
        var before = slide.Groups.ToList();
        if (before.SequenceEqual(groups)) return;
        Do(texts[labelKey],
            () => { slide.ReplaceGroups(groups); deck.Raise(); },
            () => { slide.ReplaceGroups(before); deck.Raise(); });
    }

    public void TransformGroup(Slide slide, SlideGroup group, DocumentRect bounds, double rotation)
    {
        if (_groupEditing.Transform(slide, group, bounds, rotation) is not { } transform) return;
        var swaps = transform.Elements
            .Select(element => (Before: slide.Find(element.Id), After: Fitted(slide, element)))
            .Where(swap => swap.Before is not null)
            .ToList();
        var groupsBefore = slide.Groups.ToList();
        Do(texts["deck.action.transform"],
            () => { foreach (var swap in swaps) slide.Replace(swap.After); slide.ReplaceGroups(transform.Groups); deck.Raise(); },
            () => { foreach (var swap in swaps) slide.Replace(swap.Before!); slide.ReplaceGroups(groupsBefore); deck.Raise(); });
    }

    public void ConvertGroup(Slide slide, ObjectElement group, SlideGroupLayout layout)
    {
        var groupsBefore = slide.Groups.ToList();
        var groupsAfter = groupsBefore.Concat(layout.Groups).ToList();
        Do(texts["deck.action.group"],
            () =>
            {
                slide.Remove(group.Id);
                foreach (var leaf in layout.Leaves) slide.Add(leaf);
                slide.ReplaceGroups(groupsAfter);
                deck.Raise();
            },
            () =>
            {
                foreach (var leaf in layout.Leaves) slide.Remove(leaf.Id);
                slide.Add(group);
                slide.ReplaceGroups(groupsBefore);
                deck.Raise();
            });
    }

    public void UngroupObject(Slide slide, ObjectElement group, IReadOnlyList<ObjectElement> children)
    {
        Do(texts["deck.action.ungroup"],
            () =>
            {
                slide.Remove(group.Id);
                foreach (var child in children) slide.Add(child);
                deck.Raise();
            },
            () =>
            {
                foreach (var child in children) slide.Remove(child.Id);
                slide.Add(group);
                deck.Raise();
            });
    }

    public void TransformElement(Slide slide, string elementId, DocumentRect bounds, double rotation)
    {
        if (slide.Find(elementId) is not { } current) return;
        var before = current;
        var after = Fitted(slide, current with { Bounds = bounds, RotationDegrees = rotation });

        Do(texts["deck.action.transform"],
            () => { slide.Replace(after); deck.Raise(); },
            () => { slide.Replace(before); deck.Raise(); });
    }

    public void ReplaceElements(Slide slide, IReadOnlyList<SlideElement> updated, string labelKey)
    {
        var swaps = updated
            .Select(element => (Before: slide.Find(element.Id), After: Fitted(slide, element)))
            .Where(swap => swap.Before is not null && !Equals(swap.Before, swap.After))
            .ToList();
        if (swaps.Count == 0) return;

        Do(texts[labelKey],
            () => { foreach (var swap in swaps) slide.Replace(swap.After); deck.Raise(); },
            () => { foreach (var swap in swaps) slide.Replace(swap.Before!); deck.Raise(); });
    }

    public void EditFreeformPath(Slide slide, ObjectElement element, PulseOffice.Drawing.Freeform.FreeformPath path)
    {
        if (element.Content is not PulseOffice.Drawing.Freeform.FreeformObject freeform) return;
        ReplaceElement(slide, _refit.Refit(element, freeform, path), "deck.action.editPoints");
    }

    public void AdjustShape(Slide slide, ObjectElement element, PulseOffice.Drawing.Shapes.ShapeObject shape)
    {
        if (element.Content is not PulseOffice.Drawing.Shapes.ShapeObject) return;
        ReplaceElement(slide, element with { Content = shape }, "deck.action.adjustShape");
    }

    public void EditShapeText(Slide slide, ObjectElement element, PulseOffice.Drawing.Shapes.ShapeObject shape, bool format)
    {
        if (element.Content is not PulseOffice.Drawing.Shapes.ShapeObject) return;
        ReplaceElement(slide, element with { Content = shape }, format ? "deck.action.shapeTextFormat" : "deck.action.shapeText");
    }

    public void ChangeControl(Slide slide, ObjectElement updated)
    {
        var elements = slide.Elements.ToList();
        var controls = elements
            .Select(element => element.Id == updated.Id ? updated.Content : (element as ObjectElement)?.Content)
            .Select(content => content as PulseOffice.Document.Forms.FormControlObject)
            .ToList();
        var chosen = elements.FindIndex(element => element.Id == updated.Id);
        if (chosen < 0) return;

        var changed = new List<SlideElement> { updated };
        foreach (var release in _optionGroups.Release(controls, chosen))
            if (elements[release.Index] is ObjectElement { Content: PulseOffice.Document.Forms.FormControlObject form } sibling)
                changed.Add(sibling with { Content = form with { Properties = release.Released } });

        ReplaceElements(slide, changed, "deck.action.control");
    }

    public void ReplaceElement(Slide slide, SlideElement updated, string labelKey)
    {
        if (slide.Find(updated.Id) is not { } before) return;
        updated = Fitted(slide, updated);
        if (Equals(updated, before)) return;

        Do(texts[labelKey],
            () => { slide.Replace(updated); deck.Raise(); },
            () => { slide.Replace(before); deck.Raise(); });
    }

    public void ReorderElements(Slide slide, IReadOnlyList<(string Id, int ZOrder)> order)
    {
        var before = slide.Elements.ToDictionary(e => e.Id, e => e.ZOrder);
        var after = order.ToDictionary(o => o.Id, o => o.ZOrder);

        Do(texts["deck.action.reorder"],
            () => { Apply(slide, after); deck.Raise(); },
            () => { Apply(slide, before); deck.Raise(); });
    }

    public void AlignElements(Slide slide, IReadOnlyList<(string Id, DocumentRect Bounds)> placements)
    {
        var before = placements
            .Select(p => (p.Id, slide.Find(p.Id)?.Bounds))
            .Where(p => p.Item2 is not null)
            .Select(p => (p.Id, Bounds: p.Item2!.Value))
            .ToList();

        Do(texts["deck.action.align"],
            () => { PlaceAll(slide, placements); deck.Raise(); },
            () => { PlaceAll(slide, before); deck.Raise(); });
    }

    private void Apply(Slide slide, IReadOnlyDictionary<string, int> order)
    {
        foreach (var element in slide.Elements.ToList())
            if (order.TryGetValue(element.Id, out var z) && z != element.ZOrder)
                slide.Replace(element with { ZOrder = z });
    }

    private void PlaceAll(Slide slide, IReadOnlyList<(string Id, DocumentRect Bounds)> placements)
    {
        foreach (var (id, bounds) in placements)
            if (slide.Find(id) is { } element)
                slide.Replace(element with { Bounds = bounds });
    }

    public void SetInteraction(Slide slide, string elementId, InteractiveAction? action)
    {
        var before = slide.Interaction(elementId);

        Do(texts["deck.action.interaction"],
            () =>
            {
                if (action is null) slide.RemoveInteraction(elementId);
                else slide.SetInteraction(action);
                deck.Raise();
            },
            () =>
            {
                if (before is null) slide.RemoveInteraction(elementId);
                else slide.SetInteraction(before);
                deck.Raise();
            });
    }

    private int Top(Slide slide) => slide.Elements.Count == 0 ? 0 : slide.Elements.Max(other => other.ZOrder) + 1;

    private bool IsReadOnly => recorder.IsReadOnly;

    private void Do(string description, Action redo, Action undo) => recorder.Do(description, redo, undo);

    private T Fitted<T>(Slide slide, T element) where T : SlideElement => recorder.Fitted(slide, element);
}
