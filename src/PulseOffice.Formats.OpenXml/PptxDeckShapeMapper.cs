using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Presentations;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

public sealed class PptxDeckShapeMapper
{
    private readonly DeckPresetShapeCodec _shapes;
    private readonly DeckFreeformReader _freeforms;
    private readonly DeckFormulaShapeCodec _formulas = new(new DrawingML.DrawingPlainText());
    private readonly DeckPictureCodec _pictures = new(new OoxmlImageTypes(), new DrawingML.DrawingDuotone());
    private readonly DeckOleObjectCodec _ole = new();
    private readonly IReadOnlyList<DeckElementBuilder> _builders;
    private readonly DeckFontworkCodec _fontwork;
    private readonly DeckConnectionShapeReader _connections;
    private readonly DeckVectorPictureCodec _vectors;
    private readonly DeckSceneCodec _scenes;
    private readonly DrawingML.DrawingAspectLock _aspect = new();

    public PptxDeckShapeMapper() : this(new PulseOffice.Formats.VectorFallback())
    {
    }

    public PptxDeckShapeMapper(PulseOffice.Formats.VectorFallback fallback)
    {
        _vectors = new DeckVectorPictureCodec(fallback, new DrawingML.OoxmlSvgBlip());
        var styles = new XlsxShapeStyleMapper();
        _shapes = new DeckPresetShapeCodec(styles, new ShapePresets());
        _freeforms = new DeckFreeformReader(styles);
        _fontwork = new DeckFontworkCodec(styles);
        _connections = new DeckConnectionShapeReader(styles, new ConnectorPresetPaths(), new OoxmlConnectionSites());
        _scenes = new DeckSceneCodec(styles);
        _builders =
        [
            _scenes, _shapes, _pictures, _vectors, _formulas, _fontwork,
            new DeckGeometryCodec<FreeformObject>(styles, "DeckFreeform", freeform => freeform.Path.ToOutline(), freeform => freeform.Style),
            new DeckConnectorCodec(styles),
        ];
    }

    public OpenXmlElement? Build(ObjectElement element, uint id, SlidePart part) =>
        _builders.FirstOrDefault(builder => builder.Builds(element.Content))?.Build(element, id, part) is { } built ? _aspect.Mark(built, element.Content) : null;

    public bool IsDecoration(ObjectElement element) => element.Content is ShapeObject;

    public OpenXmlElement BuildDecoration(ObjectElement element, uint id) => _aspect.Mark(_shapes.Build(element, id), element.Content);

    public ObjectElement? ReadGeometry(P.Shape shape, string id) => Locked(_freeforms.Read(shape, id), shape);

    public ObjectElement? ReadShape(P.Shape shape, string id) => Locked(_shapes.Read(shape, id), shape);

    public ObjectElement? ReadScene(P.Shape shape, string id) => Locked(_scenes.Read(shape, id) ?? _shapes.Read(shape, id), shape);

    public ObjectElement? ReadConnection(P.ConnectionShape connection, string id) => Locked(_connections.Read(connection, id), connection);

    public bool IsFontwork(P.Shape shape) => _fontwork.Reads(shape);

    public ObjectElement ReadFontwork(P.Shape shape, string id) => Locked(_fontwork.Read(shape, id), shape)!;

    public ObjectElement? ReadFormula(P.Shape shape, string id) => Locked(_formulas.Read(shape, id), shape);

    public ObjectElement? ReadImage(P.Picture picture, SlidePart part, string id) =>
        Locked(_vectors.Read(picture, part, id) ?? _pictures.Read(picture, part, id), picture);

    private ObjectElement? Locked(ObjectElement? element, OpenXmlElement source) =>
        element is null ? null : element with { Content = _aspect.Apply(element.Content, source) };

    public P.GraphicFrame BuildOle(ObjectElement element, PackagedObject package, uint id, SlidePart part) => _ole.Build(element, package, id, part);

    public ObjectElement? ReadOle(P.GraphicFrame frame, SlidePart part, string id) => _ole.Read(frame, part, id);
}
