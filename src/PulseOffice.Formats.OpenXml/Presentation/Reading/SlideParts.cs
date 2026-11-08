using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Formats.OpenXml.DrawingML;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed record PptxSlideSource(SlidePart Slide, PresentationPart Presentation)
{
    public P.ShapeTree? Tree => Slide.Slide?.CommonSlideData?.ShapeTree;

    public IReadOnlyList<P.Shape> Shapes => [.. Tree?.Elements<P.Shape>() ?? []];

    public PptxGroups Groups { get; init; } = PptxGroups.None;

    public IReadOnlyList<PptxDiagramSource> Diagrams { get; init; } = [];

    public SoundLibrary Sounds { get; init; } = new();

    private Dictionary<DocumentFormat.OpenXml.OpenXmlElement, int>? _positions;

    public int Position(DocumentFormat.OpenXml.OpenXmlElement node, int fallback)
    {
        if (node.Parent is not { } parent) return fallback;
        if (!ReferenceEquals(parent, Tree)) return parent.ChildElements.ToList().IndexOf(node);
        _positions ??= Tree!.ChildElements.Select((child, index) => (child, index)).ToDictionary<(DocumentFormat.OpenXml.OpenXmlElement child, int index), DocumentFormat.OpenXml.OpenXmlElement, int>(pair => pair.child, pair => pair.index, ReferenceEqualityComparer.Instance);
        return _positions.TryGetValue(node, out var position) ? position : fallback;
    }
}

internal interface IPptxSlidePartReader
{
    void Read(PptxSlideSource source, SlideAssembly<uint> assembly);
}

internal abstract class PptxObjectReader<TNode>(OoxmlAltTextMapper altText) : IPptxSlidePartReader
    where TNode : DocumentFormat.OpenXml.OpenXmlElement
{
    public void Read(PptxSlideSource source, SlideAssembly<uint> assembly)
    {
        foreach (var node in source.Tree?.Elements<TNode>() ?? [])
            Place(node, source, assembly);
    }

    private readonly PptxElementRotation _rotation = new();

    private readonly OoxmlObjectNameMapper _names = new();

    protected abstract void Place(TNode node, PptxSlideSource source, SlideAssembly<uint> assembly);

    protected void Add(SlideAssembly<uint> assembly, ObjectElement element, DocumentFormat.OpenXml.OpenXmlElement node, PptxSlideSource source)
    {
        if (node.Descendants<P.NonVisualDrawingProperties>().FirstOrDefault()?.Id?.Value is { } spid) assembly.Remember(spid, element.Id);
        var named = element with { Content = _names.Read(altText.Read(element.Content, node), node) };
        assembly.Add(_rotation.Read(node, named) with { ZOrder = Position(node, source) });
    }

    protected int Position(DocumentFormat.OpenXml.OpenXmlElement node, PptxSlideSource source) => source.Position(node, 0);
}

internal sealed class TextShapeReader(
    ShapeClassifier shapes, PlaceholderInheritance inheritance, DrawingParagraphReader paragraphs, PptxInteractionMapper interactions,
    BodyFrameReader frames, PptxTextBackdrops backdrops)
    : IPptxSlidePartReader
{
    private readonly PptxElementRotation _rotation = new();

    private readonly OoxmlObjectNameMapper _names = new();

    public IReadOnlyList<P.Shape> TextShapes(PptxSlideSource source) =>
        [.. source.Shapes.Where(shape => shapes.Classify(shape) == ShapeClass.Text)];

    public void Read(PptxSlideSource source, SlideAssembly<uint> assembly)
    {
        var text = TextShapes(source);
        for (var index = 0; index < text.Count; index++)
        {
            var shape = text[index];
            var role = assembly.Roles[index];
            var id = assembly.NextId(role.ToString().ToLowerInvariant());
            var inheritsBullets = role == PlaceholderRole.Body && shapes.IsPlaceholder(shape);
            var drawing = shape.NonVisualShapeProperties?.NonVisualDrawingProperties;
            var chain = inheritance.ForSlide(shape, source.Slide, source.Presentation);

            if (drawing?.Id?.Value is { } spid) assembly.Remember(spid, id);
            assembly.Add(_rotation.Read(shape, new TextElement
            {
                Id = id,
                Bounds = inheritance.Bounds(chain),
                ZOrder = source.Position(shape, assembly.Order),
                Text = new RichText(role, paragraphs.Read(shape.TextBody, chain.Style, inheritsBullets, new PptxSlideRunLinkReader(source.Slide, source.Presentation)))
                {
                    Frame = frames.Resolve(chain.Shapes.Select(link => link.TextBody?.BodyProperties), TextFrame.Default),
                },
                Backdrop = backdrops.Read(chain.Shapes),
            }));

            if (drawing is not null && interactions.Read(drawing, source.Slide, id) is { } action)
                assembly.Slide.AddInteraction(action);
        }
    }
}

