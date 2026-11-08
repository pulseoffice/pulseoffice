using System.Text;

namespace PulseOffice.Typography.OpenType;

public interface IOpenTypeReader
{
    IReadOnlyList<OpenTypeFace> Read(Stream stream);

    IReadOnlyList<OpenTypeFace> Read(byte[] data);

    bool LooksLikeFont(ReadOnlySpan<byte> header);
}

public sealed record OpenTypeTable(string Tag, uint CheckSum, uint Offset, uint Length);

public sealed class OpenTypeReader : IOpenTypeReader
{
    private const uint TrueTypeVersion = 0x00010000;
    private const uint CffVersion = 0x4F54544F;
    private const uint AppleTrueType = 0x74727565;
    private const uint CollectionTag = 0x74746366;
    private const int MaximumTables = 512;
    private const int MaximumFaces = 256;

    private readonly MacRomanText _macRoman = new();

    public bool LooksLikeFont(ReadOnlySpan<byte> header)
    {
        if (header.Length < 4) return false;
        var tag = ((uint)header[0] << 24) | ((uint)header[1] << 16) | ((uint)header[2] << 8) | header[3];
        return tag is TrueTypeVersion or CffVersion or AppleTrueType or CollectionTag;
    }

    public IReadOnlyList<OpenTypeFace> Read(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var stream = new MemoryStream(data, writable: false);
        return Read(stream);
    }

