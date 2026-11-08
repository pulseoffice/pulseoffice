using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Presentations;

public interface IGraphicStyleRelinker
{
    EmbeddedObject? Relink(EmbeddedObject content, Func<EmbeddedStyled, EmbeddedStyled?> relink);

    IReadOnlyList<SlideElement>? Relink(IReadOnlyList<SlideElement> elements, Func<EmbeddedStyled, EmbeddedStyled?> relink);

    SlideMaster? Relink(SlideMaster master, Func<EmbeddedStyled, EmbeddedStyled?> relink);

    void Relink(Presentation deck, Func<EmbeddedStyled, EmbeddedStyled?> relink);
}

public sealed class GraphicStyleRelinker : IGraphicStyleRelinker
{
    private const int MaximumDepth = 64;

    public EmbeddedObject? Relink(EmbeddedObject content, Func<EmbeddedStyled, EmbeddedStyled?> relink)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(relink);
        return Relink(content, relink, 0);
    }

    private EmbeddedObject? Relink(EmbeddedObject content, Func<EmbeddedStyled, EmbeddedStyled?> relink, int depth)
    {
        if (content is ShapeGroupObject group)
        {
            if (depth >= MaximumDepth) return null;
            var changed = false;
            var children = new List<GroupChild>(group.Children.Count);
            foreach (var child in group.Children)
            {
                if (Relink(child.Content, relink, depth + 1) is { } updated)
                {
                    children.Add(child with { Content = updated });
                    changed = true;
                }
                else children.Add(child);
            }
            return changed ? group with { Children = children } : null;
        }
        return EmbeddedStyled.Of(content) is { } styled && relink(styled) is { } result && !ReferenceEquals(result.Content, content) ? result.Content : null;
    }

    public IReadOnlyList<SlideElement>? Relink(IReadOnlyList<SlideElement> elements, Func<EmbeddedStyled, EmbeddedStyled?> relink)
    {
        ArgumentNullException.ThrowIfNull(elements);
        ArgumentNullException.ThrowIfNull(relink);
        var changed = false;
        var result = new List<SlideElement>(elements.Count);
        foreach (var element in elements)
        {
            if (element is ObjectElement placed && Relink(placed.Content, relink, 0) is { } updated)
            {
                result.Add(placed with { Content = updated });
                changed = true;
            }
            else result.Add(element);
        }
        return changed ? result : null;
    }

    public SlideMaster? Relink(SlideMaster master, Func<EmbeddedStyled, EmbeddedStyled?> relink)
    {
        ArgumentNullException.ThrowIfNull(master);
        var objects = Relink(master.Objects, relink);
        var changed = objects is not null;
        var layouts = new List<SlideLayoutTemplate>(master.Layouts.Count);
        foreach (var layout in master.Layouts)
        {
            if (Relink(layout.Objects, relink) is { } updated)
            {
                layouts.Add(layout with { Objects = updated });
                changed = true;
            }
            else layouts.Add(layout);
        }
        return changed ? master with { Objects = objects ?? master.Objects, Layouts = layouts } : null;
    }

    public void Relink(Presentation deck, Func<EmbeddedStyled, EmbeddedStyled?> relink)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(relink);
        foreach (var slide in deck.Slides)
            foreach (var element in slide.Elements.OfType<ObjectElement>().ToList())
                if (Relink(element.Content, relink, 0) is { } updated)
                    slide.Replace(element with { Content = updated });
        for (var index = 0; index < deck.Masters.Count; index++)
            if (Relink(deck.Masters[index], relink) is { } master)
            {
                if (index == 0) deck.Master = master;
                else deck.ReplaceMaster(master);
            }
    }
}