internal sealed class ObjectInteractionReader(PptxInteractionMapper interactions) : IPptxSlidePartReader
{
    public void Read(PptxSlideSource source, SlideAssembly<uint> assembly)
    {
        foreach (var drawing in source.Tree?.Descendants<P.NonVisualDrawingProperties>() ?? [])
        {
            if (drawing.Id?.Value is not { } spid
                || !assembly.ElementByFrame.TryGetValue(spid, out var elementId)
                || assembly.Slide.Interaction(elementId) is not null
                || interactions.Read(drawing, source.Slide, elementId) is not { } action)
                continue;

            assembly.Slide.AddInteraction(action);
        }
    }
}

internal sealed class PictureReader(PptxMediaMapper media, PptxDeckShapeMapper shapes, OoxmlAltTextMapper altText)
    : PptxObjectReader<P.Picture>(altText)
{
    protected override void Place(P.Picture picture, PptxSlideSource source, SlideAssembly<uint> assembly)
    {
        if (media.Read(picture, source.Slide, assembly.NextId("media"), Position(picture, source)) is { } element)
        {
            if (picture.NonVisualPictureProperties?.NonVisualDrawingProperties?.Id?.Value is { } spid) assembly.Remember(spid, element.Id);
            assembly.Add(element with { ZOrder = Position(picture, source) });
        }
        else if (shapes.ReadImage(picture, source.Slide, assembly.NextId("img")) is { } image)
            Add(assembly, image, picture, source);
    }
}

internal sealed class DeckShapeReader(ShapeClassifier classifier, PptxDeckShapeMapper shapes, OoxmlAltTextMapper altText)
    : PptxObjectReader<P.Shape>(altText)
{
    protected override void Place(P.Shape shape, PptxSlideSource source, SlideAssembly<uint> assembly)
    {
        var element = classifier.Name(shape) switch
        {
            "DeckFormula" => shapes.ReadFormula(shape, assembly.NextId("formula")),
            DeckSceneCodec.Name => shapes.ReadScene(shape, assembly.NextId("scene")),
            _ when shapes.IsFontwork(shape) => shapes.ReadFontwork(shape, assembly.NextId("fontwork")),
            "DeckFreeform" or "DeckConnector" => shapes.ReadGeometry(shape, assembly.NextId("path")),
            _ when classifier.Classify(shape) == ShapeClass.Freeform => shapes.ReadGeometry(shape, assembly.NextId("path")),
            _ when classifier.Classify(shape) == ShapeClass.Shape => shapes.ReadShape(shape, assembly.NextId("shape")),
            _ => null,
        };
        if (element is not null) Add(assembly, _links.Read(shape, element, new PptxSlideRunLinkReader(source.Slide, source.Presentation)), shape, source);
    }

    private readonly PptxShapeTextRuns _links = new();
}

internal sealed class ConnectionShapeReader(PptxDeckShapeMapper shapes, OoxmlAltTextMapper altText)
    : PptxObjectReader<P.ConnectionShape>(altText)
{
    protected override void Place(P.ConnectionShape connection, PptxSlideSource source, SlideAssembly<uint> assembly)
    {
        if (shapes.ReadConnection(connection, assembly.NextId("connector")) is { } element) Add(assembly, element, connection, source);
    }
}

internal sealed class GraphicFrameReader(
    PptxDeckShapeMapper shapes, PptxDeckChartMapper charts, PptxDeckTableMapper tables, OoxmlAltTextMapper altText)
    : PptxObjectReader<P.GraphicFrame>(altText)
{
    protected override void Place(P.GraphicFrame frame, PptxSlideSource source, SlideAssembly<uint> assembly)
    {
        var element = shapes.ReadOle(frame, source.Slide, assembly.NextId("ole"))
            ?? charts.Read(frame, source.Slide, assembly.NextId("chart"))
            ?? tables.Read(frame, assembly.NextId("table"), source.Presentation);
        if (element is not null) Add(assembly, element, frame, source);
    }
}

