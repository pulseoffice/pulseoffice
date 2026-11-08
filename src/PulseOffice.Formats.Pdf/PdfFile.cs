using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace PulseOffice.Formats.Pdf;

public sealed class PdfFile : IPdfObjectSink
{
    private readonly Stream _stream;
    private readonly List<long> _offsets = [];
    private readonly IPdfObjectSealer? _sealer;
    private int? _encryptId;

    public PdfFile(Stream stream, IPdfObjectSealer? sealer = null)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _sealer = sealer;
        Write("%PDF-1.7\n%âãÏÓ\n");
    }

    public bool Encrypted => _sealer is not null;

    public int Reserve()
    {
        _offsets.Add(-1);
        return _offsets.Count;
    }

    public int WriteObject(string body)
    {
        var id = Reserve();
        WriteObject(id, body);
        return id;
    }

    public void WriteObject(int id, string body)
    {
        _offsets[id - 1] = _stream.Position;
        Write($"{id} 0 obj\n{(_sealer is null ? body : _sealer.SealStrings(body))}\nendobj\n");
    }

    public int WriteStream(byte[] payload, bool compress) => WriteStream(payload, compress, null);

    public int WriteStream(byte[] payload, bool compress, string? extraDict)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return WriteEncoded(compress ? Deflate(payload) : payload, compress ? "/FlateDecode" : null, extraDict);
    }

    public int WriteFiltered(byte[] encoded, string filter, string? extraDict)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        ArgumentException.ThrowIfNullOrWhiteSpace(filter);
        return WriteEncoded(encoded, filter, extraDict);
    }

    private int WriteEncoded(byte[] encoded, string? filter, string? extraDict)
    {
        var body = _sealer is null ? encoded : _sealer.Seal(encoded);
        var id = Reserve();

        _offsets[id - 1] = _stream.Position;

        Write($"{id} 0 obj\n<< /Length {body.Length}");
        if (filter is not null) Write(" /Filter " + filter);
        if (!string.IsNullOrEmpty(extraDict)) Write(" " + (_sealer is null ? extraDict : _sealer.SealStrings(extraDict)));
        Write(" >>\nstream\n");

        _stream.Write(body);
        Write("\nendstream\nendobj\n");

        return id;
    }

    public void Finish(int catalogId, int infoId, string? identifier = null)
    {
        if (_sealer is not null)
        {
            _encryptId = Reserve();
            _offsets[_encryptId.Value - 1] = _stream.Position;
            Write($"{_encryptId} 0 obj\n{_sealer.EncryptDictionary}\nendobj\n");
        }

        var startXref = _stream.Position;

        Write($"xref\n0 {_offsets.Count + 1}\n");
        Write("0000000000 65535 f \n");

        foreach (var offset in _offsets)
            Write($"{Math.Max(offset, 0):D10} 00000 n \n");

        var id = string.IsNullOrEmpty(identifier) ? string.Empty : " /ID " + identifier;
        if (_encryptId is { } encrypt) id += $" /Encrypt {encrypt} 0 R";
        Write($"trailer\n<< /Size {_offsets.Count + 1} /Root {catalogId} 0 R /Info {infoId} 0 R{id} >>\n");
        Write($"startxref\n{startXref}\n%%EOF\n");

        _stream.Flush();
    }

    public static string TextString(string? value)
    {
        value ??= string.Empty;
        if (value.All(character => character is >= ' ' and < '\u007F')) return PdfEncoding.Literal(value);

        var hex = new StringBuilder("<FEFF");
        foreach (var unit in value.Normalize(NormalizationForm.FormC))
            hex.Append(((int)unit).ToString("X4", CultureInfo.InvariantCulture));

        return hex.Append('>').ToString();
    }

    public static string DateString(DateTimeOffset moment)
    {
        var offset = moment.Offset;
        var sign = offset.Ticks < 0 ? '-' : '+';

        return string.Create(CultureInfo.InvariantCulture,
            $"(D:{moment:yyyyMMddHHmmss}{sign}{Math.Abs(offset.Hours):D2}'{Math.Abs(offset.Minutes):D2}')");
    }

    private static byte[] Deflate(byte[] payload)
    {
        using var buffer = new MemoryStream();

        using (var compressor = new ZLibStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
            compressor.Write(payload);

        return buffer.ToArray();
    }

    private void Write(string text) => _stream.Write(PdfEncoding.ToBytes(text));
}
