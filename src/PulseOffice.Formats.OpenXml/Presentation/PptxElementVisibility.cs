using DocumentFormat.OpenXml;
using PulseOffice.Presentations;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class PptxElementVisibility
{
    public T Write<T>(T node, SlideElement element) where T : OpenXmlElement
    {
        if (element.Hidden && Drawing(node) is { } drawing) drawing.Hidden = true;
        return node;
    }

    public bool Hidden(OpenXmlElement node) => Drawing(node)?.Hidden?.Value == true;

    public void Inherit(OpenXmlElement child)
    {
        if (Drawing(child) is { } drawing) drawing.Hidden = true;
    }

    private P.NonVisualDrawingProperties? Drawing(OpenXmlElement node) =>
        node is P.NonVisualDrawingProperties own ? own : node.Descendants<P.NonVisualDrawingProperties>().FirstOrDefault();
}

internal sealed class PptxHiddenElementReader(PptxElementVisibility visibility) : IPptxSlidePartReader
{
    public void Read(PptxSlideSource source, SlideAssembly<uint> assembly)
    {
        foreach (var drawing in source.Tree?.Descendants<P.NonVisualDrawingProperties>() ?? [])
        {
            if (!visibility.Hidden(drawing) || drawing.Id?.Value is not { } spid
                || !assembly.ElementByFrame.TryGetValue(spid, out var elementId)
                || assembly.Slide.Find(elementId) is not { Hidden: false } element)
                continue;
            assembly.Slide.Replace(element with { Hidden = true });
        }
    }
}
