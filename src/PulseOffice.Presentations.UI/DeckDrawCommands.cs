using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;

namespace PulseOffice.Presentations.UI;

public sealed class DeckDrawCommands(IActiveDeckContext context, IDeckDrawingState drawing, IPathEditing editing) : DeckCommandSet(context)
{
    internal static readonly IReadOnlyList<(string Id, DeckPen Pen, bool Filled, string IconKey)> Pens =
    [
        ("polygon", DeckPen.Polygon, false, "folio.draw.polygon"),
        ("polygonFilled", DeckPen.Polygon, true, "folio.draw.polygonFilled"),
        ("polygon45", DeckPen.PolygonConstrained, false, "folio.draw.polygon45"),
        ("curve", DeckPen.Curve, false, "folio.draw.curve"),
        ("curveFilled", DeckPen.Curve, true, "folio.draw.curveFilled"),
        ("freehand", DeckPen.Freehand, false, "folio.draw.freeform"),
        ("freehandFilled", DeckPen.Freehand, true, "folio.draw.freeformFilled"),
    ];

    public IEnumerable<CommandDefinition> GetCommands()
    {
        foreach (var entry in Pens)
            yield return Prototype($"deck.draw.{entry.Id}") with
            {
                Surface = CommandSurface.Palette,
                RequiredState = DocumentState.DocumentOpen,
                ForbiddenState = DocumentState.ReadOnly,
                EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
                CheckedWhen = _ => drawing.Pen == entry.Pen && drawing.Filled == entry.Filled,
                Execute = (_, _) => { drawing.Choose(entry.Pen, entry.Filled); return Task.CompletedTask; },
            };

        yield return Prototype("deck.draw.editPoints") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => drawing.EditingPoints || Freeform() is not null,
            CheckedWhen = _ => drawing.EditingPoints,
            Execute = (_, _) => { drawing.ToggleEditingPoints(); return Task.CompletedTask; },
        };

        yield return Prototype("deck.draw.gluePoints") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => drawing.GluePoints || (Context.Target?.CurrentSlide()?.Elements.OfType<ObjectElement>().Count() ?? 0) >= 2,
            CheckedWhen = _ => drawing.GluePoints,
            Execute = (_, _) => { drawing.ToggleGluePoints(); return Task.CompletedTask; },
        };

        yield return Point("deck.draw.point.insert", (path, handle) =>
        {
            var nodes = path.Figures[handle.Figure].Nodes;
            var next = nodes[(handle.Node + 1) % nodes.Count].Point;
            var here = nodes[handle.Node].Point;
            return editing.InsertNode(path, handle.Figure, handle.Node, new ShapePoint((here.X + next.X) / 2, (here.Y + next.Y) / 2));
        });
        yield return Point("deck.draw.point.delete", (path, handle) => editing.DeleteNode(path, handle with { Part = PathHandlePart.Node }));
        yield return Point("deck.draw.point.corner", (path, handle) => editing.SetKind(path, handle, PathNodeKind.Corner));
        yield return Point("deck.draw.point.smooth", (path, handle) => editing.SetKind(path, handle, PathNodeKind.Smooth));
        yield return Point("deck.draw.point.symmetric", (path, handle) => editing.SetKind(path, handle, PathNodeKind.Symmetric));
        yield return Point("deck.draw.point.close", (path, handle) => editing.ToggleClosed(path, handle.Figure));
    }

    private (Slide Slide, ObjectElement Element, FreeformObject Content)? Freeform() =>
        Context.Target is { } t && t.CurrentSlide() is { } slide && t.Selected() is ObjectElement { Content: FreeformObject freeform } element
            ? (slide, element, freeform)
            : null;

    private CommandDefinition Point(string id, Func<FreeformPath, PathHandle, FreeformPath> change) => Prototype(id) with
    {
        Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
        RequiredState = DocumentState.DocumentOpen,
        ForbiddenState = DocumentState.ReadOnly,
        EnabledWhen = _ => drawing.EditingPoints && drawing.Handle.Exists && Freeform() is not null,
        Execute = (_, _) =>
        {
            if (Freeform() is not { } target || Context.Target is not { } t) return Task.CompletedTask;
            var handle = drawing.Handle;
            if (handle.Figure >= target.Content.Path.Figures.Count) return Task.CompletedTask;
            var path = change(target.Content.Path, handle);
            t.Actions.EditFreeformPath(target.Slide, target.Element, path);
            drawing.Handle = PathHandle.None;
            return Task.CompletedTask;
        },
    };
}
