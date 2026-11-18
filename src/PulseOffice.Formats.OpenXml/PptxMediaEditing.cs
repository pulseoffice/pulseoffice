using System.Globalization;
using DocumentFormat.OpenXml;
using PulseOffice.Presentations;
using P = DocumentFormat.OpenXml.Presentation;
using P14 = DocumentFormat.OpenXml.Office2010.PowerPoint;

namespace PulseOffice.Formats.OpenXml;

public sealed record PptxMediaEdits(TimeSpan TrimStart, TimeSpan TrimEnd, TimeSpan FadeIn, TimeSpan FadeOut)
{
    public static readonly PptxMediaEdits None = new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
}

public sealed class PptxMediaEditing
{
    public void Write(P14.Media media, MediaElement source)
    {
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(source);
        if (source.IsTrimmed)
        {
            var trim = new P14.MediaTrim();
            if (source.TrimStart > TimeSpan.Zero) trim.SetAttribute(new OpenXmlAttribute("st", string.Empty, Milliseconds(source.TrimStart)));
            if (source.TrimEnd > TimeSpan.Zero) trim.SetAttribute(new OpenXmlAttribute("end", string.Empty, Milliseconds(source.TrimEnd)));
            media.Append(trim);
        }
        if (source.IsFaded)
        {
            var fade = new P14.MediaFade();
            if (source.FadeIn > TimeSpan.Zero) fade.SetAttribute(new OpenXmlAttribute("in", string.Empty, Milliseconds(source.FadeIn)));
            if (source.FadeOut > TimeSpan.Zero) fade.SetAttribute(new OpenXmlAttribute("out", string.Empty, Milliseconds(source.FadeOut)));
            media.Append(fade);
        }
    }

    public PptxMediaEdits Read(P.ApplicationNonVisualDrawingProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        var media = properties.Descendants<P14.Media>().FirstOrDefault();
        if (media is null) return PptxMediaEdits.None;
        var trim = media.GetFirstChild<P14.MediaTrim>();
        var fade = media.GetFirstChild<P14.MediaFade>();
        return new PptxMediaEdits(
            Duration(trim, "st"), Duration(trim, "end"), Duration(fade, "in"), Duration(fade, "out"));
    }

    private TimeSpan Duration(OpenXmlElement? element, string name)
    {
        var value = element?.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == name && string.IsNullOrEmpty(attribute.NamespaceUri)).Value;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds)
            && double.IsFinite(milliseconds) && milliseconds > 0
            ? TimeSpan.FromMilliseconds(Math.Min(milliseconds, TimeSpan.FromDays(1).TotalMilliseconds))
            : TimeSpan.Zero;
    }

    private string Milliseconds(TimeSpan value) =>
        Math.Round(value.TotalMilliseconds, 3).ToString("0.###", CultureInfo.InvariantCulture);
}
