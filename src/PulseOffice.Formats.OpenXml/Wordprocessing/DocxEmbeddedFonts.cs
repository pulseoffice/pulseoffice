using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Document.Fonts;
using PulseOffice.Typography.OpenType;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxFontObfuscation
{
    private const int ObfuscatedLength = 32;

    public byte[] Apply(byte[] data, Guid key)
    {
        var copy = (byte[])data.Clone();
        var bytes = Key(key);
        for (var index = 0; index < Math.Min(ObfuscatedLength, copy.Length); index++) copy[index] ^= bytes[index % bytes.Length];
        return copy;
    }

    private byte[] Key(Guid key)
    {
        var hex = key.ToString("N");
        var bytes = new byte[16];
        for (var index = 0; index < 16; index++)
            bytes[index] = byte.Parse(hex.AsSpan(30 - 2 * index, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return bytes;
    }
}

public sealed class DocxEmbeddedFonts(IOpenTypeReader reader, IEmbeddedOpenTypeCodec codec, DocxFontObfuscation obfuscation)
{
    public const string UnreadableFont = "DOCX221";

    public void Write(MainDocumentPart main, DocumentFonts fonts)
    {
        if (!fonts.HasFiles && fonts.Traits.Count == 0) return;
        var table = main.AddNewPart<FontTablePart>();
        var root = new W.Fonts();
        foreach (var family in fonts.Families.Concat(fonts.Traits.Select(traits => traits.Family)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var font = new W.Font { Name = family };
            if (fonts.TraitsOf(family) is { } traits) Classify(font, traits);
            var files = fonts.FilesOf(family).ToList();
            Slot(table, font, files.FirstOrDefault(file => !file.IsBold && !file.Italic), (id, key) => new W.EmbedRegularFont { Id = id, FontKey = key });
            Slot(table, font, files.FirstOrDefault(file => file.IsBold && !file.Italic), (id, key) => new W.EmbedBoldFont { Id = id, FontKey = key });
            Slot(table, font, files.FirstOrDefault(file => !file.IsBold && file.Italic), (id, key) => new W.EmbedItalicFont { Id = id, FontKey = key });
            Slot(table, font, files.FirstOrDefault(file => file.IsBold && file.Italic), (id, key) => new W.EmbedBoldItalicFont { Id = id, FontKey = key });
            if (font.HasChildren) root.Append(font);
        }

        table.Fonts = root;
    }

    public DocumentFonts Read(MainDocumentPart main, bool embedSystemFonts, ICollection<string> losses)
    {
        var files = new List<EmbeddedFontFile>();
        var traits = new List<FontFaceTraits>();
        if (main.FontTablePart is { Fonts: { } fonts } table)
        {
            foreach (var font in fonts.Elements<W.Font>())
            {
                if (font.Name?.Value is not { Length: > 0 } family) continue;
                if (Traits(family, font) is { } found && !traits.Any(known => known.Family.Equals(family, StringComparison.OrdinalIgnoreCase))) traits.Add(found);
                Add(table, family, font.GetFirstChild<W.EmbedRegularFont>(), 400, false, files, losses);
                Add(table, family, font.GetFirstChild<W.EmbedBoldFont>(), 700, false, files, losses);
                Add(table, family, font.GetFirstChild<W.EmbedItalicFont>(), 400, true, files, losses);
                Add(table, family, font.GetFirstChild<W.EmbedBoldItalicFont>(), 700, true, files, losses);
            }
        }

        return files.Count == 0 && !embedSystemFonts && traits.Count == 0 ? DocumentFonts.Empty : new DocumentFonts(embedSystemFonts, files) { Traits = traits };
    }

    private FontFaceTraits? Traits(string family, W.Font font)
    {
        var generic = font.FontFamily?.Val?.Value;
        var pitch = font.Pitch?.Val?.Value;
        if (generic is null && pitch is null) return null;
        return new FontFaceTraits(family, Generic(generic), Pitch(pitch));
    }

    private FontGenericFamily Generic(W.FontFamilyValues? value)
    {
        if (value == W.FontFamilyValues.Roman) return FontGenericFamily.Roman;
        if (value == W.FontFamilyValues.Swiss) return FontGenericFamily.Swiss;
        if (value == W.FontFamilyValues.Modern) return FontGenericFamily.Modern;
        if (value == W.FontFamilyValues.Script) return FontGenericFamily.Script;
        if (value == W.FontFamilyValues.Decorative) return FontGenericFamily.Decorative;
        return FontGenericFamily.Unknown;
    }

    private FontPitch Pitch(W.FontPitchValues? value)
    {
        if (value == W.FontPitchValues.Variable) return FontPitch.Variable;
        if (value == W.FontPitchValues.Fixed) return FontPitch.Fixed;
        return FontPitch.Unknown;
    }

    private void Classify(W.Font font, FontFaceTraits traits)
    {
        font.Append(new W.FontFamily
        {
            Val = traits.Generic switch
            {
                FontGenericFamily.Roman => W.FontFamilyValues.Roman,
                FontGenericFamily.Swiss => W.FontFamilyValues.Swiss,
                FontGenericFamily.Modern => W.FontFamilyValues.Modern,
                FontGenericFamily.Script => W.FontFamilyValues.Script,
                FontGenericFamily.Decorative => W.FontFamilyValues.Decorative,
                _ => W.FontFamilyValues.Auto,
            },
        });
        font.Append(new W.Pitch
        {
            Val = traits.Pitch switch
            {
                FontPitch.Variable => W.FontPitchValues.Variable,
                FontPitch.Fixed => W.FontPitchValues.Fixed,
                _ => W.FontPitchValues.Default,
            },
        });
    }

    private void Slot(FontTablePart table, W.Font font, EmbeddedFontFile? file, Func<string, string, OpenXmlElement> element)
    {
        if (file is null) return;
        var key = Guid.NewGuid();
        var part = table.AddFontPart(FontPartType.FontOdttf);
        using (var stream = new MemoryStream(obfuscation.Apply(file.Data, key))) part.FeedData(stream);
        font.Append(element(table.GetIdOfPart(part), key.ToString("B").ToUpperInvariant()));
    }

    private void Add(FontTablePart table, string family, W.FontRelationshipType? embed, int weight, bool italic, List<EmbeddedFontFile> files, ICollection<string> losses)
    {
        if (embed?.Id?.Value is not { } id || !table.TryGetPartById(id, out var found) || found is not FontPart part) return;
        using var stream = part.GetStream();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var data = buffer.ToArray();
        try
        {
            if (Guid.TryParse(embed.FontKey?.Value, out var key)) data = obfuscation.Apply(data, key);
            if (!reader.LooksLikeFont(data)) data = codec.Decode(data);
            files.Add(new EmbeddedFontFile(family, weight, italic,
                $"{new string([.. family.Where(char.IsLetterOrDigit)])}-{weight.ToString(CultureInfo.InvariantCulture)}{(italic ? "i" : string.Empty)}.ttf", data));
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or EndOfStreamException or ArgumentException)
        {
            losses.Add(UnreadableFont);
        }
    }
}