    public IReadOnlyList<OpenTypeFace> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek) throw new ArgumentException("The font stream must be seekable.", nameof(stream));

        var source = new BigEndianSource(stream);
        var faces = new List<OpenTypeFace>();

        foreach (var (offset, index) in FaceOffsets(source))
            faces.Add(ReadFace(source, offset, index));

        return faces;
    }

    public IReadOnlyList<OpenTypeTable> Tables(Stream stream, int collectionIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var source = new BigEndianSource(stream);
        var faces = FaceOffsets(source).ToList();
        if (collectionIndex < 0 || collectionIndex >= faces.Count)
            throw new ArgumentOutOfRangeException(nameof(collectionIndex));
        return TableDirectory(source, faces[collectionIndex].Offset);
    }

    private IEnumerable<(long Offset, int Index)> FaceOffsets(BigEndianSource source)
    {
        var tag = source.UInt32(0);

        if (tag == CollectionTag)
        {
            var count = source.UInt32(8);
            if (count == 0 || count > MaximumFaces) throw new InvalidDataException("Font collection has an invalid face count.");
            for (var index = 0; index < count; index++)
                yield return (source.UInt32(12 + (index * 4)), index);
            yield break;
        }

        if (tag is not (TrueTypeVersion or CffVersion or AppleTrueType))
            throw new InvalidDataException("Not an OpenType or TrueType font.");

        yield return (0, 0);
    }

    private IReadOnlyList<OpenTypeTable> TableDirectory(BigEndianSource source, long offset)
    {
        var count = source.UInt16(offset + 4);
        if (count > MaximumTables) throw new InvalidDataException("Font declares too many tables.");

        var tables = new List<OpenTypeTable>(count);
        for (var index = 0; index < count; index++)
        {
            var record = offset + 12 + (index * 16);
            tables.Add(new OpenTypeTable(source.Tag(record), source.UInt32(record + 4), source.UInt32(record + 8), source.UInt32(record + 12)));
        }

        return tables;
    }

    private OpenTypeFace ReadFace(BigEndianSource source, long offset, int index)
    {
        var tables = TableDirectory(source, offset).DistinctBy(table => table.Tag, StringComparer.Ordinal).ToDictionary(table => table.Tag, StringComparer.Ordinal);
        var names = tables.TryGetValue("name", out var name) ? ReadNames(source, name) : new Dictionary<int, string>();

        var face = new OpenTypeFace
        {
            CollectionIndex = index,
            Family = First(names, 16, 1) ?? "Unknown",
            Subfamily = First(names, 17, 2) ?? "Regular",
            FullName = First(names, 4),
            PostScriptName = First(names, 6),
            Version = First(names, 5),
            Designer = First(names, 9),
            Copyright = First(names, 0),
            LicenseDescription = First(names, 13),
            LicenseUrl = First(names, 14),
        };

        if (tables.TryGetValue("head", out var head) && head.Length >= 46)
        {
            var macStyle = source.UInt16(head.Offset + 44);
            face = face with
            {
                CheckSumAdjustment = source.UInt32(head.Offset + 8),
                Italic = (macStyle & 0x2) != 0,
                Weight = (macStyle & 0x1) != 0 ? 700 : 400,
            };
        }

        if (tables.TryGetValue("post", out var post) && post.Length >= 16)
            face = face with { IsFixedPitch = source.UInt32(post.Offset + 12) != 0 };

        if (tables.TryGetValue("OS/2", out var os2) && os2.Length >= 78)
        {
            var version = source.UInt16(os2.Offset);
            var fsSelection = source.UInt16(os2.Offset + 62);
            var weight = source.UInt16(os2.Offset + 4);
            face = face with
            {
                HasOs2 = true,
                Weight = weight switch { >= 1 and <= 9 => weight * 100, >= 10 and <= 1000 => weight, _ => face.Weight },
                Width = source.UInt16(os2.Offset + 6) is var width and >= 1 and <= 9 ? width : face.Width,
                FsType = source.UInt16(os2.Offset + 8),
                FamilyClass = source.Int16(os2.Offset + 30),
                Panose = source.Bytes(os2.Offset + 32, 10),
                UnicodeRanges =
                [
                    source.UInt32(os2.Offset + 42), source.UInt32(os2.Offset + 46),
                    source.UInt32(os2.Offset + 50), source.UInt32(os2.Offset + 54),
                ],
                CodePageRanges = version >= 1 && os2.Length >= 86
                    ? [source.UInt32(os2.Offset + 78), source.UInt32(os2.Offset + 82)]
                    : new uint[2],
                Italic = face.Italic || (fsSelection & 0x1) != 0,
            };
        }

        return face;
    }

    private string? First(Dictionary<int, string> names, params int[] ids)
    {
        foreach (var id in ids)
            if (names.TryGetValue(id, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        return null;
    }

    private Dictionary<int, string> ReadNames(BigEndianSource source, OpenTypeTable table)
    {
        var count = source.UInt16(table.Offset + 2);
        var storage = table.Offset + source.UInt16(table.Offset + 4);
        var best = new Dictionary<int, (int Rank, string Value)>();

        for (var index = 0; index < count; index++)
        {
            var record = table.Offset + 6 + (index * 12);
            if (record + 12 > table.Offset + table.Length) break;

            var platform = source.UInt16(record);
            var encoding = source.UInt16(record + 2);
            var language = source.UInt16(record + 4);
            var id = source.UInt16(record + 6);
            var length = source.UInt16(record + 8);
            var start = storage + source.UInt16(record + 10);

            var rank = Rank(platform, encoding, language);
            if (rank < 0 || length == 0) continue;
            if (best.TryGetValue(id, out var current) && current.Rank <= rank) continue;
            if (start + length > source.Length) continue;

            var bytes = source.Bytes(start, length);
            var text = platform == 1 ? _macRoman.Decode(bytes) : Encoding.BigEndianUnicode.GetString(bytes);
            best[id] = (rank, text.TrimEnd('\0'));
        }

        return best.ToDictionary(pair => pair.Key, pair => pair.Value.Value);
    }

    private int Rank(ushort platform, ushort encoding, ushort language) => platform switch
    {
        3 when encoding is 1 or 10 && language == 0x0409 => 0,
        3 when encoding is 1 or 10 => 1,
        3 when encoding == 0 && language == 0x0409 => 2,
        3 when encoding == 0 => 3,
        0 => 4,
        1 when encoding == 0 && language == 0 => 5,
        _ => -1,
    };
}