internal sealed class PptxAnimationReader(PptxAnimationMapper animations, PptxSounds sounds) : IPptxSlidePartReader
{
    public void Read(PptxSlideSource source, SlideAssembly<uint> assembly)
    {
        IReadOnlyList<string> Resolve(uint spid) =>
            assembly.ElementByFrame.TryGetValue(spid, out var direct) ? [direct]
            : source.Groups.Members.TryGetValue(spid, out var members) ? [.. members.Select(member => assembly.ElementByFrame.GetValueOrDefault(member)).OfType<string>()]
            : [];

        foreach (var effect in animations.FromTiming(source.Slide.Slide?.GetFirstChild<P.Timing>(), Resolve, assembly.Slide.Id,
                     target => sounds.Read(target, source.Slide, source.Sounds)))
            assembly.Slide.AddAnimation(effect);
    }
}

internal sealed class PptxGroupReader : IPptxSlidePartReader
{
    public void Read(PptxSlideSource source, SlideAssembly<uint> assembly)
    {
        var diagrams = source.Diagrams.GroupBy(diagram => diagram.GroupId)
            .ToDictionary(group => group.Key, group => group.SelectMany(diagram => diagram.MemberIds).Distinct().ToList());
        var shapes = source.Groups.Shapes.Where(shape => !diagrams.ContainsKey(shape.Id)).ToList();
        var names = shapes.ToDictionary(shape => shape.Id, shape => GroupId(assembly, shape.Id));
        foreach (var shape in shapes)
        {
            var members = shape.Children
                .SelectMany(child => names.TryGetValue(child, out var nested) ? [nested]
                    : diagrams.TryGetValue(child, out var diagram) ? diagram.Select(member => assembly.ElementByFrame.GetValueOrDefault(member))
                    : [assembly.ElementByFrame.GetValueOrDefault(child)])
                .OfType<string>().Distinct(StringComparer.Ordinal).ToList();
            if (members.Count > 0) assembly.Slide.AddGroup(new SlideGroup(names[shape.Id], members) { Name = shape.Name, RotationDegrees = shape.RotationDegrees });
        }
    }

    private string GroupId(SlideAssembly<uint> assembly, uint spid) =>
        $"{assembly.Slide.Id}.group{spid.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
}

internal sealed class PptxCommentReader(PptxCommentMapper comments) : IPptxSlidePartReader
{
    public void Read(PptxSlideSource source, SlideAssembly<uint> assembly)
    {
        foreach (var comment in comments.Read(source.Slide, source.Presentation, assembly.Slide.Id))
            assembly.Slide.AddComment(comment);
    }
}

internal sealed class BackgroundImageReader(PptxBackgroundFillReader fills) : IPptxSlidePartReader
{
    private P.Background? Shared(SlideLayoutPart? layout)
    {
        if (layout?.SlideLayout?.CommonSlideData?.Background is { } own) return own;
        if (layout?.SlideMasterPart is not { } master) return null;
        var type = layout.SlideLayout?.Type?.InnerText;
        var siblings = master.SlideLayoutParts.Where(other => other != layout && other.SlideLayout?.Type?.InnerText == type).ToList();
        if (!siblings.Any(other => other.SlideLayout?.CommonSlideData?.Background is not null)) return null;
        return layout.SlideLayout?.CommonSlideData?.Background ?? master.SlideMaster?.CommonSlideData?.Background;
    }

    public void Read(PptxSlideSource source, SlideAssembly<uint> assembly)
    {
        var theme = source.Slide.SlideLayoutPart?.SlideMasterPart?.ThemePart;
        var background = source.Slide.Slide?.CommonSlideData?.Background;
        assembly.Slide.BackgroundFill = background is null
            ? fills.Read(source.Slide.SlideLayoutPart?.SlideLayout?.CommonSlideData?.Background, theme)
            : fills.Read(background, theme);
        if (background is null && assembly.Slide.BackgroundFill is null && Shared(source.Slide.SlideLayoutPart) is { } inherited
            && fills.Colour(inherited, theme) is { } inheritedColour)
            assembly.Slide.Background = inheritedColour;
        if (fills.Solid(background, theme) is { } referenced) assembly.Slide.Background = referenced;
        if (background?.BackgroundProperties?.GetFirstChild<D.SolidFill>() is not null && fills.Colour(background, theme) is { } own)
            assembly.Slide.Background = own;
        if (source.Slide.Slide?.CommonSlideData?.Background?.BackgroundProperties?.GetFirstChild<D.BlipFill>()?.GetFirstChild<D.Tile>() is not null)
            assembly.Slide.BackgroundLayout = new BackgroundImageLayout(BackgroundImageMode.Tile, PulseOffice.Foundation.Units.Twip.Zero, PulseOffice.Foundation.Units.Twip.Zero);

        if (source.Slide.Slide?.CommonSlideData?.Background?.BackgroundProperties?
                .GetFirstChild<D.BlipFill>()?.Blip?.Embed?.Value is not { } embed
            || !source.Slide.TryGetPartById(embed, out var found) || found is not ImagePart part) return;

        using var stream = part.GetStream();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        if (source.Slide.Slide?.CommonSlideData?.Background?.BackgroundProperties?.GetFirstChild<D.BlipFill>()?.Blip?.GetFirstChild<D.AlphaModulationFixed>()?.Amount?.Value is { } amount)
            assembly.Slide.BackgroundLayout = assembly.Slide.BackgroundLayout with { Opacity = Math.Clamp(amount / 100000.0, 0, 1) };
        assembly.Slide.BackgroundImage = new PulseOffice.Drawing.Assets.RasterImageObject
        {
            Id = $"{assembly.Slide.Id}.bg",
            Data = buffer.ToArray(),
            MediaType = part.ContentType,
            Duotone = fills.Duotone(source.Slide.Slide?.CommonSlideData?.Background?.BackgroundProperties?.GetFirstChild<D.BlipFill>()?.Blip),
        };
    }
}

