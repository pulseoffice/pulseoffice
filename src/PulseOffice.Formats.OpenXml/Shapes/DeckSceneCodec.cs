using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Scenes;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Presentations;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class DeckSceneCodec(XlsxShapeStyleMapper styles) : DeckElementBuilder
{
    public const string Name = "DeckScene";
    public const string Uri = "{8E2F4A6B-1C3D-4B5E-8F70-9A1B2C3D4E5F}";
    public const string Namespace = "urn:pulseoffice:scene3d";

    private const string DrawingNamespace = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private const string Prefix = "ps";
    private const string Caption = "3D";

    private readonly ShapeStyle _placeholder = ShapeStyle.Default with
    {
        Fill = ShapeFill.Default with { ColorHex = "#E2E8F0" },
        Line = ShapeLine.Default with { ColorHex = "#64748B", Dash = ShapeStrokeStyle.Dashed },
        Text = ShapeTextStyle.Default with { ColorHex = "#334155", Size = 18, Bold = true },
    };

    public override bool Builds(EmbeddedObject content) => content is Scene3DObject;

    public override OpenXmlElement Build(ObjectElement element, uint id, SlidePart part)
    {
        var scene = (Scene3DObject)element.Content;
        var properties = new P.ShapeProperties(Transform(element), new D.PresetGeometry(new D.AdjustValueList()) { Preset = D.ShapeTypeValues.Rectangle });
        properties.Append(styles.Fill(_placeholder.Fill));
        properties.Append(styles.Line(_placeholder.Line));

        var value = new OpenXmlUnknownElement(Prefix, "scene", Namespace);
        value.SetAttribute(new OpenXmlAttribute("format", string.Empty, scene.Format));
        value.AppendChild(Text("markup", scene.Markup));
        foreach (var style in scene.Styles) value.AppendChild(Text("style", style));
        var extension = new OpenXmlUnknownElement("a", "ext", DrawingNamespace);
        extension.SetAttribute(new OpenXmlAttribute("uri", string.Empty, Uri));
        extension.AppendChild(value);
        properties.Append(new D.ShapePropertiesExtensionList(extension));

        var paragraph = new D.Paragraph(styles.Paragraph(_placeholder.Text), new D.Run(styles.Run(_placeholder.Text), new D.Text(Caption)));
        return new P.Shape(ShapeNames(id, Name), properties, new P.TextBody(styles.Body(_placeholder.Text), new D.ListStyle(), paragraph));
    }

    public ObjectElement? Read(P.Shape shape, string id)
    {
        var value = shape.ShapeProperties?.ChildElements.FirstOrDefault(child => child.LocalName == "extLst")?
            .Descendants().FirstOrDefault(element => element.LocalName == "scene" && element.NamespaceUri == Namespace);
        if (value is null) return null;
        var markup = Value(value.ChildElements.FirstOrDefault(child => child.LocalName == "markup"));
        if (string.IsNullOrEmpty(markup)) return null;
        return new ObjectElement
        {
            Id = id,
            Bounds = Bounds(shape.ShapeProperties?.Transform2D),
            Content = new Scene3DObject
            {
                Id = id,
                Format = value.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "format").Value ?? Scene3DObject.OdfFormat,
                Markup = markup,
                Styles = [.. value.ChildElements.Where(child => child.LocalName == "style").Select(Value).OfType<string>()],
            },
        };
    }

    private string? Value(OpenXmlElement? element) =>
        element?.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "val").Value;

    private OpenXmlUnknownElement Text(string name, string text)
    {
        var element = new OpenXmlUnknownElement(Prefix, name, Namespace);
        element.SetAttribute(new OpenXmlAttribute("val", string.Empty, text));
        return element;
    }
}
