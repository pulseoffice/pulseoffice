using System.Xml.Linq;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

internal interface IOdpPageReader
{
    void Read(XElement page, SlideAssembly<string> assembly, OdpReadContext context);
}

internal abstract class OdpPageReader : IOdpPageReader
{
    protected static readonly XNamespace Office = OdfPackage.Namespaces.Office;
    protected static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;
    protected static readonly XNamespace Text = OdfPackage.Namespaces.Text;
    protected static readonly XNamespace Presentation = OdfPackage.Namespaces.Presentation;
    protected static readonly XNamespace Svg = OdfPackage.Namespaces.Svg;
    protected static readonly XName XmlId = OdfPackage.Namespaces.Xml + "id";

    public abstract void Read(XElement page, SlideAssembly<string> assembly, OdpReadContext context);

    protected int Position(XElement node) =>
        int.TryParse((string?)node.Attribute(Draw + "z-index"), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var order)
            ? order
            : _siblings.Of(node);

    private readonly OdpSiblingIndex _siblings = new();
}

internal sealed class OdpSiblingIndex
{
    public int Of(XElement node)
    {
        if (node.Parent is not { } parent) return 0;
        if (parent.Annotation<Positions>() is { } cached && cached.Table.TryGetValue(node, out var known)) return known;

        parent.RemoveAnnotations<Positions>();
        var positions = new Positions(parent.Elements().Select((child, index) => (child, index))
            .ToDictionary<(XElement child, int index), XElement, int>(pair => pair.child, pair => pair.index, ReferenceEqualityComparer.Instance));
        parent.AddAnnotation(positions);
        return positions.Table.GetValueOrDefault(node);
    }

    private sealed record Positions(Dictionary<XElement, int> Table);
}

internal abstract class OdpObjectReader(OdfAltTextMapper altText) : OdpPageReader
{
    public sealed override void Read(XElement page, SlideAssembly<string> assembly, OdpReadContext context)
    {
        foreach (var node in Candidates(page))
            if (ReadOne(node, assembly.NextId(Kind), context) is { } element)
            {
                assembly.Remember((string?)node.Attribute(XmlId) ?? (string?)node.Attribute(Draw + "id"), element.Id);
                assembly.Add(_rotation.Read(node, element) with { ZOrder = Position(node), Content = new OdfObjectNameMapper().Read(altText.Read(element.Content, node), node) });
            }
    }

    private readonly OdpElementRotation _rotation = new();

    protected abstract string Kind { get; }

    protected abstract IEnumerable<XElement> Candidates(XElement page);

    protected abstract ObjectElement? ReadOne(XElement node, string id, OdpReadContext context);

    protected IEnumerable<XElement> FramesWith(XElement page, XName child) =>
        page.Elements(Draw + "frame").Where(frame => frame.Element(child) is not null);
}

internal sealed class PathReader(OdfDeckShapeMapper shapes, OdfAltTextMapper altText) : OdpObjectReader(altText)
{
    protected override string Kind => "path";

    protected override IEnumerable<XElement> Candidates(XElement page) =>
        page.Elements().Where(node => node.Name == Draw + "path" || node.Name == Draw + "polygon" || node.Name == Draw + "polyline");

    protected override ObjectElement? ReadOne(XElement node, string id, OdpReadContext context) => shapes.ReadPath(node, id, context.Styles.Drawings);
}

internal sealed class PrimitiveShapeReader(OdfDeckShapeMapper shapes, OdfAltTextMapper altText) : OdpObjectReader(altText)
{
    protected override string Kind => "shape";

    protected override IEnumerable<XElement> Candidates(XElement page) => page.Elements().Where(node => shapes.Primitives.Contains(node.Name));

    protected override ObjectElement? ReadOne(XElement node, string id, OdpReadContext context) => shapes.ReadPrimitive(node, id, context.Styles.Drawings);
}

internal sealed class SceneReader(OdfDeckShapeMapper shapes, OdfAltTextMapper altText) : OdpObjectReader(altText)
{
    protected override string Kind => "scene";

    protected override IEnumerable<XElement> Candidates(XElement page) => page.Elements(shapes.Scene);

    protected override ObjectElement? ReadOne(XElement node, string id, OdpReadContext context) => shapes.ReadScene(node, id, context.Styles.Drawings);
}

