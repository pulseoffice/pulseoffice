using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class PptxDiagramExpander(Diagrams.IDiagramDrawingGenerator generator)
{
    public const string DiagramUri = "http://schemas.openxmlformats.org/drawingml/2006/diagram";

    private static readonly XNamespace Drawing = "http://schemas.microsoft.com/office/drawing/2008/diagram";
    private static readonly XNamespace Presentation = "http://schemas.openxmlformats.org/presentationml/2006/main";

    private readonly PptxDiagramRoles _roles = new();
    private readonly SafeXmlLoader _xml = new();

    public IReadOnlyList<PptxDiagramSource> Expand(SlidePart slide)
    {
        var tree = slide.Slide?.CommonSlideData?.ShapeTree;
        if (tree is null) return [];
        var expanded = new List<PptxDiagramSource>();
        var nextId = new DiagramShapeIds(tree.Descendants<P.NonVisualDrawingProperties>().Select(drawing => drawing.Id?.Value ?? 0));

        foreach (var frame in tree.Descendants<P.GraphicFrame>().Where(IsDiagram).ToList())
        {
            var drawing = DrawingPart(slide, frame);
            var shapes = drawing is not null ? Shapes(drawing) : Generated(slide, frame);
            if (shapes.Count == 0) continue;
            var associations = Associations(slide, frame);
            var points = shapes.Select(shape => (string?)shape.Attribute("modelId") is { } model ? associations.GetValueOrDefault(model, model) : null).ToList();
            var group = Group(frame, shapes, nextId);
            var members = group.Descendants<P.NonVisualDrawingProperties>().Where(item => item.Parent is P.NonVisualShapeProperties).Select(item => item.Id!.Value).ToList();
            var source = new PptxDiagramSource(frame.OuterXml, group.NonVisualGroupShapeProperties!.NonVisualDrawingProperties!.Id!.Value,
                members, Parts(slide, frame, drawing))
            {
                PointsByMember = members.Count == points.Count
                    ? members.Zip(points).Where(pair => pair.Second is not null).ToDictionary(pair => pair.First, pair => pair.Second!)
                    : new Dictionary<uint, string>(),
            };
            frame.Parent!.ReplaceChild(group, frame);
            expanded.Add(source);
        }
        return expanded;
    }

    private Dictionary<string, string> Associations(SlidePart slide, P.GraphicFrame frame)
    {
        var relIds = frame.Graphic!.GraphicData!.ChildElements.FirstOrDefault(child => child.LocalName == "relIds");
        if (relIds?.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == PptxDiagramRoles.Data).Value is not { } id
            || !slide.TryGetPartById(id, out var part) || LoadPart(part) is not { } data) return [];
        return data.Descendants().Where(element => element.Name.LocalName == "pt" && (string?)element.Attribute("type") == "pres")
            .Select(element => (Id: (string?)element.Attribute("modelId"), Associated: (string?)element.Elements().FirstOrDefault(child => child.Name.LocalName == "prSet")?.Attribute("presAssocID")))
            .Where(pair => pair.Id is not null && pair.Associated is not null)
            .GroupBy(pair => pair.Id!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Associated!, StringComparer.Ordinal);
    }

    private List<XElement> Generated(SlidePart slide, P.GraphicFrame frame)
    {
        var relIds = frame.Graphic!.GraphicData!.ChildElements.FirstOrDefault(child => child.LocalName == "relIds");
        XDocument? Load(string role) =>
            relIds?.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == role).Value is { } id && slide.TryGetPartById(id, out var part)
                ? LoadPart(part)
                : null;
        if (Load(PptxDiagramRoles.Data) is not { } data || Load(PptxDiagramRoles.Layout) is not { } layout) return [];
        var extents = frame.Transform?.Extents;
        return generator.Generate(new Diagrams.DiagramSources(data, layout, Load(PptxDiagramRoles.Colors), Load(PptxDiagramRoles.Style)),
            extents?.Cx?.Value ?? 0, extents?.Cy?.Value ?? 0);
    }

    private XDocument? LoadPart(OpenXmlPart part)
    {
        try
        {
            using var stream = part.GetStream();
            return _xml.Document(stream);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private IReadOnlyList<PulseOffice.Presentations.NativeDiagramPart> Parts(SlidePart slide, P.GraphicFrame frame, OpenXmlPart? drawing)
    {
        var relIds = frame.Graphic!.GraphicData!.ChildElements.FirstOrDefault(child => child.LocalName == "relIds");
        var parts = new List<PulseOffice.Presentations.NativeDiagramPart>();
        foreach (var role in _roles.Related)
            if (relIds?.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == role).Value is { } id && slide.TryGetPartById(id, out var part))
                parts.Add(Copy(role, part));
        if (drawing is not null) parts.Add(Copy(PptxDiagramRoles.Drawing, drawing));
        return parts;
    }

    private PulseOffice.Presentations.NativeDiagramPart Copy(string role, OpenXmlPart part)
    {
        using var stream = part.GetStream();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return new PulseOffice.Presentations.NativeDiagramPart(role, part.ContentType, buffer.ToArray());
    }

    private bool IsDiagram(P.GraphicFrame frame) =>
        frame.Graphic?.GraphicData?.Uri?.Value == DiagramUri;

    private OpenXmlPart? DrawingPart(SlidePart slide, P.GraphicFrame frame)
    {
        var relIds = frame.Graphic!.GraphicData!.ChildElements.FirstOrDefault(child => child.LocalName == "relIds");
        var dataId = relIds?.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "dm").Value;
        if (dataId is not null && slide.TryGetPartById(dataId, out var data) && LoadPart(data) is { } document)
        {
            var relId = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "dataModelExt")?.Attribute("relId")?.Value;
            if (relId is not null && slide.TryGetPartById(relId, out var drawing)) return drawing;
        }
        var drawings = slide.Parts.Select(pair => pair.OpenXmlPart).Where(part => part.ContentType == "application/vnd.ms-office.drawingml.diagramDrawing+xml").ToList();
        return drawings.Count == 1 && slide.Slide!.Descendants<P.GraphicFrame>().Count(IsDiagram) == 1 ? drawings[0] : null;
    }

    private List<XElement> Shapes(OpenXmlPart drawing)
    {
        var tree = LoadPart(drawing)?.Root?.Element(Drawing + "spTree");
        return tree is null ? [] : [.. tree.Elements().Where(element => element.Name == Drawing + "sp" || element.Name == Drawing + "grpSp")];
    }

    private P.GroupShape Group(P.GraphicFrame frame, List<XElement> shapes, DiagramShapeIds nextId)
    {
        var offset = frame.Transform?.Offset;
        var extents = frame.Transform?.Extents;
        var group = new P.GroupShape(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = frame.NonVisualGraphicFrameProperties?.NonVisualDrawingProperties?.Id?.Value ?? nextId.Next(), Name = frame.NonVisualGraphicFrameProperties?.NonVisualDrawingProperties?.Name?.Value ?? "Diagram" },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new D.TransformGroup(
                new D.Offset { X = offset?.X?.Value ?? 0, Y = offset?.Y?.Value ?? 0 },
                new D.Extents { Cx = extents?.Cx?.Value ?? 0, Cy = extents?.Cy?.Value ?? 0 },
                new D.ChildOffset { X = 0, Y = 0 },
                new D.ChildExtents { Cx = extents?.Cx?.Value ?? 0, Cy = extents?.Cy?.Value ?? 0 })));

        foreach (var shape in shapes)
            if (Convert(shape, nextId) is { } converted) group.Append(converted);
        return group;
    }

    private OpenXmlElement? Convert(XElement source, DiagramShapeIds nextId)
    {
        var copy = new XElement(source);
        copy.Elements(Drawing + "txXfrm").Remove();
        foreach (var element in copy.DescendantsAndSelf().Where(element => element.Name.Namespace == Drawing).ToList())
            element.Name = Presentation + element.Name.LocalName;
        foreach (var element in copy.DescendantsAndSelf().ToList())
            element.Attributes().Where(attribute => attribute.Name.LocalName == "modelId" && attribute.Name.Namespace == XNamespace.None).Remove();

        foreach (var properties in copy.DescendantsAndSelf().Where(element => element.Name.LocalName is "nvSpPr" or "nvGrpSpPr" && element.Name.Namespace == Presentation))
        {
            if (properties.Element(Presentation + "nvPr") is null) properties.Add(new XElement(Presentation + "nvPr"));
            if (properties.Element(Presentation + "cNvPr") is { } drawing) drawing.SetAttributeValue("id", nextId.Next());
        }

        var xml = copy.ToString(SaveOptions.DisableFormatting);
        return copy.Name.LocalName == "grpSp" ? new P.GroupShape(xml) : new P.Shape(xml);
    }
}

internal sealed class PptxDiagramRoles
{
    public const string Data = "dm";
    public const string Layout = "lo";
    public const string Style = "qs";
    public const string Colors = "cs";
    public const string Drawing = "drawing";

    public IReadOnlyList<string> Related { get; } = [Data, Layout, Style, Colors];
}

internal sealed class DiagramShapeIds
{
    private readonly HashSet<uint> _used;
    private uint _candidate;

    public DiagramShapeIds(IEnumerable<uint> used)
    {
        _used = [.. used];
        var highest = _used.DefaultIfEmpty(0u).Max();
        _candidate = highest == uint.MaxValue ? 1 : highest + 1;
    }

    public uint Next()
    {
        while (_candidate == 0 || _used.Contains(_candidate)) _candidate++;
        _used.Add(_candidate);
        return _candidate;
    }
}

internal sealed record PptxDiagramSource(string FrameXml, uint GroupId, IReadOnlyList<uint> MemberIds, IReadOnlyList<PulseOffice.Presentations.NativeDiagramPart> Parts)
{
    public IReadOnlyDictionary<uint, string> PointsByMember { get; init; } = new Dictionary<uint, string>();
}
