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
    private readonly DeckFormulaShapeCodec _formulas = new();
    private readonly DeckPictureCodec _pictures = new(new OoxmlImageTypes(), new DrawingML.DrawingDuotone());
    private readonly DeckOleObjectCodec _ole = new();
    private readonly IReadOnlyList<DeckElementBuilder> _builders;
    private readonly DeckFontworkCodec _fontwork;
    private readonly DeckConnectionShapeReader _connections;
    private readonly DeckVectorPictureCodec _vectors;
    private readonly DeckSceneCodec _scenes;

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
        _connections = new DeckConnectionShapeReader(styles, new ConnectorPresetPaths());
        _scenes = new DeckSceneCodec(styles);
        _builders =
        [
            _scenes, _shapes, _pictures, _vectors, _formulas, _fontwork,
            new DeckGeometryCodec<FreeformObject>(styles, "DeckFreeform", freeform => freeform.Path.ToOutline(), freeform => freeform.Style),
            new DeckGeometryCodec<ConnectorObject>(styles, "DeckConnector", connector => connector.Path, connector => connector.Style),
        ];
    }

    public OpenXmlElement? Build(ObjectElement element, uint id, SlidePart part) =>
        _builders.FirstOrDefault(builder => builder.Builds(element.Content))?.Build(element, id, part);

    public bool IsDecoration(ObjectElement element) => element.Content is ShapeObject;

    public OpenXmlElement BuildDecoration(ObjectElement element, uint id) => _shapes.Build(element, id);

    public ObjectElement? ReadGeometry(P.Shape shape, string id) => _freeforms.Read(shape, id);

    public ObjectElement? ReadShape(P.Shape shape, string id) => _shapes.Read(shape, id);

    public ObjectElement? ReadScene(P.Shape shape, string id) => _scenes.Read(shape, id) ?? _shapes.Read(shape, id);

    public ObjectElement? ReadConnection(P.ConnectionShape connection, string id) => _connections.Read(connection, id);

    public bool IsFontwork(P.Shape shape) => _fontwork.Reads(shape);

    public ObjectElement ReadFontwork(P.Shape shape, string id) => _fontwork.Read(shape, id);

    public ObjectElement? ReadFormula(P.Shape shape, string id) => _formulas.Read(shape, id);

    public ObjectElement? ReadImage(P.Picture picture, SlidePart part, string id) =>
        _vectors.Read(picture, part, id) ?? _pictures.Read(picture, part, id);

    public P.GraphicFrame BuildOle(ObjectElement element, PackagedObject package, uint id, SlidePart part) => _ole.Build(element, package, id, part);

    public ObjectElement? ReadOle(P.GraphicFrame frame, SlidePart part, string id) => _ole.Read(frame, part, id);
}
