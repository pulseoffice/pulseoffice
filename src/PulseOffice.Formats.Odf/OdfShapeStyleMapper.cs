using System.Xml.Linq;
using PulseOffice.Drawing.Shapes;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

public sealed class OdfShapeStyleMapper
{
    private readonly GraphicDefinitions _definitions = new();
    private readonly OdfShapeVocabulary _vocabulary = new();
    private readonly OdfMeasure _measure = new();
    private readonly OdfShapeStyleReader _reader = new();
    private readonly ShapeTextPropertiesBuilder _text;
    private readonly IReadOnlyList<GraphicPropertyWriter> _writers;
    private readonly FillPropertyWriter _fill = new();

    public OdfShapeStyleMapper()
    {
        _text = new ShapeTextPropertiesBuilder(_measure);
        _writers =
        [
            _fill, new LinePropertyWriter(new ArrowMarkers()), new ShadowPropertyWriter(),
            new GlowPropertyWriter(), new SoftEdgePropertyWriter(), new BevelPropertyWriter(), new TextAreaPropertyWriter(),
        ];
    }

    public IReadOnlyList<XElement> Definitions => _definitions.Items;

    public XElement Build(string name, ShapeStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        var graphic = new XElement(Namespaces.Style + "graphic-properties");
        var context = new GraphicWriteContext(graphic, name, _definitions, _vocabulary, _measure);
        foreach (var writer in _writers) writer.Write(context, style);

        return new XElement(Namespaces.Style + "style",
            new XAttribute(Namespaces.Style + "name", name),
            new XAttribute(Namespaces.Style + "family", "graphic"),
            graphic,
            new XElement(Namespaces.Style + "paragraph-properties",
                new XAttribute(Namespaces.Fo + "text-align", _vocabulary.ParagraphAlignment.Token(style.Text.Horizontal))),
            _text.Build(style.Text));
    }

    public void WriteFill(XElement properties, string name, ShapeFill fill)
    {
        ArgumentNullException.ThrowIfNull(fill);
        _fill.Write(new GraphicWriteContext(properties, name, _definitions, _vocabulary, _measure), ShapeStyle.Default with { Fill = fill });
    }

    public ShapeFill ReadFill(XElement properties, IReadOnlyDictionary<string, XElement> definitions) =>
        _reader.ReadFill(properties, definitions);

    public ShapeStyle Read(XElement? style, IReadOnlyDictionary<string, XElement> definitions) =>
        _reader.Read(style, definitions);
}
