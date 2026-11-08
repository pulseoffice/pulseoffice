using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace PulseOffice.Security;

internal sealed record PdfObject(int Id, string Body);

internal sealed record PdfTrailerInfo(long StartXref, bool CrossReferenceStream, int Size, int? Root, string Carried);

internal sealed partial class PdfTrailer
{
    [GeneratedRegex(@"startxref\s+(\d+)\s*%%EOF\s*$", RegexOptions.RightToLeft)]
    private partial Regex LastStartXref();

    [GeneratedRegex(@"/Size\s+(\d+)")]
    private partial Regex SizeEntry();

    [GeneratedRegex(@"/Root\s+(\d+)\s+\d+\s+R")]
    private partial Regex RootEntry();

    [GeneratedRegex(@"/Info\s+(\d+\s+\d+\s+R)")]
    private partial Regex InfoEntry();

    [GeneratedRegex(@"/ID\s*(\[\s*<[0-9A-Fa-f\s]*>\s*<[0-9A-Fa-f\s]*>\s*\])")]
    private partial Regex IdentifierEntry();

    [GeneratedRegex(@"/Encrypt\b")]
    private partial Regex EncryptEntry();

    [GeneratedRegex(@"/Type\s*/XRef\b")]
    private partial Regex CrossReferenceStreamType();

    public PdfTrailerInfo Read(string text)
    {
        var startXref = LastStartXref().Match(text) is { Success: true } match
            ? long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
            : throw new InvalidOperationException("The file has no cross-reference table.");

        var (section, stream) = Section(text, startXref);
        if (EncryptEntry().IsMatch(section)) throw new InvalidOperationException("The PDF file is encrypted, and signing encrypted PDF files is not supported.");

        var size = SizeEntry().Match(section) is { Success: true } sized
            ? int.Parse(sized.Groups[1].Value, CultureInfo.InvariantCulture)
            : throw new InvalidOperationException("The file has no trailer size.");
        if (size is <= 0 or > 8_388_607) throw new InvalidOperationException("The trailer size is out of range.");

        int? root = RootEntry().Match(section) is { Success: true } rooted ? int.Parse(rooted.Groups[1].Value, CultureInfo.InvariantCulture) : null;

        var carried = new StringBuilder();
        if (InfoEntry().Match(section) is { Success: true } info) carried.Append(" /Info ").Append(info.Groups[1].Value);
        if (IdentifierEntry().Match(section) is { Success: true } identifier) carried.Append(" /ID ").Append(identifier.Groups[1].Value);

        return new PdfTrailerInfo(startXref, stream, size, root, carried.ToString());
    }

    private (string Section, bool Stream) Section(string text, long startXref)
    {
        var start = startXref >= 0 && startXref < text.Length ? (int)startXref : 0;
        while (start < text.Length && char.IsWhiteSpace(text[start])) start++;
        var end = text.IndexOf("startxref", start, StringComparison.Ordinal);
        if (end < 0) end = text.Length;

        if (StartsWithObject(text, start))
        {
            var dictionaryEnd = text.IndexOf("stream", start, end - start, StringComparison.Ordinal);
            var dictionary = text[start..(dictionaryEnd >= 0 ? dictionaryEnd : end)];
            return (dictionary, CrossReferenceStreamType().IsMatch(dictionary));
        }

        var trailer = text.IndexOf("trailer", start, end - start, StringComparison.Ordinal);
        return trailer >= 0 ? (text[trailer..end], false) : (text[start..end], false);
    }

    private bool StartsWithObject(string text, int start)
    {
        var index = start;
        while (index < text.Length && char.IsAsciiDigit(text[index])) index++;
        return index > start;
    }
}

internal sealed partial class PdfObjectIndex(string text)
{
    private const int MaximumObjectStreamBytes = 64 * 1024 * 1024;
    private const int MaximumObjectStreams = 4096;

    private Dictionary<int, string>? _packed;
    private Dictionary<int, int>? _headers;

    [GeneratedRegex(@"(?<![0-9])(\d+)\s+(\d+)\s+obj\b")]
    private partial Regex ObjectHeader();

    [GeneratedRegex(@"/Type\s*/ObjStm\b")]
    private partial Regex ObjectStreamType();

    [GeneratedRegex(@"/N\s+(\d+)")]
    private partial Regex CountEntry();

    [GeneratedRegex(@"/First\s+(\d+)")]
    private partial Regex FirstEntry();

    [GeneratedRegex(@"/Length\s+(\d+)(\s+\d+\s+R)?")]
    private partial Regex LengthEntry();

    [GeneratedRegex(@"/Filter\s*(\[\s*)?/(\w+)")]
    private partial Regex FilterEntry();

    [GeneratedRegex(@"/Type\s*/Catalog\b")]
    private partial Regex CatalogueType();

    [GeneratedRegex(@"/DecodeParms")]
    private partial Regex DecodeParameters();

    public string? Body(int id)
    {
        var direct = Direct(id);
        if (direct is not null) return direct;
        return Packed().GetValueOrDefault(id);
    }

    public PdfObject? Catalogue(int? root)
    {
        if (root is { } id && Body(id) is { } body && body.Contains("/Catalog", StringComparison.Ordinal)) return new PdfObject(id, body);

        foreach (var candidate in Headers().OrderByDescending(pair => pair.Value).Select(pair => pair.Key))
        {
            if (Direct(candidate) is { } direct && CatalogueType().IsMatch(direct)) return new PdfObject(candidate, direct);
        }

        return Packed().OrderByDescending(pair => pair.Key)
            .Where(pair => CatalogueType().IsMatch(pair.Value))
            .Select(pair => new PdfObject(pair.Key, pair.Value))
            .FirstOrDefault();
    }

