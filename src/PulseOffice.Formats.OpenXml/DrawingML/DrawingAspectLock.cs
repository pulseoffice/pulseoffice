using DocumentFormat.OpenXml;
using PulseOffice.Document.Embedding;
using D = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.DrawingML;

internal sealed class DrawingAspectLock
{
    public TContent Apply<TContent>(TContent content, OpenXmlElement source) where TContent : EmbeddedObject =>
        (TContent)content.WithAspectLock(Locked(source));

    public bool Locked(OpenXmlElement source) =>
        source.Descendants<D.PictureLocks>().FirstOrDefault()?.NoChangeAspect?.Value == true
        || source.Descendants<D.ShapeLocks>().FirstOrDefault()?.NoChangeAspect?.Value == true
        || source.Ancestors().FirstOrDefault(ancestor => ancestor is DW.Inline or DW.Anchor) is { } frame && FrameLocked(frame);

    public bool FrameLocked(OpenXmlElement frame) =>
        frame.GetFirstChild<DW.NonVisualGraphicFrameDrawingProperties>()?.GetFirstChild<D.GraphicFrameLocks>()?.NoChangeAspect?.Value == true;

    public TElement Mark<TElement>(TElement built, EmbeddedObject content) where TElement : OpenXmlElement
    {
        if (!content.LocksAspectRatio || built.Descendants().FirstOrDefault(node => node.LocalName == "cNvSpPr") is not { } shapeProperties) return built;
        if (shapeProperties.GetFirstChild<D.ShapeLocks>() is { } existing) existing.NoChangeAspect = true;
        else shapeProperties.PrependChild(new D.ShapeLocks { NoChangeAspect = true });
        return built;
    }

    public void MarkByName(OpenXmlElement drawing, Func<string, EmbeddedObject?> lookup)
    {
        foreach (var anchor in drawing.ChildElements)
            if (Name(anchor) is { } name && lookup(name) is { } content) Mark(anchor, content);
    }

    public IReadOnlySet<string> LockedNames(OpenXmlElement drawing) =>
        drawing.ChildElements
            .Where(anchor => !anchor.Descendants().Any(node => node.LocalName == "grpSp") && Locked(anchor))
            .Select(Name).OfType<string>().ToHashSet(StringComparer.Ordinal);

    private static string? Name(OpenXmlElement anchor) =>
        anchor.Descendants().FirstOrDefault(node => node.LocalName == "cNvPr")?.GetAttributes()
            .FirstOrDefault(attribute => attribute.LocalName == "name").Value;

    public D.GraphicFrameLocks? FrameLocks(EmbeddedObject? content) =>
        content?.LocksAspectRatio == true ? new D.GraphicFrameLocks { NoChangeAspect = true } : null;
}