internal sealed class PptxSlideReader(
    PptxLayoutIdentity identities,
    SchemeRoles schemes,
    ColorMapCodec maps,
    PptxThemeReader themes,
    ShapeClassifier classifier,
    PptxTransitionMapper transitions,
    TextShapeReader text,
    IReadOnlyList<IPptxSlidePartReader> parts,
    DrawingShapeColourNormalizer normalizer,
    PptxBackgroundFillReader backgrounds)
{
    private readonly PptxAlternateContent _alternates = new();

    public Slide Read(PptxSlideSource source, string id)
    {
        var layoutPart = source.Slide.SlideLayoutPart;
        var master = maps.Read(layoutPart?.SlideMasterPart?.SlideMaster?.ColorMap, OoxmlColorMap.Standard);
        var effective = maps.Override(maps.Override(master, layoutPart?.SlideLayout?.ColorMapOverride), source.Slide.Slide?.ColorMapOverride);
        using var scope = schemes.Enter(master, effective, layoutPart?.SlideMasterPart is { } masterPart ? themes.Read(masterPart).Colors : null);
        var theme = layoutPart?.SlideMasterPart?.ThemePart;
        _alternates.Unwrap(source.Slide.Slide?.CommonSlideData?.ShapeTree);
        normalizer.Normalize(source.Slide.Slide?.CommonSlideData?.ShapeTree, theme, () => backgrounds.FillElement(
            source.Slide.Slide?.CommonSlideData?.Background ?? layoutPart?.SlideLayout?.CommonSlideData?.Background
            ?? layoutPart?.SlideMasterPart?.SlideMaster?.CommonSlideData?.Background, theme));
        var roles = classifier.Roles(text.TextShapes(source));
        var declared = source.Slide.SlideLayoutPart?.SlideLayout?.Type;
        var layout = declared is not null && layoutPart is not null ? identities.KindOf(layoutPart) : classifier.Infer(roles);

        var slide = new Slide(id, layout)
        {
            LayoutId = identities.IdOf(layoutPart),
            Name = source.Slide.Slide?.CommonSlideData?.Name?.Value is { Length: > 0 } name ? name : null,
            Hidden = source.Slide.Slide?.Show?.Value == false,
            ShowMasterObjects = source.Slide.Slide?.ShowMasterShapes?.Value != false,
        };
        if (transitions.FromSlide(source.Slide.Slide) is { } transition)
            slide.Transition = transition;

        var assembly = new SlideAssembly<uint>(slide, roles);
        text.Read(source, assembly);
        foreach (var part in parts) part.Read(source, assembly);
        foreach (var diagram in source.Diagrams)
        {
            var members = diagram.MemberIds
                .Select(member => assembly.ElementByFrame.TryGetValue(member, out var elementId) ? slide.Elements.FirstOrDefault(element => element.Id == elementId) : null)
                .OfType<SlideElement>()
                .ToList();
            var points = diagram.PointsByMember
                .Where(pair => assembly.ElementByFrame.ContainsKey(pair.Key))
                .GroupBy(pair => assembly.ElementByFrame[pair.Key], StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal);
            if (members.Count > 0) slide.AddDiagram(new NativeDiagram(members, diagram.FrameXml, diagram.Parts) { PointsByMember = points });
        }
        return slide;
    }
}