    private string? Direct(int id)
    {
        if (!Headers().TryGetValue(id, out var start)) return null;
        var end = text.IndexOf("endobj", start, StringComparison.Ordinal);
        if (end < 0) return null;
        var body = text[start..end];
        var stream = body.IndexOf("stream", StringComparison.Ordinal);
        return (stream >= 0 ? body[..stream] : body).Trim();
    }

    private Dictionary<int, int> Headers()
    {
        if (_headers is not null) return _headers;
        _headers = [];
        foreach (Match header in ObjectHeader().Matches(text))
            if (int.TryParse(header.Groups[1].ValueSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)) _headers[id] = header.Index + header.Length;
        return _headers;
    }

    private Dictionary<int, string> Packed()
    {
        if (_packed is not null) return _packed;
        _packed = [];
        var streams = 0;
        foreach (Match header in ObjectHeader().Matches(text))
        {
            var start = header.Index + header.Length;
            var dictionaryEnd = text.IndexOf("stream", start, StringComparison.Ordinal);
            var objectEnd = text.IndexOf("endobj", start, StringComparison.Ordinal);
            if (dictionaryEnd < 0 || (objectEnd >= 0 && objectEnd < dictionaryEnd)) continue;
            var dictionary = text[start..dictionaryEnd];
            if (!ObjectStreamType().IsMatch(dictionary)) continue;
            if (++streams > MaximumObjectStreams) break;
            if (Decoded(dictionary, dictionaryEnd + 6) is not { } decoded) continue;
            Unpack(dictionary, decoded);
        }
        return _packed;
    }

    private string? Decoded(string dictionary, int afterKeyword)
    {
        if (DecodeParameters().IsMatch(dictionary)) return null;
        var data = afterKeyword;
        if (data < text.Length && text[data] == '\r') data++;
        if (data < text.Length && text[data] == '\n') data++;

        var end = text.IndexOf("endstream", data, StringComparison.Ordinal);
        if (end < 0) return null;
        var length = LengthEntry().Match(dictionary) is { Success: true } sized && !sized.Groups[2].Success
            && int.TryParse(sized.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var declared) && data + declared <= end
                ? declared
                : end - data;
        var raw = Encoding.Latin1.GetBytes(text.Substring(data, length));

        var filter = FilterEntry().Match(dictionary);
        if (!filter.Success) return Encoding.Latin1.GetString(raw);
        if (filter.Groups[2].Value != "FlateDecode") return null;

        try
        {
            using var input = new MemoryStream(raw);
            using var inflater = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = inflater.Read(buffer)) > 0)
            {
                if (output.Length + read > MaximumObjectStreamBytes) return null;
                output.Write(buffer, 0, read);
            }
            return Encoding.Latin1.GetString(output.GetBuffer(), 0, (int)output.Length);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private void Unpack(string dictionary, string decoded)
    {
        if (CountEntry().Match(dictionary) is not { Success: true } counted || FirstEntry().Match(dictionary) is not { Success: true } firsted) return;
        var count = int.Parse(counted.Groups[1].Value, CultureInfo.InvariantCulture);
        var first = int.Parse(firsted.Groups[1].Value, CultureInfo.InvariantCulture);
        if (first > decoded.Length || count <= 0) return;

        var numbers = decoded[..first].Split([' ', '\r', '\n', '\t', '\f', '\0'], StringSplitOptions.RemoveEmptyEntries);
        var pairs = Math.Min(count, numbers.Length / 2);
        var entries = new List<(int Id, int Offset)>(pairs);
        for (var i = 0; i < pairs; i++)
        {
            if (!int.TryParse(numbers[2 * i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                || !int.TryParse(numbers[2 * i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset)
                || offset < 0 || first + offset > decoded.Length) return;
            entries.Add((id, first + offset));
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var end = i + 1 < entries.Count && entries[i + 1].Offset >= entries[i].Offset ? entries[i + 1].Offset : decoded.Length;
            _packed![entries[i].Id] = decoded[entries[i].Offset..end].Trim();
        }
    }
}

internal sealed class PdfCatalogue
{
    public string WithForm(string body, int fieldId)
    {
        var form = $"/AcroForm << /Fields [{fieldId} 0 R] /SigFlags 3 >>";
        var existing = body.IndexOf("/AcroForm", StringComparison.Ordinal);

        if (existing < 0)
        {
            var close = body.LastIndexOf(">>", StringComparison.Ordinal);
            return close < 0 ? body : body[..close] + " " + form + " " + body[close..];
        }

        var start = body.IndexOf("<<", existing, StringComparison.Ordinal);
        if (start < 0) return body;

        var end = new PdfDictionaryScanner().End(body, start);
        var fields = body.IndexOf("/Fields", start, StringComparison.Ordinal);
        var open = fields >= 0 && fields < end ? body.IndexOf('[', fields) : -1;
        var bracket = open >= 0 && open < end ? body.IndexOf(']', open) : -1;

        return bracket >= 0 && bracket < end
            ? body[..bracket] + $" {fieldId} 0 R" + body[bracket..]
            : body[..existing] + form + body[end..];
    }
}

internal sealed class PdfDictionaryScanner
{
    public int End(string body, int start)
    {
        var depth = 0;
        var index = start;

        while (index < body.Length - 1)
        {
            if (body[index] == '<' && body[index + 1] == '<') { depth++; index += 2; continue; }
            if (body[index] == '>' && body[index + 1] == '>')
            {
                index += 2;
                if (--depth == 0) break;
                continue;
            }

            index++;
        }

        return index;
    }
}
