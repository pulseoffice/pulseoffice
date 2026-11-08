using DocumentFormat.OpenXml.Packaging;
using SVG = DocumentFormat.OpenXml.Office2019.Drawing.SVG;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.DrawingML;

public sealed class OoxmlSvgBlip
{
    private const string Extension = "{96DAC541-7B7A-43D3-8B79-37D633B846F1}";

    public byte[]? Svg(D.Blip blip, OpenXmlPart owner)
    {
        ArgumentNullException.ThrowIfNull(blip);
        ArgumentNullException.ThrowIfNull(owner);
        var reference = blip.Descendants<SVG.SVGBlip>().FirstOrDefault()?.Embed?.Value
            ?? blip.Descendants().FirstOrDefault(element => element.LocalName == "svgBlip")
                ?.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "embed").Value;
        if (string.IsNullOrEmpty(reference)) return null;
        OpenXmlPart? part;
        try { part = owner.GetPartById(reference); }
        catch (ArgumentOutOfRangeException) { return null; }
        if (part is null) return null;
        using var stream = part.GetStream();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    public D.Blip Blip(OpenXmlPart owner, byte[] svg, byte[] png)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(svg);
        ArgumentNullException.ThrowIfNull(png);
        var fallback = owner.AddNewPart<ImagePart>("image/png", null);
        using (var stream = fallback.GetStream(FileMode.Create)) stream.Write(png);
        var vector = owner.AddNewPart<ImagePart>("image/svg+xml", null);
        using (var stream = vector.GetStream(FileMode.Create)) stream.Write(svg);

        var extension = new D.BlipExtension { Uri = Extension };
        extension.Append(new SVG.SVGBlip { Embed = owner.GetIdOfPart(vector) });
        return new D.Blip(new D.BlipExtensionList(extension)) { Embed = owner.GetIdOfPart(fallback) };
    }
}
