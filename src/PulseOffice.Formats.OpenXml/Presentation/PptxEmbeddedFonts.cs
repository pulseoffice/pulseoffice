using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Document.Fonts;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Typography.OpenType;
using PulseOffice.Presentations;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class PptxEmbeddedFonts(IOpenTypeReader reader, IEmbeddedOpenTypeCodec codec)
{
    public PptxEmbeddedFonts() : this(new OpenTypeReader(), new EmbeddedOpenTypeCodec())
    {
    }

    public void Write(PresentationPart part, DocumentFonts fonts)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(fonts);
        if (fonts.EmbedSystemFonts || fonts.HasFiles) part.Presentation!.EmbedTrueTypeFonts = true;
        if (!fonts.HasFiles) return;

        var list = new P.EmbeddedFontList();
        foreach (var family in fonts.Families)
        {
            var embedded = new P.EmbeddedFont(new P.Font { Typeface = family });
            var slots = fonts.FilesOf(family).ToList();

            if (Pick(slots, false, false) is { } regular && Store(part, regular) is { } regularId) embedded.Append(new P.RegularFont { Id = regularId });
            if (Pick(slots, true, false) is { } bold && Store(part, bold) is { } boldId) embedded.Append(new P.BoldFont { Id = boldId });
            if (Pick(slots, false, true) is { } italic && Store(part, italic) is { } italicId) embedded.Append(new P.ItalicFont { Id = italicId });
            if (Pick(slots, true, true) is { } both && Store(part, both) is { } bothId) embedded.Append(new P.BoldItalicFont { Id = bothId });

            if (embedded.ChildElements.Count > 1) list.Append(embedded);
        }

        if (list.HasChildren) part.Presentation!.Append(list);
    }

    public Diagnostic UnreadableFont { get; } = Diagnostic.Warning("PPTX220",
        "Embedded fonts whose data cannot be decoded are left out: the text falls back to an installed font.");

    public DocumentFonts Read(PresentationPart part, ICollection<Diagnostic> losses)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(losses);
        var presentation = part.Presentation;
        var files = new List<EmbeddedFontFile>();

        foreach (var embedded in presentation?.EmbeddedFontList?.Elements<P.EmbeddedFont>() ?? [])
        {
            var family = embedded.GetFirstChild<P.Font>()?.Typeface?.Value;
            if (string.IsNullOrWhiteSpace(family)) continue;

            Add(part, family, embedded.GetFirstChild<P.RegularFont>()?.Id?.Value, 400, false, files, losses);
            Add(part, family, embedded.GetFirstChild<P.BoldFont>()?.Id?.Value, 700, false, files, losses);
            Add(part, family, embedded.GetFirstChild<P.ItalicFont>()?.Id?.Value, 400, true, files, losses);
            Add(part, family, embedded.GetFirstChild<P.BoldItalicFont>()?.Id?.Value, 700, true, files, losses);
        }

        return new DocumentFonts(presentation?.EmbedTrueTypeFonts?.Value ?? false, files);
    }

    private EmbeddedFontFile? Pick(List<EmbeddedFontFile> files, bool bold, bool italic) =>
        files.FirstOrDefault(file => file.IsBold == bold && file.Italic == italic);

    private string? Store(PresentationPart part, EmbeddedFontFile file)
    {
        OpenTypeFace face;
        try
        {
            face = reader.Read(file.Data).FirstOrDefault() ?? new OpenTypeFace { Family = file.Family };
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ArgumentException)
        {
            return null;
        }

        var fontPart = part.AddFontPart(FontPartType.FontData);
        using (var stream = new MemoryStream(codec.Encode(file.Data, face with { Family = file.Family })))
            fontPart.FeedData(stream);
        return part.GetIdOfPart(fontPart);
    }

    private void Add(PresentationPart part, string family, string? id, int weight, bool italic, List<EmbeddedFontFile> files, ICollection<Diagnostic> losses)
    {
        if (string.IsNullOrEmpty(id) || !part.TryGetPartById(id, out var found) || found is not FontPart font) return;

        using var stream = font.GetStream();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var data = buffer.ToArray();

        try
        {
            var bare = reader.LooksLikeFont(data) ? data : codec.Decode(data);
            files.Add(new EmbeddedFontFile(family, weight, italic, $"{new string([.. family.Where(char.IsLetterOrDigit)])}-{weight}{(italic ? "i" : string.Empty)}.ttf", bare));
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException or EndOfStreamException or IndexOutOfRangeException or OverflowException)
        {
            if (!losses.Contains(UnreadableFont)) losses.Add(UnreadableFont);
        }
    }
}
