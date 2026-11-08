using System.Globalization;
using System.Text;
using PulseOffice.Presentations;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

public sealed record PptxMediaPlayback(bool Loop, bool Muted, double Volume)
{
    public static readonly PptxMediaPlayback Default = new(false, false, 1);
}

public sealed class PptxMediaTiming
{
    private const double FullVolume = 100000;
    private const double SchemaDefaultVolume = 50000;

    public string Nodes(Slide slide, IReadOnlyDictionary<string, uint> spidByElement, int firstId)
    {
        ArgumentNullException.ThrowIfNull(slide);
        var xml = new StringBuilder();
        var id = firstId;
        foreach (var media in slide.Elements.OfType<MediaElement>().Where(media => media.HasPlaybackSettings))
        {
            if (!spidByElement.TryGetValue(media.Id, out var spid)) continue;
            var kind = media.Kind == MediaKind.Video ? "video" : "audio";
            var volume = (int)Math.Round(Math.Clamp(media.Volume, 0, 1) * FullVolume);
            xml.Append(CultureInfo.InvariantCulture,
                $"<p:{kind}><p:cMediaNode vol=\"{volume}\"{(media.Muted ? " mute=\"1\"" : string.Empty)}>");
            xml.Append(CultureInfo.InvariantCulture,
                $"<p:cTn id=\"{id++}\"{(media.Loop ? " repeatCount=\"indefinite\"" : string.Empty)} fill=\"hold\" display=\"0\">");
            xml.Append("<p:stCondLst><p:cond delay=\"indefinite\"/></p:stCondLst></p:cTn>");
            xml.Append(CultureInfo.InvariantCulture, $"<p:tgtEl><p:spTgt spid=\"{spid}\"/></p:tgtEl></p:cMediaNode></p:{kind}>");
        }
        return xml.ToString();
    }

    public PptxMediaPlayback Read(P.Timing? timing, uint spid)
    {
        var node = timing?.Descendants<P.CommonMediaNode>()
            .FirstOrDefault(media => media.TargetElement?.ShapeTarget?.ShapeId?.Value == spid.ToString(CultureInfo.InvariantCulture));
        if (node is null) return PptxMediaPlayback.Default;
        return new PptxMediaPlayback(
            node.CommonTimeNode?.RepeatCount?.Value == "indefinite",
            node.Mute?.Value == true,
            (node.Volume?.Value ?? SchemaDefaultVolume) / FullVolume);
    }
}
