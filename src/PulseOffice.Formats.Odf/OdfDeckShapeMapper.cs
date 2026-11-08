using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

public sealed class OdfDeckShapeMapper
{
    private readonly DeckPathCodec _paths = new(new ShapePathWriter(), new OdfShapeStyleReader());
    private readonly DeckShapeCodec _shapes;
    private readonly DeckImageCodec _images = new(new OdfImageTypes());
    private readonly DeckFormulaCodec _formulas = new();
    private readonly DeckOleCodec _ole = new();
    private readonly DeckTableCodec _tables;
    private readonly DeckFontworkCodec _fontwork = new(new OdfShapeStyleReader());
    private readonly DeckSceneCodec _scenes = new();

    public OdfDeckShapeMapper()
    {
        var paragraphs = new OdfParagraphText();
        _shapes = new DeckShapeCodec(new ShapePresets(), new OdfShapeStyleReader(), paragraphs);
        _tables = new DeckTableCodec(paragraphs);
    }

    public XElement BuildFontwork(ObjectElement element, PulseOffice.Drawing.Fontwork.FontworkObject fontwork, string styleName) => _fontwork.Build(element, fontwork, styleName);

    public bool IsFontwork(XElement node) => _fontwork.Reads(node);

    public ObjectElement ReadFontwork(XElement node, string id, IReadOnlyDictionary<string, XElement> definitions) => _fontwork.Read(node, id, definitions);

    public XElement BuildPath(ObjectElement element, ShapeOutline outline, string styleName) => _paths.Build(element, outline, styleName);

    public ObjectElement? ReadPath(XElement node, string id, IReadOnlyDictionary<string, XElement>? definitions = null) => _paths.Read(node, id, definitions);

    public XElement BuildShape(ObjectElement element, ShapeObject shape, string styleName, IOdfTextStyleSink textStyles) => _shapes.Build(element, shape, styleName, textStyles);

    public ObjectElement? ReadShape(XElement node, string id, IReadOnlyDictionary<string, XElement> definitions) =>
        _shapes.Read(node, id, definitions);

    public IReadOnlyList<XName> Primitives => DeckShapeCodec.Primitives;

    public XName Scene => _scenes.Element;

    public ObjectElement ReadScene(XElement node, string id, IReadOnlyDictionary<string, XElement> definitions) => _scenes.Read(node, id, definitions);

    public XElement? BuildScene(ObjectElement element, PulseOffice.Drawing.Scenes.Scene3DObject scene, Func<string> nextStyle, Action<XElement> addStyle) =>
        _scenes.Build(element, scene, nextStyle, addStyle);

    public ObjectElement? ReadPrimitive(XElement node, string id, IReadOnlyDictionary<string, XElement> definitions) =>
        _shapes.ReadPrimitive(node, id, definitions);

    public XElement BuildImage(ObjectElement element, string href, string? styleName = null) => _images.Build(element, href, styleName);

    public XElement BuildVector(ObjectElement element, string svgHref, string fallbackHref, string? styleName) =>
        _images.BuildVector(element, svgHref, fallbackHref, styleName);

    public ObjectElement? ReadImage(XElement frame, string id, Func<string, byte[]?> readBytes,
        IReadOnlyDictionary<string, XElement>? definitions = null) => _images.Read(frame, id, readBytes, definitions);

    public XElement BuildFormula(ObjectElement element, FormulaObject formula, string href) => _formulas.Build(element, formula, href);

    public ObjectElement? ReadFormula(XElement frame, string id, Func<string, XDocument?> readXml) => _formulas.Read(frame, id, readXml);

    public XElement BuildOle(ObjectElement element, PackagedObject package, string href, string? replacementHref) => _ole.Build(element, package, href, replacementHref);

    public ObjectElement? ReadOle(XElement frame, string id, Func<string, byte[]?> readBytes) => _ole.Read(frame, id, readBytes);

    public XElement BuildTable(ObjectElement element, DataTableObject table, XElement autostyles) => _tables.Build(element, table, autostyles);

    public ObjectElement? ReadTable(XElement frame, string id, OdfTableStyles styles) =>
        _tables.Read(frame, id, styles);
}
