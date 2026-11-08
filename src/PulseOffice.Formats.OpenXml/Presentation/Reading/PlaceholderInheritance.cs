using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Formats.OpenXml.DrawingML;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed record PlaceholderChain(IReadOnlyList<P.Shape> Shapes, ITextStyleLayer Style)
{
    public P.Shape Own => Shapes[0];
}

internal enum PlaceholderFamily { Title, Body, DateTime, Footer, SlideNumber, Other }

internal sealed record PlaceholderKey(P.PlaceholderValues? Type, uint? Index)
{
    public P.PlaceholderValues Effective => Type ?? P.PlaceholderValues.Object;

    public PlaceholderFamily Family =>
        Effective == P.PlaceholderValues.Title || Effective == P.PlaceholderValues.CenteredTitle ? PlaceholderFamily.Title
        : Effective == P.PlaceholderValues.DateAndTime ? PlaceholderFamily.DateTime
        : Effective == P.PlaceholderValues.Footer ? PlaceholderFamily.Footer
        : Effective == P.PlaceholderValues.SlideNumber ? PlaceholderFamily.SlideNumber
        : Effective == P.PlaceholderValues.Header ? PlaceholderFamily.Other
        : PlaceholderFamily.Body;
}

internal interface IPlaceholderMatchRule
{
    P.Shape? Match(PlaceholderKey key, IReadOnlyList<(P.Shape Shape, PlaceholderKey Key)> candidates);
}

internal sealed class ExactMatchRule : IPlaceholderMatchRule
{
    public P.Shape? Match(PlaceholderKey key, IReadOnlyList<(P.Shape Shape, PlaceholderKey Key)> candidates) =>
        candidates.FirstOrDefault(c => c.Key.Index == key.Index && c.Key.Effective == key.Effective).Shape;
}

internal sealed class IndexMatchRule : IPlaceholderMatchRule
{
    public P.Shape? Match(PlaceholderKey key, IReadOnlyList<(P.Shape Shape, PlaceholderKey Key)> candidates) =>
        key.Index is { } index && index != 0
            ? candidates.FirstOrDefault(c => c.Key.Index == index).Shape
            : null;
}

internal sealed class TypeMatchRule : IPlaceholderMatchRule
{
    public P.Shape? Match(PlaceholderKey key, IReadOnlyList<(P.Shape Shape, PlaceholderKey Key)> candidates) =>
        key.Type is null ? null : candidates.FirstOrDefault(c => c.Key.Effective == key.Effective).Shape;
}

internal sealed class FamilyMatchRule : IPlaceholderMatchRule
{
    public P.Shape? Match(PlaceholderKey key, IReadOnlyList<(P.Shape Shape, PlaceholderKey Key)> candidates) =>
        candidates.Where(c => c.Key.Family == key.Family)
            .OrderBy(c => c.Key.Effective == P.PlaceholderValues.Body || c.Key.Effective == P.PlaceholderValues.Title ? 0 : 1)
            .FirstOrDefault().Shape;
}

internal sealed class PlaceholderMatcher(IReadOnlyList<IPlaceholderMatchRule> layoutRules, IReadOnlyList<IPlaceholderMatchRule> masterRules)
{
    public PlaceholderKey? KeyOf(P.Shape shape) =>
        shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.GetFirstChild<P.PlaceholderShape>() is { } placeholder
            ? new PlaceholderKey(placeholder.Type?.Value, placeholder.Index?.Value)
            : null;

    public P.Shape? InLayout(PlaceholderKey key, OpenXmlElement? tree) => Match(key, tree, layoutRules);

    public P.Shape? InMaster(PlaceholderKey key, OpenXmlElement? tree) => Match(key, tree, masterRules);

    private P.Shape? Match(PlaceholderKey key, OpenXmlElement? tree, IReadOnlyList<IPlaceholderMatchRule> rules)
    {
        var candidates = (tree?.Elements<P.Shape>() ?? [])
            .Select(shape => (Shape: shape, Key: KeyOf(shape)))
            .Where(c => c.Key is not null)
            .Select(c => (c.Shape, Key: c.Key!))
            .ToList();
        if (candidates.Count == 0) return null;

        foreach (var rule in rules)
            if (rule.Match(key, candidates) is { } found) return found;
        return null;
    }
}

internal sealed class PlaceholderInheritance(PlaceholderMatcher matcher, LevelStyleReader reader, OoxmlValues values)
{
    public PlaceholderChain ForSlide(P.Shape shape, SlidePart slide, PresentationPart? presentation)
    {
        var layoutPart = slide.SlideLayoutPart;
        return Resolve(shape, layoutPart?.SlideLayout?.CommonSlideData?.ShapeTree, layoutPart?.SlideMasterPart, presentation);
    }

    public PlaceholderChain ForLayout(P.Shape shape, SlideMasterPart? master) => Resolve(shape, null, master, null);

    public DocumentRect Bounds(PlaceholderChain chain) =>
        chain.Shapes.Select(values.Bounds).FirstOrDefault(bounds => bounds.Width.Value > 0 || bounds.Height.Value > 0);

    private PlaceholderChain Resolve(P.Shape shape, OpenXmlElement? layoutTree, SlideMasterPart? masterPart, PresentationPart? presentation)
    {
        var master = masterPart?.SlideMaster;
        var masterTree = master?.CommonSlideData?.ShapeTree;
        var shapes = new List<P.Shape> { shape };
        var layers = new List<ITextStyleLayer>();

        if (matcher.KeyOf(shape) is not { } key)
        {
            layers.Add(Own(shape));
            layers.Add(FontReference(shape));
            layers.Add(new ListStyleLayer(presentation?.Presentation?.DefaultTextStyle, reader));
            return new PlaceholderChain(shapes, new CompositeTextStyle(layers));
        }

        var layout = matcher.InLayout(key, layoutTree);
        if (layout is not null) shapes.Add(layout);
        var masterKey = layout is not null ? matcher.KeyOf(layout) ?? key : key;
        if (matcher.InMaster(masterKey, masterTree) is { } inMaster) shapes.Add(inMaster);

        layers.Add(Own(shape));
        layers.Add(FontReference(shape));
        layers.AddRange(shapes.Skip(1).Select(Own));
        var styles = master?.TextStyles;
        layers.Add(new ListStyleLayer(
            key.Family == PlaceholderFamily.Title ? styles?.TitleStyle
            : key.Family == PlaceholderFamily.Body ? styles?.BodyStyle
            : styles?.OtherStyle, reader));
        return new PlaceholderChain(shapes, new CompositeTextStyle(layers));
    }

    private ITextStyleLayer FontReference(P.Shape shape) => new UniformStyleLayer(reader.FontReference(shape.ShapeStyle?.FontReference));

    private ITextStyleLayer Own(P.Shape shape) => new ListStyleLayer(shape.TextBody?.ListStyle, reader);
}