internal sealed class CustomShapeReader(OdfDeckShapeMapper shapes, OdfAltTextMapper altText, OdfFreeTextShape freeText) : OdpObjectReader(altText)
{
    protected override string Kind => "shape";

    protected override IEnumerable<XElement> Candidates(XElement page) => page.Elements(Draw + "custom-shape").Where(node => !freeText.Is(node));

    protected override ObjectElement? ReadOne(XElement node, string id, OdpReadContext context) =>
        shapes.IsFontwork(node) ? shapes.ReadFontwork(node, id, context.Styles.Drawings) : shapes.ReadShape(node, id, context.Styles.Drawings);
}

internal sealed class ImageReader(OdfDeckShapeMapper shapes, OdfAltTextMapper altText) : OdpObjectReader(altText)
{
    protected override string Kind => "img";

    protected override IEnumerable<XElement> Candidates(XElement page) =>
        FramesWith(page, Draw + "image").Where(frame => frame.Element(Draw + "plugin") is null && frame.Element(Draw + "object-ole") is null);

    protected override ObjectElement? ReadOne(XElement node, string id, OdpReadContext context) =>
        shapes.ReadImage(node, id, context.ReadBytes, context.Styles.Drawings);
}

internal sealed class OleReader(OdfDeckShapeMapper shapes, OdfAltTextMapper altText) : OdpObjectReader(altText)
{
    protected override string Kind => "ole";

    protected override IEnumerable<XElement> Candidates(XElement page) => FramesWith(page, Draw + "object-ole");

    protected override ObjectElement? ReadOne(XElement node, string id, OdpReadContext context) =>
        shapes.ReadOle(node, id, context.ReadBytes);
}

internal sealed class FormulaReader(OdfDeckShapeMapper shapes, OdfAltTextMapper altText) : OdpObjectReader(altText)
{
    protected override string Kind => "formula";

    protected override IEnumerable<XElement> Candidates(XElement page) => FramesWith(page, Draw + "object");

    protected override ObjectElement? ReadOne(XElement node, string id, OdpReadContext context) => shapes.ReadFormula(node, id, context.ReadXml);
}

internal sealed class ChartReader(OdfDeckChartMapper charts, OdfAltTextMapper altText) : OdpObjectReader(altText)
{
    protected override string Kind => "chart";

    protected override IEnumerable<XElement> Candidates(XElement page) =>
        FramesWith(page, Draw + "object");

    protected override ObjectElement? ReadOne(XElement node, string id, OdpReadContext context) =>
        charts.Read(node, id, context.ReadXml);
}

internal sealed class TableReader(OdfDeckShapeMapper shapes, OdfAltTextMapper altText) : OdpObjectReader(altText)
{
    protected override string Kind => "table";

    protected override IEnumerable<XElement> Candidates(XElement page) => FramesWith(page, OdfPackage.Namespaces.Table + "table");

    protected override ObjectElement? ReadOne(XElement node, string id, OdpReadContext context) =>
        shapes.ReadTable(node, id, context.Styles.Tables);
}

internal sealed class MediaReader(OdfMediaMapper media) : OdpPageReader
{
    public override void Read(XElement page, SlideAssembly<string> assembly, OdpReadContext context)
    {
        foreach (var frame in page.Elements(Draw + "frame").Where(f => f.Element(Draw + "plugin") is not null))
        {
            var declared = (string?)frame.Attribute(XmlId) ?? (string?)frame.Attribute(Draw + "id");
            var id = declared is not null && assembly.Slide.Find(declared) is null ? declared : assembly.NextId("media");
            if (media.Read(frame, id, context.Values.Bounds(frame), Position(frame), context.ReadBytes) is { } element)
            {
                assembly.Remember((string?)frame.Attribute(XmlId) ?? (string?)frame.Attribute(Draw + "id"), element.Id);
                assembly.Add(element);
            }
        }
    }
}

internal sealed class FormControlReader(OdfFormControlMapper controls) : OdpPageReader
{
    private static readonly XNamespace Form = OdfPackage.Namespaces.Form;

