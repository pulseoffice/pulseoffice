using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Rulers;

namespace PulseOffice.Documents.UI.Canvas;

public sealed record FolioRulerServices(IFolioViewLayout Layout, IFolioPageStyles Pages, IParagraphRuler Paragraph);

public sealed class FolioRulers
{
    private const string MarginLeft = "margin-left";
    private const string MarginRight = "margin-right";
    private const string MarginTop = "margin-top";
    private const string MarginBottom = "margin-bottom";

    private readonly FolioEditor _editor;
    private readonly FolioCanvas _canvas;
    private readonly FolioRulerServices _services;

    public FolioRulers(FolioEditor editor, FolioCanvas canvas, ScrollViewer scroll, FolioRulerServices services)
    {
        _editor = editor;
        _canvas = canvas;
        _services = services;
        Horizontal = new RulerStrip(RulerOrientation.Horizontal);
        Vertical = new RulerStrip(RulerOrientation.Vertical);
        Horizontal.MarkerMoved += (_, move) => MoveHorizontal(move);
        Horizontal.MarkerRemoved += (_, marker) => RemoveTab(marker);
        Horizontal.Clicked += (_, position) => AddTab(position);
        Vertical.MarkerMoved += (_, move) => MoveVertical(move);
        editor.Changed += (_, _) => Update();
        scroll.ScrollChanged += (_, _) => Update();
        canvas.LayoutUpdated += (_, _) => Update();
        services.Layout.Changed += (_, _) => Update();
    }

    public RulerStrip Horizontal { get; }

    public RulerStrip Vertical { get; }

    public void Update()
    {
        var web = _services.Layout.Mode != FolioViewMode.Print;
        Horizontal.IsVisible = _services.Layout.HorizontalRuler;
        Vertical.IsVisible = _services.Layout.VerticalRuler && !web;
        if (_canvas.CaretPage() is not { } place || !Horizontal.IsAttachedToVisualTree()) return;
        var (slot, page) = place;
        var scale = _canvas.Scale;
        var origin = _canvas.TranslatePoint(new Point(slot.X, slot.Y), Horizontal) ?? default;
        var paragraph = _editor.EffectiveParagraph();
        var content = page.Content;
        var margins = _services.Pages.Read(_services.Pages.Current(_editor)).Margins;
        var left = content.X + (paragraph.MarginLeft ?? 0);
        var tabOrigin = _editor.Document.Styles.TabsRelativeToIndent ? left : content.X;
        var ruler = _services.Paragraph;
        List<RulerMarker> markers = [.. ruler.Markers(new ParagraphRulerFrame(left, content.Right, paragraph.Indent, paragraph.MarginRight, paragraph.TabStops, tabOrigin))];
        if (!web)
        {
            markers.Add(new RulerMarker(MarginLeft, RulerMarkerKind.MarginStart, margins.Left.ToPoints()));
            markers.Add(new RulerMarker(MarginRight, RulerMarkerKind.MarginEnd, page.Width - margins.Right.ToPoints()));
        }
        Horizontal.Model = new RulerModel(RulerOrientation.Horizontal, origin.X, scale, page.Width, content.X, content.Right, markers, ruler.Unit);
        var top = _canvas.TranslatePoint(new Point(slot.X, slot.Y), Vertical) ?? default;
        Vertical.IsVisible = Vertical.IsVisible && slot.CropTop == 0;
        Vertical.Model = new RulerModel(RulerOrientation.Vertical, top.Y, scale, page.Height, content.Y, content.Bottom,
        [
            new(MarginTop, RulerMarkerKind.MarginStart, margins.Top.ToPoints()),
            new(MarginBottom, RulerMarkerKind.MarginEnd, page.Height - margins.Bottom.ToPoints()),
        ], ruler.Unit);
    }

    private void MoveHorizontal(RulerMarkerMove move)
    {
        if (_editor.ReadOnly() || _canvas.CaretPage() is not { } place) return;
        var content = place.Page.Content;
        var paragraph = _editor.EffectiveParagraph();
        var marginLeft = paragraph.MarginLeft ?? 0;
        var left = content.X + marginLeft;
        switch (move.Marker.Id)
        {
            case MarginLeft:
                Margins(margins => margins with { Left = Twip.FromPoints(Math.Max(0, move.Position)) });
                break;
            case MarginRight:
                Margins(margins => margins with { Right = Twip.FromPoints(Math.Max(0, place.Page.Width - move.Position)) });
                break;
            default:
                MoveParagraph(move, left, content.X, content.Right);
                break;
        }
    }

    private void MoveParagraph(RulerMarkerMove move, double left, double contentLeft, double contentRight)
    {
        switch (_services.Paragraph.HandleOf(move.Marker))
        {
            case ParagraphRulerHandle.FirstLine:
                Paragraph(new ParagraphProperties { Indent = Math.Round(move.Position - left, 2) });
                break;
            case ParagraphRulerHandle.Left:
                Paragraph(new ParagraphProperties { MarginLeft = Math.Round(move.Position - contentLeft, 2) });
                break;
            case ParagraphRulerHandle.Right:
                Paragraph(new ParagraphProperties { MarginRight = Math.Round(Math.Max(0, contentRight - move.Position), 2) });
                break;
            case ParagraphRulerHandle.Tab:
                Tabs(tabs => _services.Paragraph.MoveTab(tabs, move.Marker, move.Position - TabOrigin(left, contentLeft)));
                break;
        }
    }

    private void MoveVertical(RulerMarkerMove move)
    {
        if (_editor.ReadOnly() || _canvas.CaretPage() is not { } place) return;
        if (move.Marker.Id == MarginTop) Margins(margins => margins with { Top = Twip.FromPoints(Math.Max(0, move.Position)) });
        else Margins(margins => margins with { Bottom = Twip.FromPoints(Math.Max(0, place.Page.Height - move.Position)) });
    }

    private void AddTab(double position)
    {
        if (_editor.ReadOnly() || _canvas.CaretPage() is not { } place) return;
        var offset = position - TabOrigin(place.Page.Content.X + (_editor.EffectiveParagraph().MarginLeft ?? 0), place.Page.Content.X);
        Tabs(tabs => _services.Paragraph.AddTab(tabs, offset));
    }

    private double TabOrigin(double indent, double margin) => _editor.Document.Styles.TabsRelativeToIndent ? indent : margin;

    private void RemoveTab(RulerMarker marker)
    {
        if (_editor.ReadOnly()) return;
        Tabs(tabs => _services.Paragraph.RemoveTab(tabs, marker));
    }

    private void Tabs(Func<IReadOnlyList<TabStop>, IReadOnlyList<TabStop>> change)
    {
        var current = _editor.EffectiveParagraph().TabStops ?? [];
        var next = change(current);
        if (ReferenceEquals(next, current)) return;
        Paragraph(new ParagraphProperties { TabStops = next });
    }

    private void Paragraph(ParagraphProperties properties) =>
        _editor.Apply((writer, range) => writer.SetParagraph(_editor.Document, _editor.History, range, properties), _editor.ParagraphRange());

    private void Margins(Func<Margins, Margins> change) =>
        _services.Pages.Change(_editor, geometry => geometry with { Margins = change(geometry.Margins) }, "ruler-margins");
}
