using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Presentations;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class PptxDiagramWriter(Diagrams.IDiagramDrawingGenerator generator)
{
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Diagram = "http://schemas.openxmlformats.org/drawingml/2006/diagram";
    private static readonly XNamespace Drawing = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace Shapes = "http://schemas.microsoft.com/office/drawing/2008/diagram";

    private readonly PptxDiagramRoles _roles = new();
    private readonly PptxDiagramDataProbe _probe = new();

    public P.GraphicFrame Write(NativeDiagram diagram, uint id, SlidePart slide, IReadOnlyDictionary<string, IReadOnlyList<string>> edits)
    {
        ArgumentNullException.ThrowIfNull(diagram);
        ArgumentNullException.ThrowIfNull(edits);
        if (edits.Count == 0 || diagram.Parts.FirstOrDefault(part => part.Role == PptxDiagramRoles.Data) is not { } data
            || _probe.Document(data.Data) is not { } model) return Write(diagram, id, slide);
        foreach (var (point, paragraphs) in edits)
            if (model.Root?.Element(Diagram + "ptLst")?.Elements(Diagram + "pt").FirstOrDefault(element => (string?)element.Attribute("modelId") == point) is { } element)
                Retext(element, paragraphs);
        var parts = diagram.Parts.Where(part => part.Role is not (PptxDiagramRoles.Data or PptxDiagramRoles.Drawing)).ToList();
        parts.Add(data with { Data = Save(model) });
        var frame = new P.GraphicFrame(diagram.FrameXml);
        var extents = frame.Transform?.Extents;
        XDocument? Part(string role) => diagram.Parts.FirstOrDefault(part => part.Role == role) is { } found ? _probe.Document(found.Data) : null;
        if (Part(PptxDiagramRoles.Layout) is { } layout)
        {
            var shapes = generator.Generate(new Diagrams.DiagramSources(model, layout, Part(PptxDiagramRoles.Colors), Part(PptxDiagramRoles.Style)),
                extents?.Cx?.Value ?? 0, extents?.Cy?.Value ?? 0);
            if (shapes.Count > 0)
                parts.Add(new NativeDiagramPart(PptxDiagramRoles.Drawing, "application/vnd.ms-office.drawingml.diagramDrawing+xml", Save(new XDocument(
                    new XElement(Shapes + "drawing", new XAttribute(XNamespace.Xmlns + "dsp", Shapes.NamespaceName), new XAttribute(XNamespace.Xmlns + "a", Drawing.NamespaceName),
                        new XElement(Shapes + "spTree",
                            new XElement(Shapes + "nvGrpSpPr", new XElement(Shapes + "cNvPr", new XAttribute("id", "0"), new XAttribute("name", string.Empty)), new XElement(Shapes + "cNvGrpSpPr")),
                            new XElement(Shapes + "grpSpPr"),
                            shapes))))));
        }
        return Write(diagram with { Parts = parts }, id, slide);
    }

    private void Retext(XElement point, IReadOnlyList<string> paragraphs)
    {
        var text = point.Element(Diagram + "t");
        if (text is null) point.Add(text = new XElement(Diagram + "t", new XElement(Drawing + "bodyPr"), new XElement(Drawing + "lstStyle")));
        var original = text.Elements(Drawing + "p").ToList();
        var runProperties = original.SelectMany(paragraph => paragraph.Elements(Drawing + "r")).Select(run => run.Element(Drawing + "rPr")).FirstOrDefault(found => found is not null);
        foreach (var paragraph in original) paragraph.Remove();
        for (var index = 0; index < paragraphs.Count; index++)
        {
            var paragraph = new XElement(Drawing + "p");
            if (index < original.Count && original[index].Element(Drawing + "pPr") is { } properties) paragraph.Add(new XElement(properties));
            if (paragraphs[index].Length > 0)
                paragraph.Add(new XElement(Drawing + "r", runProperties is null ? new XElement(Drawing + "rPr") : new XElement(runProperties), new XElement(Drawing + "t", paragraphs[index])));
            text.Add(paragraph);
        }
    }

    private byte[] Save(XDocument document)
    {
        using var output = new MemoryStream();
        document.Save(output, SaveOptions.DisableFormatting);
        return output.ToArray();
    }

    public P.GraphicFrame Write(NativeDiagram diagram, uint id, SlidePart slide)
    {
        var frame = new P.GraphicFrame(diagram.FrameXml);
        if (frame.NonVisualGraphicFrameProperties?.NonVisualDrawingProperties is { } drawing) drawing.Id = id;

        var identifiers = new Dictionary<string, string>(StringComparer.Ordinal);
        if (diagram.Parts.FirstOrDefault(part => part.Role == PptxDiagramRoles.Drawing) is { } persisted)
            identifiers[PptxDiagramRoles.Drawing] = Add(slide.AddNewPart<DiagramPersistLayoutPart>(), persisted.Data, slide);

        foreach (var part in diagram.Parts.Where(part => part.Role != PptxDiagramRoles.Drawing))
        {
            OpenXmlPart created = part.Role switch
            {
                PptxDiagramRoles.Data => slide.AddNewPart<DiagramDataPart>(),
                PptxDiagramRoles.Layout => slide.AddNewPart<DiagramLayoutDefinitionPart>(),
                PptxDiagramRoles.Style => slide.AddNewPart<DiagramStylePart>(),
                _ => slide.AddNewPart<DiagramColorsPart>(),
            };
            var data = part.Role == PptxDiagramRoles.Data && identifiers.TryGetValue(PptxDiagramRoles.Drawing, out var drawingId)
                ? Relinked(part.Data, drawingId)
                : part.Data;
            identifiers[part.Role] = Add(created, data, slide);
        }

        var relIds = frame.Graphic?.GraphicData?.ChildElements.FirstOrDefault(child => child.LocalName == "relIds");
        if (relIds is not null)
            foreach (var role in _roles.Related)
                if (identifiers.TryGetValue(role, out var identifier))
                    relIds.SetAttribute(new OpenXmlAttribute("r", role, Relationships.NamespaceName, identifier));
        return frame;
    }

    private string Add(OpenXmlPart part, byte[] data, SlidePart slide)
    {
        using (var stream = part.GetStream(FileMode.Create)) stream.Write(data);
        return slide.GetIdOfPart(part);
    }

    private byte[] Relinked(byte[] data, string drawingId)
    {
        if (_probe.Document(data) is not { } document) return data;
        foreach (var extension in document.Descendants().Where(element => element.Name.LocalName == "dataModelExt"))
            extension.SetAttributeValue("relId", drawingId);
        using var output = new MemoryStream();
        document.Save(output, SaveOptions.DisableFormatting);
        return output.ToArray();
    }
}

internal sealed class PptxDiagramDataProbe
{
    private readonly SafeXmlLoader _xml = new();

    public XDocument? Document(byte[] data)
    {
        try
        {
            using var stream = new MemoryStream(data);
            return _xml.Document(stream);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    public bool LosesEdits(NativeDiagram diagram) =>
        diagram.Parts.FirstOrDefault(part => part.Role == PptxDiagramRoles.Data) is { } data && Document(data.Data) is null;
}