    public override void Read(XElement page, SlideAssembly<string> assembly, OdpReadContext context)
    {
        if (page.Element(Office + "forms") is not { } forms) return;

        var declarations = forms.Descendants()
            .Where(e => e.Attribute(Form + "id") is not null || e.Attribute(XNamespace.Xml + "id") is not null)
            .GroupBy(e => (string?)e.Attribute(XNamespace.Xml + "id") ?? (string)e.Attribute(Form + "id")!)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var frame in page.Elements(Draw + "control"))
        {
            var reference = (string?)frame.Attribute(Draw + "control");
            if (reference is null || !declarations.TryGetValue(reference, out var declaration)) continue;
            var id = (string?)declaration.Attribute(Form + "id") ?? reference;
            if (controls.ReadControl(declaration) is not { } control) continue;

            assembly.Add(new ObjectElement
            {
                Id = id,
                Bounds = context.Values.Bounds(frame),
                ZOrder = Position(frame),
                Content = control with { Id = id },
            });
        }
    }
}

internal sealed class ObjectInteractionReader(OdfInteractionMapper interactions) : OdpPageReader
{
    public override void Read(XElement page, SlideAssembly<string> assembly, OdpReadContext context)
    {
        foreach (var shape in page.Elements())
        {
            var frame = (string?)shape.Attribute(XmlId) ?? (string?)shape.Attribute(Draw + "id");
            if (frame is null
                || !assembly.ElementByFrame.TryGetValue(frame, out var elementId)
                || assembly.Slide.Interaction(elementId) is not null
                || interactions.Read(shape, elementId) is not { } action)
                continue;

            assembly.Slide.AddInteraction(action);
        }
    }
}

internal sealed class GroupReader : OdpPageReader
{
    public override void Read(XElement page, SlideAssembly<string> assembly, OdpReadContext context)
    {
        if (page.Annotation<OdpGroupMembers>() is not { } groups) return;
        var names = groups.Shapes.ToDictionary(shape => shape.Key, shape => $"{assembly.Slide.Id}.group-{shape.Key}", StringComparer.Ordinal);
        foreach (var shape in groups.Shapes)
        {
            var members = shape.Children
                .Select(child => names.TryGetValue(child, out var nested) ? nested : assembly.ElementByFrame.GetValueOrDefault(child))
                .OfType<string>().Distinct(StringComparer.Ordinal).ToList();
            if (members.Count > 0) assembly.Slide.AddGroup(new SlideGroup(names[shape.Key], members) { Name = shape.Name, RotationDegrees = shape.RotationDegrees });
        }
    }
}

internal sealed class AnimationReader(OdfAnimationMapper animations) : OdpPageReader
{
    public override void Read(XElement page, SlideAssembly<string> assembly, OdpReadContext context)
    {
        var targets = new AnimationTargetResolver(page, assembly.ElementByFrame);
        foreach (var effect in animations.Read(page, assembly.Slide.Id))
        {
            var resolved = targets.Resolve(effect.TargetId);
            for (var index = 0; index < resolved.Count; index++)
                assembly.Slide.AddAnimation(index == 0
                    ? effect with { TargetId = resolved[0], Paragraph = resolved.Count == 1 ? targets.Paragraph(effect.TargetId) : null }
                    : effect with { Id = $"{effect.Id}.{index}", TargetId = resolved[index], Trigger = AnimationTrigger.WithPrevious });
        }

        if (!assembly.Slide.Transition.IsNone && animations.TransitionDuration(page) is { } duration)
            assembly.Slide.Transition = assembly.Slide.Transition with { DurationMs = duration };
    }
}

internal sealed class NotesReader : OdpPageReader
{
    public override void Read(XElement page, SlideAssembly<string> assembly, OdpReadContext context)
    {
        if (page.Element(Presentation + "notes") is not { } notes) return;
        var body = notes.Elements(Draw + "frame").FirstOrDefault(frame => (string?)frame.Attribute(Presentation + "class") == "notes") ?? notes;
        var text = string.Join("\n", body.Descendants(Text + "p").Select(p => p.Value));
        if (text.Trim().Length > 0) assembly.Slide.Notes = text;
    }
}

internal sealed class CommentReader(IOdfParagraphText text) : OdpPageReader
{
    private string Fresh(OdpReadContext context)
    {
        string id;
        do id = Guid.NewGuid().ToString("N");
        while (!context.CommentIds.Add(id));
        return id;
    }

    private static readonly XNamespace OfficeOoo = "http://openoffice.org/2009/office";

