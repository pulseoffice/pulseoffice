using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Presentations.UI;

public enum PrintMasterKind { Notes, Handout }

public enum NotesMasterBox { Slide, Notes }

public sealed class PrintMasterView(ISlideRenderer renderer, IHandoutLayout layout, string notesSample, IObjectManipulator? manipulator = null) : PulseOffice.UI.Controls.Theming.ThemedSurface
{
    private const double PageRatio = 297.0 / 210.0;

    private readonly IObjectManipulator _manipulator = manipulator ?? new ObjectManipulator();
    private Rect _page;
    private (NotesMasterBox Box, ObjectHandle Handle, Point Origin, ObjectBox Start)? _drag;
    private ObjectBox? _preview;
    private readonly IBrush _shadow = new SolidColorBrush(Colors.Black, 0.12);
    private readonly IPen _slideEdge = new Pen(Brushes.Gray, 0.75);
    private readonly IPen _notesEdge = new Pen(Brushes.Gray, 1) { DashStyle = DashStyle.Dash };

    public required Presentation Deck { get; init; }

    public PrintMasterKind Kind { get; init; }

    public NotesMasterBox? Selected { get; private set; }

    public event EventHandler<(NotesMasterBox Box, PageBox Bounds)>? BoxChanged;

    public override void Render(DrawingContext context)
    {
        var available = new Rect(Bounds.Size).Deflate(24);
        if (available.Width <= 0 || available.Height <= 0) return;

        var height = Math.Min(available.Height, available.Width * PageRatio);
        var width = height / PageRatio;
        _page = new Rect(available.X + (available.Width - width) / 2, available.Y + (available.Height - height) / 2, width, height);

        context.DrawRectangle(_shadow, null, _page.Translate(new Vector(0, 3)), 2, 2);
        context.DrawRectangle(Brushes.White, new Pen(Themed(PulseOffice.UI.Theming.Palette.Keys.Border, Brushes.LightGray), 1), _page);

        if (Kind == PrintMasterKind.Notes) RenderNotes(context);
        else RenderHandout(context);
    }

    private Rect Place(PageBox box) =>
        new(_page.X + box.X * _page.Width, _page.Y + box.Y * _page.Height, box.Width * _page.Width, box.Height * _page.Height);

