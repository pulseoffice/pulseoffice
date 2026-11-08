using System.Buffers.Binary;
using System.Globalization;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace PulseOffice.Formats.Pdf;

public interface IPdfArchiveParts
{
    int Metadata(PdfFile file, PdfExportOptions options, DateTimeOffset created);

    int OutputIntent(PdfFile file);

    string Identifier(PdfExportOptions options, DateTimeOffset created);
}

public sealed class PdfArchiveParts(IColorProfile profile) : IPdfArchiveParts
{
    public int Metadata(PdfFile file, PdfExportOptions options, DateTimeOffset created)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(options);
        var date = created.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
        var archive = options.Archive
            ? "<rdf:Description rdf:about=\"\" xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\"><pdfaid:part>2</pdfaid:part><pdfaid:conformance>B</pdfaid:conformance></rdf:Description>"
            : string.Empty;
        var language = string.IsNullOrWhiteSpace(options.Language) ? "x-default" : Escape(options.Language);
        var xmp =
            "<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>\n" +
            "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">" +
            "<rdf:Description rdf:about=\"\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\">" +
            "<dc:format>application/pdf</dc:format>" +
            $"<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">{Escape(options.Title)}</rdf:li></rdf:Alt></dc:title>" +
            $"<dc:creator><rdf:Seq><rdf:li>{Escape(options.Author)}</rdf:li></rdf:Seq></dc:creator>" +
            $"<dc:description><rdf:Alt><rdf:li xml:lang=\"x-default\">{Escape(options.Subject)}</rdf:li></rdf:Alt></dc:description>" +
            $"<dc:language><rdf:Bag><rdf:li>{language}</rdf:li></rdf:Bag></dc:language>" +
            "</rdf:Description>" +
            "<rdf:Description rdf:about=\"\" xmlns:xmp=\"http://ns.adobe.com/xap/1.0/\">" +
            $"<xmp:CreateDate>{date}</xmp:CreateDate><xmp:ModifyDate>{date}</xmp:ModifyDate><xmp:MetadataDate>{date}</xmp:MetadataDate>" +
            $"<xmp:CreatorTool>{Escape(options.Creator)}</xmp:CreatorTool></rdf:Description>" +
            "<rdf:Description rdf:about=\"\" xmlns:pdf=\"http://ns.adobe.com/pdf/1.3/\">" +
            $"<pdf:Producer>{Escape(options.Producer)}</pdf:Producer><pdf:Keywords>{Escape(options.Keywords)}</pdf:Keywords></rdf:Description>" +
            archive +
            "</rdf:RDF></x:xmpmeta>\n<?xpacket end=\"w\"?>";
        return file.WriteStream(Encoding.UTF8.GetBytes(xmp), false, "/Type /Metadata /Subtype /XML");
    }

    public int OutputIntent(PdfFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var icc = file.WriteStream(profile.Bytes(), true, "/N 3");
        return file.WriteObject(
            $"<< /Type /OutputIntent /S /GTS_PDFA1 /OutputConditionIdentifier ({profile.Name}) /Info ({profile.Name}) /DestOutputProfile {icc} 0 R >>");
    }

    public string Identifier(PdfExportOptions options, DateTimeOffset created)
    {
        ArgumentNullException.ThrowIfNull(options);
        var unique = options.Created is null ? Guid.NewGuid().ToString() : string.Empty;
        var seed = Encoding.UTF8.GetBytes($"{options.Title}|{options.Author}|{options.Subject}|{created:O}|{unique}");
        var hash = Convert.ToHexString(MD5.HashData(seed));
        return $"[<{hash}> <{hash}>]";
    }

    private string Escape(string? text) => SecurityElement.Escape(XmlSafe(text ?? string.Empty)) ?? string.Empty;

    private string XmlSafe(string text)
    {
        var safe = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                safe.Append(character).Append(text[++index]);
                continue;
            }

            if (char.IsControl(character)) safe.Append(' ');
            else if (System.Xml.XmlConvert.IsXmlChar(character)) safe.Append(character);
        }

        return safe.ToString();
    }
}

public interface IColorProfile
{
    string Name { get; }

    byte[] Bytes();
}

public sealed class SrgbProfile : IColorProfile
{
    private const int HeaderSize = 128;

    public string Name => "sRGB IEC61966-2.1";

    public byte[] Bytes()
    {
        var tags = new List<(string Signature, byte[] Data)>
        {
            ("desc", Description("sRGB IEC61966-2.1 (PulseOffice)")),
            ("cprt", Text("No copyright, use freely")),
            ("wtpt", Xyz(0.9642, 1.0, 0.8249)),
            ("rXYZ", Xyz(0.4361, 0.2225, 0.0139)),
            ("gXYZ", Xyz(0.3851, 0.7169, 0.0971)),
            ("bXYZ", Xyz(0.1431, 0.0606, 0.7141)),
            ("rTRC", Curve()),
            ("gTRC", Curve()),
            ("bTRC", Curve()),
        };
        var table = 4 + tags.Count * 12;
        var cursor = HeaderSize + table;
        var offsets = new List<int>();
        foreach (var (_, data) in tags)
        {
            offsets.Add(cursor);
            cursor += (data.Length + 3) & ~3;
        }
        var profile = new byte[cursor];
        BinaryPrimitives.WriteUInt32BigEndian(profile, (uint)cursor);
        BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(8), 0x02100000);
        Ascii(profile, 12, "mntr");
        Ascii(profile, 16, "RGB ");
        Ascii(profile, 20, "XYZ ");
        BinaryPrimitives.WriteUInt16BigEndian(profile.AsSpan(24), 2000);
        BinaryPrimitives.WriteUInt16BigEndian(profile.AsSpan(26), 1);
        BinaryPrimitives.WriteUInt16BigEndian(profile.AsSpan(28), 1);
        Ascii(profile, 36, "acsp");
        Fixed(profile, 68, 0.9642);
        Fixed(profile, 72, 1.0);
        Fixed(profile, 76, 0.8249);
        BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(HeaderSize), (uint)tags.Count);
        for (var i = 0; i < tags.Count; i++)
        {
            var entry = HeaderSize + 4 + i * 12;
            Ascii(profile, entry, tags[i].Signature);
            BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(entry + 4), (uint)offsets[i]);
            BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(entry + 8), (uint)tags[i].Data.Length);
            tags[i].Data.CopyTo(profile.AsSpan(offsets[i]));
        }
        return profile;
    }

    private byte[] Description(string text)
    {
        var data = new byte[12 + text.Length + 1 + 4 + 4 + 2 + 1 + 67];
        Ascii(data, 0, "desc");
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), (uint)(text.Length + 1));
        Ascii(data, 12, text);
        return data;
    }

    private byte[] Text(string text)
    {
        var data = new byte[8 + text.Length + 1];
        Ascii(data, 0, "text");
        Ascii(data, 8, text);
        return data;
    }

    private byte[] Xyz(double x, double y, double z)
    {
        var data = new byte[20];
        Ascii(data, 0, "XYZ ");
        Fixed(data, 8, x);
        Fixed(data, 12, y);
        Fixed(data, 16, z);
        return data;
    }

    private byte[] Curve()
    {
        var data = new byte[14];
        Ascii(data, 0, "curv");
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(12), (ushort)Math.Round(2.2 * 256));
        return data;
    }

    private void Ascii(byte[] target, int offset, string text) => Encoding.ASCII.GetBytes(text).CopyTo(target, offset);

    private void Fixed(byte[] target, int offset, double value) =>
        BinaryPrimitives.WriteInt32BigEndian(target.AsSpan(offset), (int)Math.Round(value * 65536));
}