    public override void Read(XElement page, SlideAssembly<string> assembly, OdpReadContext context)
    {
        foreach (var annotation in page.Elements(Office + "annotation").Concat(page.Elements(OfficeOoo + "annotation")))
            assembly.Slide.AddComment(new SlideComment
            {
                Id = (string?)annotation.Attribute(Draw + "id") is { Length: > 0 } declared && context.CommentIds.Add(declared) ? declared : Fresh(context),
                Author = annotation.Element(OdfPackage.Namespaces.Dc + "creator")?.Value ?? string.Empty,
                Text = text.Read(annotation),
                CreatedAt = DateTimeOffset.TryParse(annotation.Element(OdfPackage.Namespaces.Dc + "date")?.Value,
                    System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var created)
                    ? created : DateTimeOffset.Now,
                X = context.Values.Centimeters(annotation.Attribute(Svg + "x")),
                Y = context.Values.Centimeters(annotation.Attribute(Svg + "y")),
            });
    }
}

internal sealed class AnimationTargetResolver(XElement page, IReadOnlyDictionary<string, string> elementByFrame)
{
    private static readonly XName XmlId = OdfPackage.Namespaces.Xml + "id";
    private static readonly XName DrawId = OdfPackage.Namespaces.Draw + "id";

    private readonly ILookup<string, XElement> _nodes = page.Descendants()
        .SelectMany(node => new[] { (string?)node.Attribute(XmlId), (string?)node.Attribute(DrawId) }.OfType<string>().Distinct().Select(id => (id, node)))
        .ToLookup(pair => pair.id, pair => pair.node);

    public IReadOnlyList<string> Resolve(string target)
    {
        if (elementByFrame.TryGetValue(target, out var direct)) return [direct];
        if (page.Annotation<OdpGroupMembers>()?.Of(target) is { Count: > 0 } members)
            return [.. members.Select(member => elementByFrame.GetValueOrDefault(member)).OfType<string>().Distinct()];
        if (_nodes[target].FirstOrDefault() is not { } node) return [];

        foreach (var ancestor in node.Ancestors())
            if (IdOf(ancestor) is { } id && elementByFrame.TryGetValue(id, out var owner)) return [owner];

        return [.. node.Descendants().Select(IdOf).OfType<string>()
            .Select(id => elementByFrame.GetValueOrDefault(id)).OfType<string>().Distinct()];
    }

    public int? Paragraph(string target)
    {
        if (elementByFrame.ContainsKey(target) || _nodes[target].FirstOrDefault() is not { } node || !IsParagraph(node)) return null;
        var frame = node.Ancestors().FirstOrDefault(ancestor => IdOf(ancestor) is { } id && elementByFrame.ContainsKey(id));
        if (frame is null) return null;
        return ParagraphPositions(frame).TryGetValue(node, out var index) ? index : null;
    }

    private readonly Dictionary<XElement, Dictionary<XElement, int>> _paragraphsByFrame = new(ReferenceEqualityComparer.Instance);

    private Dictionary<XElement, int> ParagraphPositions(XElement frame)
    {
        if (_paragraphsByFrame.TryGetValue(frame, out var known)) return known;
        var positions = new Dictionary<XElement, int>(ReferenceEqualityComparer.Instance);
        foreach (var paragraph in frame.Descendants().Where(IsParagraph)) positions.Add(paragraph, positions.Count);
        _paragraphsByFrame[frame] = positions;
        return positions;
    }

    private bool IsParagraph(XElement node) => node.Name == TextP || node.Name == TextH;

    private static readonly XName TextP = OdfPackage.Namespaces.Text + "p";
    private static readonly XName TextH = OdfPackage.Namespaces.Text + "h";

    private string? IdOf(XElement node) => (string?)node.Attribute(XmlId) ?? (string?)node.Attribute(DrawId);
}

internal sealed class OdpShapeIds
{
    private static readonly XName XmlId = OdfPackage.Namespaces.Xml + "id";
    private static readonly XName DrawId = OdfPackage.Namespaces.Draw + "id";

    public XElement Assign(XElement page)
    {
        var number = 0;
        foreach (var shape in page.Elements().Where(element => element.Name.Namespace == OdfPackage.Namespaces.Draw))
        {
            number++;
            if (shape.Attribute(XmlId) is null && shape.Attribute(DrawId) is null)
                shape.SetAttributeValue(XmlId, $"pulseoffice-shape-{number}");
        }
        return page;
    }
}
