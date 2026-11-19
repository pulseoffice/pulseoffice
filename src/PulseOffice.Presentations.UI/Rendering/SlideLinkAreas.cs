using Avalonia;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Printing;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Controls.Text.Rich;

namespace PulseOffice.Presentations.UI.Rendering;

public sealed record SlideLinkPlacement(Presentation Deck, Slide Slide, Rect Target, Func<Slide, bool> Exported);

public interface ISlideLinkAreas
{
    IReadOnlyList<PrintLink> For(SlideLinkPlacement placement);

    string Anchor(Slide slide);
}

public sealed class SlideLinkAreas(ITextFrameComposer composer, IRichTextLinkHit hits, IShapeListText shapeText) : ISlideLinkAreas
{
    public SlideLinkAreas() : this(new TextFrameComposer(new ListLabels()), new RichTextLinkHit(), new ShapeListText())
    {
    }

    public string Anchor(Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        return "slide-" + slide.Id;
    }

    public IReadOnlyList<PrintLink> For(SlideLinkPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        var (deck, slide, target, _) = placement;
        var scale = deck.Size.Width.Value == 0 ? 1 : target.Width / deck.Size.Width.Value;
        var visible = slide.Elements.Where(element => !element.Hidden).ToList();
        var links = new List<PrintLink>();
        var linked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in slide.Interactions)
        {
            if (Destination(placement, action) is not { } destination) continue;
            if (visible.FirstOrDefault(element => element.Id == action.TargetId) is not { } element || !linked.Add(element.Id)) continue;
            var area = Place(element.Bounds, target, scale);
            links.Add(new PrintLink(Twips(Rotate(area, area.Center, element.RotationDegrees)), destination));
        }
        var fields = new SlideFieldsFactory().For(deck, slide);
        foreach (var element in visible)
        {
            if (linked.Contains(element.Id)) continue;
            var bounds = Place(element.Bounds, target, scale);
            switch (element)
            {
                case TextElement text when Linked(text.Text):
                    Add(links, composer.Compose(text.Text, bounds, deck.ThemeOf(slide), fields, scale), fields, bounds, text.RotationDegrees);
                    break;
                case ObjectElement { Content: ShapeObject shape } when shape.Runs is { Count: > 0 } runs && runs.Any(run => run.Link is not null):
                    Add(links, composer.Compose(shapeText.Framed(shape, shape.RotationDegrees), bounds, shapeText.Theme(shape, default), fields, scale), fields, bounds, shape.RotationDegrees);
                    break;
            }
        }
        return links;
    }

    private string? Destination(SlideLinkPlacement placement, InteractiveAction action)
    {
        if (action.Kind == InteractionKind.Hyperlink) return action.Reference is { Length: > 0 } reference ? reference : null;
        var shown = placement.Deck.Slides.Where(placement.Exported).ToList();
        var here = shown.IndexOf(placement.Slide);
        var slide = action.Kind switch
        {
            InteractionKind.NextSlide => here >= 0 && here + 1 < shown.Count ? shown[here + 1] : null,
            InteractionKind.PreviousSlide => here > 0 ? shown[here - 1] : null,
            InteractionKind.FirstSlide => shown.FirstOrDefault(),
            InteractionKind.LastSlide => shown.LastOrDefault(),
            InteractionKind.GoToSlide => shown.FirstOrDefault(candidate => string.Equals(candidate.Id, action.Reference, StringComparison.Ordinal)),
            _ => null,
        };
        return slide is null ? null : "#" + Anchor(slide);
    }

    private bool Linked(RichText text) =>
        text.Paragraphs.SelectMany(paragraph => paragraph.EffectiveRuns()).Any(run => run.Hyperlink is { Length: > 0 });

    private void Add(List<PrintLink> links, ComposedTextFrame composed, TextFields fields, Rect bounds, double degrees)
    {
        foreach (var area in hits.Areas(composed, fields))
            links.Add(new PrintLink(Twips(Rotate(area.Bounds, bounds.Center, degrees)), area.Link.Address));
    }

    private Rect Place(DocumentRect bounds, Rect target, double scale) =>
        new(target.X + bounds.Left.Value * scale, target.Y + bounds.Top.Value * scale, bounds.Width.Value * scale, bounds.Height.Value * scale);

    private Rect Rotate(Rect area, Point centre, double degrees)
    {
        if (degrees == 0 || !double.IsFinite(degrees)) return area;
        var turn = Matrix.CreateTranslation(-centre.X, -centre.Y) * Matrix.CreateRotation(degrees * Math.PI / 180) * Matrix.CreateTranslation(centre.X, centre.Y);
        return area.TransformToAABB(turn);
    }

    private DocumentRect Twips(Rect rect) =>
        new(Twip.FromPoints(rect.X), Twip.FromPoints(rect.Y), Twip.FromPoints(Math.Max(0, rect.Width)), Twip.FromPoints(Math.Max(0, rect.Height)));
}
