using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Presentations;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class PptxSlideWriter(
    OoxmlWriteUnits units,
    ShapeTreeFactory trees,
    TextShapeBuilder text,
    PptxInteractionMapper interactions,
    PptxMediaMapper media,
    IReadOnlyList<IPptxObjectWriter> objects,
    PptxTransitionMapper transitions,
    PptxAnimationMapper animations,
    PptxCommentMapper comments,
    ISlideGroupDecomposition decomposition)
{
    private readonly PptxSlideGroupWriter _groups = new(decomposition);

    private readonly SlideGroupRotation _groupFrames = new(new SlideGroupUnits(), decomposition);

    private readonly OoxmlImageTypes _imageTypes = new();
    private readonly DrawingML.DrawingDuotone _duotones = new();

    private readonly PptxHeaderFooter _headerFooter = new();

    private readonly PptxElementRotation _rotation = new();

    private readonly PptxElementVisibility _visibility = new();

    private readonly PptxShapeTextRuns _shapeRuns = new();

    private readonly PptxSounds _sounds = new(new SoundTypes());

    private readonly PptxDiagramWriter _diagrams = new(new Diagrams.DiagramDrawingGenerator());

    private readonly PptxDiagramTexts _diagramTexts = new();

    public SlidePart Write(PptxWriteSession session, SlideLayoutPart layout, Slide slide)
    {
        var part = session.Part.AddNewPart<SlidePart>();
        var tree = trees.Empty();
        var shapes = new Dictionary<string, uint>(StringComparer.Ordinal);
        uint shapeId = 2;

        var diagrams = slide.IntactDiagrams().ToList();
        var edited = slide.TextEditedDiagrams().ToList();
        var emitted = new HashSet<NativeDiagram>();
        var nodes = new SlideGroupNodes<OpenXmlElement>();
        var framed = _groupFrames.InGroupFrames(slide);
        void Frame(NativeDiagram diagram, P.GraphicFrame frame)
        {
            tree.Append(frame);
            foreach (var member in diagram.Members)
            {
                shapes.TryAdd(member.Id, frame.NonVisualGraphicFrameProperties!.NonVisualDrawingProperties!.Id!.Value);
                nodes.Remember(member.Id, frame);
            }
        }
        foreach (var element in slide.InZOrder())
        {
            if (diagrams.FirstOrDefault(diagram => diagram.Members.Any(member => ReferenceEquals(member, element))) is { } diagram)
            {
                if (emitted.Add(diagram)) Frame(diagram, _diagrams.Write(diagram, shapeId++, part));
                continue;
            }
            if (edited.FirstOrDefault(diagram => diagram.Members.Any(member => member.Id == element.Id)) is { } changed)
            {
                if (emitted.Add(changed)) Frame(changed, _diagrams.Write(changed, shapeId++, part, _diagramTexts.Edits(slide, changed)));
                continue;
            }

            switch (framed.GetValueOrDefault(element.Id) ?? element)
            {
                case TextElement textElement:
                {
                    var id = shapeId++;
                    shapes[textElement.Id] = id;
                    var shape = _visibility.Write(_rotation.Write(text.Build(id, textElement, session.Deck.Theme, new PptxSlideRunLinks(part, session)), textElement), textElement);
                    if (slide.Interaction(textElement.Id) is { } interaction)
                        interactions.Apply(shape.NonVisualShapeProperties!.NonVisualDrawingProperties!, interaction, part, session);
                    tree.Append(shape);
                    nodes.Remember(textElement.Id, shape);
                    break;
                }
                case MediaElement mediaElement:
                {
                    shapes[mediaElement.Id] = shapeId;
                    var built = _visibility.Write(_rotation.Write(media.Build(shapeId++, mediaElement, session.Document, part), mediaElement), mediaElement);
                    if (slide.Interaction(mediaElement.Id) is { } action
                        && built.Descendants<P.NonVisualDrawingProperties>().FirstOrDefault() is { } drawing)
                        interactions.Apply(drawing, action, part, session);
                    tree.Append(built);
                    nodes.Remember(mediaElement.Id, built);
                    break;
                }
                case ObjectElement objectElement:
                    var decomposed = decomposition.Decompose(objectElement);
                    nodes.Declare(decomposed.Groups);
                    foreach (var piece in decomposed.Leaves)
                    {
                        var id = shapeId++;
                        if (objects.FirstOrDefault(writer => writer.CanWrite(piece.Content))?.Write(piece, id, part) is not { } written) continue;
                        if (piece.Content is PulseOffice.Drawing.Shapes.ShapeObject linked) _shapeRuns.Write(written, linked, new PptxSlideRunLinks(part, session));
                        if (shapes.TryAdd(objectElement.Id, id) && slide.Interaction(objectElement.Id) is { } action
                            && written.Descendants<P.NonVisualDrawingProperties>().FirstOrDefault() is { } drawing)
                            interactions.Apply(drawing, action, part, session);
                        var placed = _visibility.Write(_rotation.Write(written, piece), objectElement);
                        tree.Append(placed);
                        nodes.Remember(piece.Id, placed);
                    }
                    break;
            }
        }

        foreach (var chrome in _headerFooter.Shapes(session.Deck.HeaderFooter, slide, session.Deck.IndexOf(slide) + 1, session.Deck.Size, shapeId, session.Deck.TextLanguage))
            tree.Append(chrome);
        nodes.Declare(slide.Groups);
        _groups.Wrap(nodes);

        var common = slide.Background is null && slide.BackgroundImage is null && slide.BackgroundFill is null && slide.ShowMasterBackground
            ? new P.CommonSlideData(tree)
            : new P.CommonSlideData(Background(session.Deck, slide, part), tree);
        if (slide.Name is { Length: > 0 } name) common.Name = name;

        part.Slide = new P.Slide(common, new P.ColorMapOverride(new D.MasterColorMapping()));
        if (slide.Hidden) part.Slide.Show = false;
        if (!slide.ShowMasterObjects) part.Slide.ShowMasterShapes = false;
        transitions.Append(part.Slide, slide.Transition);
        if (_sounds.Write(slide.Transition.SoundPath, part, session) is { } transitionSound)
            foreach (var transition in part.Slide.Descendants<P.Transition>())
                transition.Append(_sounds.Transition(transitionSound, slide.Transition.LoopSoundUntilNext));
        if (animations.ToTiming(slide, shapes, path => _sounds.Write(path, part, session)) is { } timing) part.Slide.Append(timing);

        part.AddPart(layout);
        comments.Write(part, session.Part, slide);
        return part;
    }

    private readonly XlsxShapeStyleMapper _fills = new();

    private P.Background Background(PulseOffice.Presentations.Presentation deck, Slide slide, SlidePart part)
    {
        if (slide.BackgroundImage is null && slide.BackgroundFill is { } fill)
            return new P.Background(new P.BackgroundProperties(_fills.Fill(fill), new D.EffectList()));

        if (slide.BackgroundImage is not { } image)
            return new P.Background(new P.BackgroundProperties(
                new D.SolidFill(Opaque(slide.Background ?? 0xFFFFFFFFu)),
                new D.EffectList()));

        var imagePart = part.AddImagePart(_imageTypes.PartOf(image.MediaType));
        using (var stream = imagePart.GetStream(FileMode.Create)) stream.Write(image.Data.Span);

        var blip = new D.Blip { Embed = part.GetIdOfPart(imagePart) };
        _duotones.Write(blip, image.Duotone);
        if (slide.BackgroundLayout.Opacity < 1) blip.Append(new D.AlphaModulationFixed { Amount = (int)Math.Round(Math.Max(slide.BackgroundLayout.Opacity, 0) * 100000) });
        return new P.Background(new P.BackgroundProperties(
            slide.BackgroundLayout.Mode is BackgroundImageMode.Tile
                ? new D.BlipFill(blip, new D.Tile { HorizontalRatio = 100000, VerticalRatio = 100000, Alignment = D.RectangleAlignmentValues.TopLeft, Flip = D.TileFlipValues.None })
                : new D.BlipFill(blip, new D.Stretch(new D.FillRectangle())),
            new D.EffectList()));
    }

    private D.RgbColorModelHex Opaque(uint argb)
    {
        var colour = new D.RgbColorModelHex { Val = units.Hex(argb) };
        if (argb >> 24 is var alpha and < 255) colour.AppendChild(new D.Alpha { Val = (int)Math.Round(alpha / 255.0 * 100000) });
        return colour;
    }
}
