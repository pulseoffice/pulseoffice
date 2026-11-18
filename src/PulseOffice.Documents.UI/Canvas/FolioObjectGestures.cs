using PulseOffice.Foundation.Zoom;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Layout.Queries;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.UI.Controls.Text.Rich;

namespace PulseOffice.Documents.UI.Canvas;

internal sealed class FolioObjectGestures(FolioCanvas canvas, FolioEditor editor, FolioCanvasServices services)
{
    private sealed record ObjectGesture(PulseOffice.Drawing.Shapes.ObjectTransformSession Session, Point Origin, PageSlot Slot, bool Moved = false)
    {
        public PulseOffice.Drawing.Shapes.ObjectHandle Handle => Session.Handle;

        public PulseOffice.Drawing.Shapes.ObjectBox Preview => Session.Preview;

        public double Rotation => Session.Rotation;
    }

    internal sealed record ObjectHit(ObjectBox Box, PageSlot Slot);

    private ObjectGesture? _gesture;

    public FolioObjectLinkTarget? PointMember(ObjectHit hit, Point point) =>
        services.ObjectLinks.Point(editor, hit.Box, (point.X - hit.Slot.X) / canvas.Scale, (point.Y - hit.Slot.Y) / canvas.Scale);

    public FolioObjectLinkTarget? LocateMember(ObjectHit hit, Point point) =>
        services.ObjectLinks.Locate(editor, hit.Box, (point.X - hit.Slot.X) / canvas.Scale, (point.Y - hit.Slot.Y) / canvas.Scale);

    public ObjectHit? ObjectAt(Point point)
    {
        var layout = editor.DisplayLayout;
        if (canvas.SlotAt(point) is not { } slot || slot.Index >= layout.Pages.Count) return null;
        return services.Objects.At(layout.Pages[slot.Index], (point.X - slot.X) / canvas.Scale, (point.Y - slot.Y) / canvas.Scale) is { } box ? new ObjectHit(box, slot) : null;
    }

    private sealed record AdjustGesture(PulseOffice.UI.Controls.Shapes.AdjustHandleGrip Grip, PulseOffice.UI.Controls.Shapes.AdjustFrame Frame, PageSlot Slot);

    private AdjustGesture? _adjust;

    private (PulseOffice.Drawing.Shapes.ShapeObject Shape, PulseOffice.UI.Controls.Shapes.AdjustFrame Frame, PageSlot Slot)? Adjustable()
    {
        if (editor.ReadOnly() || editor.SelectedObjectIds.Count > 1 || editor.SelectedObjectId is not { } id
            || editor.Document.Objects.Find(id) is not { Content: PulseOffice.Documents.Objects.EmbeddedContent { Value: PulseOffice.Drawing.Shapes.ShapeObject shape } } item
            || editor.Objects.Locked(item))
            return null;
        if (services.Objects.Find(editor.DisplayLayout, id, out var index) is not { } box || index >= canvas.Slots.Count) return null;
        return (shape, new PulseOffice.UI.Controls.Shapes.AdjustFrame(new Rect(box.Bounds.X, box.Bounds.Y, box.Bounds.Width, box.Bounds.Height), item.RotationDegrees, item.FlipHorizontal, item.FlipVertical), canvas.Slots[index]);
    }

    private double AdjustSize => (services.Manipulator.HandleSize / 2 + 1) / canvas.Scale;

    public bool BeginAdjust(Point point, int clicks)
    {
        if (clicks > 1 || Adjustable() is not { } adjustable) return false;
        var local = new Point((point.X - adjustable.Slot.X) / canvas.Scale, (point.Y - adjustable.Slot.Y) / canvas.Scale);
        if (services.Adjusting.Grip(adjustable.Shape, adjustable.Frame, local, AdjustSize + 1 / canvas.Scale) is not { } grip) return false;
        _adjust = new AdjustGesture(grip, adjustable.Frame, adjustable.Slot);
        return true;
    }

    public void PaintAdjustHandles(DrawingContext context, PageBox page)
    {
        if (_gesture is not null || Adjustable() is not { } adjustable || adjustable.Slot.Index != page.Index) return;
        if (_adjust is { } adjusting) services.Adjusting.DrawPreview(context, adjusting.Grip, adjusting.Frame, AdjustSize, 1 / canvas.Scale);
        else services.Adjusting.Draw(context, adjustable.Shape, adjustable.Frame, AdjustSize);
    }

    public bool BeginObjectGesture(Point point, int clicks)
    {
        if (clicks > 1 || editor.ReadOnly() || editor.SelectedObjectId is not { } id || editor.Document.Objects.Find(id) is not { } item) return false;
        if (services.Objects.Find(editor.DisplayLayout, id, out var index) is not { } box || index >= canvas.Slots.Count) return false;
        var slot = canvas.Slots[index];
        var area = new PulseOffice.Drawing.Shapes.ObjectBox(box.Bounds.X, box.Bounds.Y, box.Bounds.Width, box.Bounds.Height);
        var handle = services.Frame.HandleAt(new PulseOffice.UI.Controls.Shapes.FrameScene(area, item.RotationDegrees, Brushes.Transparent, 1 / canvas.Scale), new Point((point.X - slot.X) / canvas.Scale, (point.Y - slot.Y) / canvas.Scale));
        if (handle == PulseOffice.Drawing.Shapes.ObjectHandle.None) return false;
        _gesture = new ObjectGesture(services.Manipulator.Begin(handle, area, item.RotationDegrees), point, slot);
        return true;
    }

