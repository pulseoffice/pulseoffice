using System.Buffers.Binary;
using System.Text;

namespace PulseOffice.Typography.OpenType.Metrics;

public sealed class FontTableData
{
    private readonly byte[] _data;
    private readonly Dictionary<string, (int Offset, int Length)> _tables = new(StringComparer.Ordinal);

    public FontTableData(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
        if (data.Length < 12) throw new InvalidDataException("Font file too short.");
        var count = U16(4);
        for (var i = 0; i < count; i++)
        {
            var record = 12 + i * 16;
            if (record + 16 > data.Length) throw new InvalidDataException("Font table directory truncated.");
            var tag = Encoding.ASCII.GetString(data, record, 4);
            var offset = (int)U32(record + 8);
            var length = (int)U32(record + 12);
            if (offset < 0 || length < 0 || (long)offset + length > data.Length) continue;
            _tables.TryAdd(tag, (offset, length));
        }
    }

    public bool Has(string tag) => _tables.ContainsKey(tag);

    public int Offset(string tag) =>
        _tables.TryGetValue(tag, out var table) ? table.Offset : throw new InvalidDataException($"Missing font table '{tag}'.");

    public byte[] Bytes(string tag) =>
        _tables.TryGetValue(tag, out var table) ? _data.AsSpan(table.Offset, table.Length).ToArray() : [];

    public byte[] Slice(int offset, int length) =>
        offset >= 0 && length >= 0 && (long)offset + length <= _data.Length ? _data.AsSpan(offset, length).ToArray() : [];

    public IEnumerable<string> Tags => _tables.Keys;

    public int Size => _data.Length;

    public byte[] Whole() => (byte[])_data.Clone();

    public int Length(string tag) => _tables.TryGetValue(tag, out var table) ? table.Length : 0;

    public int U16(int offset) => offset >= 0 && offset + 2 <= _data.Length ? BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(offset)) : 0;

    public int S16(int offset) => offset >= 0 && offset + 2 <= _data.Length ? BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(offset)) : 0;

    public uint U32(int offset) => offset >= 0 && offset + 4 <= _data.Length ? BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(offset)) : 0;

    public string Tag(int offset) => offset >= 0 && offset + 4 <= _data.Length ? Encoding.ASCII.GetString(_data, offset, 4) : string.Empty;
}