    private ObjectBox ToBox(Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    private PageBox ToPage(ObjectBox box)
    {
        var normal = box.Normalized();
        return new PageBox((normal.X - _page.X) / _page.Width, (normal.Y - _page.Y) / _page.Height, normal.Width / _page.Width, normal.Height / _page.Height).Clamped();
    }

    private Rect BoxRect(NotesMasterBox box)
    {
        if (_drag is { } drag && drag.Box == box && _preview is { } preview)
        {
            var normal = preview.Normalized();
            return new Rect(normal.X, normal.Y, normal.Width, normal.Height);
        }
        return Place(box == NotesMasterBox.Slide ? Deck.NotesMaster.SlideBox : Deck.NotesMaster.NotesBox);
    }

    private void RenderNotes(DrawingContext context)
    {
        var master = Deck.NotesMaster;
        var slideArea = Fit(BoxRect(NotesMasterBox.Slide));
        if (Deck.Slides.FirstOrDefault() is { } slide)
            renderer.Render(context, slide, Deck.Size, slideArea, Deck.ThemeOf(slide), Deck.MasterOf(slide), Palette());
        context.DrawRectangle(null, _slideEdge, slideArea);

        var notes = BoxRect(NotesMasterBox.Notes);
        context.DrawRectangle(null, _notesEdge, notes);
        var scale = _page.Height / (297.0 / 25.4 * 72);
        var text = new FormattedText(notesSample, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default,
            Math.Max(4, master.FontSize * scale), Brushes.DimGray) { MaxTextWidth = Math.Max(1, notes.Width - 8), MaxTextHeight = Math.Max(1, notes.Height - 8) };
        context.DrawText(text, notes.TopLeft + new Vector(4, 4));

        RenderChrome(context, master.Header, master.Footer, master.PageNumber);
        if (Selected is { } selected) DrawHandles(context, BoxRect(selected));
    }

    private void RenderHandout(DrawingContext context)
    {
        var master = Deck.Handout;
        var cells = layout.Cells(master.SlidesPerPage);
        for (var index = 0; index < cells.Count; index++)
        {
            var area = Fit(Place(cells[index]));
            if (index < Deck.Slides.Count)
            {
                var slide = Deck.Slides[index];
                renderer.Render(context, slide, Deck.Size, area, Deck.ThemeOf(slide), Deck.MasterOf(slide), Palette());
            }
            context.DrawRectangle(null, _slideEdge, area);
        }

        RenderChrome(context, master.Header, master.Footer, master.PageNumber);
    }

    private void RenderChrome(DrawingContext context, string header, string footer, bool pageNumber)
    {
        var size = Math.Max(6, _page.Height * 0.014);
        var side = _page.Width * 0.057;
        FormattedText Text(string value, IBrush brush) =>
            new(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, size, brush);

        var top = _page.Y + _page.Height * 0.02;
        var bottom = _page.Bottom - _page.Height * 0.04;

        context.DrawText(Text(header.Length > 0 ? header : "", Brushes.DimGray), new Point(_page.X + side, top));
        context.DrawText(Text(footer.Length > 0 ? footer : "", Brushes.DimGray), new Point(_page.X + side, bottom));
        if (pageNumber)
        {
            var number = Text("1", Brushes.DimGray);
            context.DrawText(number, new Point(_page.Right - side - number.Width, bottom));
        }
    }

    private Rect Fit(Rect area)
    {
        var ratio = Deck.Size.Height.Value / (double)Math.Max(1, Deck.Size.Width.Value);
        var width = area.Width;
        var height = width * ratio;
        if (height > area.Height)
        {
            height = area.Height;
            width = height / ratio;
        }
        return new Rect(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height);
    }

    private SlidePalette Palette() => new(Colors.Transparent, Colors.Transparent);

    private void DrawHandles(DrawingContext context, Rect rect)
    {
        var accent = this.FindResource("PulseAccent") as IBrush ?? Brushes.SteelBlue;
        context.DrawRectangle(null, new Pen(accent, 1.5), rect);
        foreach (var (_, area) in _manipulator.Handles(ToBox(rect)))
            context.DrawRectangle(Brushes.White, new Pen(accent, 1.2), new Rect(area.X, area.Y, area.Width, area.Height));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Kind != PrintMasterKind.Notes) return;

        var point = e.GetPosition(this);
        if (Selected is { } current)
        {
            var handle = _manipulator.HandleAt(ToBox(BoxRect(current)), 0, point.X, point.Y);
            if (handle is not ObjectHandle.None and not ObjectHandle.Rotate)
            {
                Begin(current, handle, point, e);
                return;
            }
        }

        foreach (var box in new[] { NotesMasterBox.Slide, NotesMasterBox.Notes })
            if (BoxRect(box).Contains(point))
            {
                Selected = box;
                Begin(box, ObjectHandle.Body, point, e);
                return;
            }

        Selected = null;
        InvalidateVisual();
    }

    private void Begin(NotesMasterBox box, ObjectHandle handle, Point point, PointerPressedEventArgs e)
    {
        _drag = (box, handle, point, ToBox(BoxRect(box)));
        _preview = _drag.Value.Start;
        e.Pointer.Capture(this);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag is not { } drag) return;
        var point = e.GetPosition(this);
        _preview = _manipulator.Resize(drag.Start, drag.Handle, point.X - drag.Origin.X, point.Y - drag.Origin.Y, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag is not { } drag) return;
        var result = _preview;
        _drag = null;
        _preview = null;
        e.Pointer.Capture(null);
        if (result is { } final && final != drag.Start) BoxChanged?.Invoke(this, (drag.Box, ToPage(final)));
        InvalidateVisual();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_drag is null) return;
        _drag = null;
        _preview = null;
        InvalidateVisual();
    }
}