    private void Commit(ObjectGesture gesture, Point release)
    {
        if (editor.SelectedObjectId is not { } id) return;
        switch (gesture.Handle)
        {
            case PulseOffice.Drawing.Shapes.ObjectHandle.Body:
                var target = canvas.SlotAt(release) ?? gesture.Slot;
                var shiftX = (gesture.Slot.X - target.X) / canvas.Scale;
                var shiftY = (gesture.Slot.Y - target.Y) / canvas.Scale;
                services.Mover.Move(editor, id, new ObjectDrop(target.Index, gesture.Preview.X + shiftX, gesture.Preview.Y + shiftY,
                    (release.X - target.X) / canvas.Scale, (release.Y - target.Y) / canvas.Scale));
                break;
            case PulseOffice.Drawing.Shapes.ObjectHandle.Rotate:
                editor.Objects.Rotate(gesture.Rotation);
                break;
            default:
                editor.Objects.Resize(new PulseOffice.Foundation.Units.DocumentSize(
                    PulseOffice.Foundation.Units.Twip.FromPoints(Math.Max(1, gesture.Preview.Width)), PulseOffice.Foundation.Units.Twip.FromPoints(Math.Max(1, gesture.Preview.Height))));
                break;
        }
    }

    private PulseOffice.Drawing.Shapes.ObjectBox Snap(PulseOffice.Drawing.Shapes.ObjectTransformSession session, int page)
    {
        var view = services.ViewLayout;
        var pages = editor.DisplayLayout.Pages;
        var step = view.GridSpacing;
        if (!view.SnapToGrid || page >= pages.Count || step <= 0) return session.Preview;
        var sheet = pages[page];
        var content = sheet.Content;
        var left = content.X - Math.Ceiling(content.X / step) * step;
        var top = content.Y - Math.Ceiling(content.Y / step) * step;
        var lines = services.Snapping.Lattice(left, top, sheet.Width, sheet.Height, step);
        return services.Snapping.Snap(session, lines, step / 2);
    }

    public void PaintOtherSelections(DrawingContext context, PageBox page, IBrush? brush)
    {
        var accent = brush ?? Brushes.DodgerBlue;
        foreach (var id in editor.SelectedObjectIds.Skip(1))
            if (services.Objects.Find(editor.DisplayLayout, id, out var index) is { } box && index == page.Index)
                services.Frame.DrawSecondary(context, new PulseOffice.UI.Controls.Shapes.FrameScene(
                    new PulseOffice.Drawing.Shapes.ObjectBox(box.Bounds.X, box.Bounds.Y, box.Bounds.Width, box.Bounds.Height), box.RotationDegrees, accent, 1 / canvas.Scale));
    }

    public void PaintObjectSelection(DrawingContext context, PageBox page, IBrush? accent)
    {
        if (editor.SelectedObjectId is not { } id || services.Objects.Find(editor.DisplayLayout, id, out var index) is not { } box || index != page.Index) return;
        var area = _gesture?.Preview ?? new PulseOffice.Drawing.Shapes.ObjectBox(box.Bounds.X, box.Bounds.Y, box.Bounds.Width, box.Bounds.Height);
        var rotation = _gesture?.Rotation ?? box.RotationDegrees;
        services.Frame.Draw(context, new PulseOffice.UI.Controls.Shapes.FrameScene(area, rotation, accent ?? Brushes.DodgerBlue, 1 / canvas.Scale, !editor.ReadOnly()));
    }

    public PulseOffice.Drawing.Shapes.ObjectBox? Helpline(int page) =>
        _gesture is { Moved: true } moved && moved.Slot.Index == page ? moved.Preview : null;

    public bool Drag(Point at, bool shift)
    {
        if (_adjust is { } adjusting)
        {
            _adjust = adjusting with
            {
                Grip = services.Adjusting.Drag(adjusting.Grip, adjusting.Frame,
                    new Point((at.X - adjusting.Slot.X) / canvas.Scale, (at.Y - adjusting.Slot.Y) / canvas.Scale)),
            };
            return true;
        }
        if (_gesture is { } gesture)
        {
            var dx = (at.X - gesture.Origin.X) / canvas.Scale;
            var dy = (at.Y - gesture.Origin.Y) / canvas.Scale;
            var session = gesture.Session;
            session.Track(dx, dy, (at.X - gesture.Slot.X) / canvas.Scale, (at.Y - gesture.Slot.Y) / canvas.Scale, shift);
            session.Settle(Snap(session, gesture.Slot.Index));
            _gesture = gesture with { Moved = gesture.Moved || !session.Moving || Math.Abs(dx) + Math.Abs(dy) > 2 / canvas.Scale };
            return true;
        }
        return false;
    }

    public bool Release(Point at, IPointer pointer)
    {
        if (_adjust is { } adjusted)
        {
            _adjust = null;
            pointer.Capture(null);
            if (adjusted.Grip.Changed)
                services.ObjectEditing.Replace(editor, value => value is PulseOffice.Drawing.Shapes.ShapeObject ? adjusted.Grip.Preview : value);
            return true;
        }
        if (_gesture is { } done)
        {
            _gesture = null;
            pointer.Capture(null);
            if (done.Moved) Commit(done, at);
            return true;
        }
        return false;
    }

    public void Cancel()
    {
        _gesture = null;
        _adjust = null;
    }
}
