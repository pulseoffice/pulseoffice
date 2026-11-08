using System.Buffers.Binary;
using System.Text;

namespace PulseOffice.Typography.Tests;

public sealed class SyntheticFont
{
    public string Family { get; init; } = "Prova Sans";

    public string Subfamily { get; init; } = "Regular";

    public string? LicenseDescription { get; init; } = "This Font Software is licensed under the SIL Open Font License, Version 1.1.";

    public string? LicenseUrl { get; init; } = "https://openfontlicense.org";

    public ushort Weight { get; init; } = 400;

    public ushort WidthClass { get; init; } = 5;

    public bool Italic { get; init; }

    public ushort FsType { get; init; }

    public byte[] Panose { get; init; } = [2, 11, 5, 3, 0, 0, 0, 0, 0, 0];

    public uint UnicodeRange1 { get; init; } = 0x00000287;

    public bool FixedPitch { get; init; }

    public (ushort Platform, ushort Encoding, ushort Language) NameRecord { get; init; } = (3, 1, 0x0409);

    public IReadOnlyList<(string Tag, byte[] Data)> ExtraTables { get; init; } = [];

    public byte[] Build() => Sfnt(Tables());

    public byte[] BuildCollection(SyntheticFont second)
    {
        var first = Tables();
        var other = second.Tables();
        var headerLength = 12 + 8;
        var firstDirectory = 12 + (first.Count * 16);
        var secondDirectory = 12 + (other.Count * 16);
        var dataStart = headerLength + firstDirectory + secondDirectory;

        using var data = new MemoryStream();
        var firstOffsets = Place(first, dataStart, data);
        var secondOffsets = Place(other, dataStart, data);

        var output = new byte[dataStart + data.Length];
        BinaryPrimitives.WriteUInt32BigEndian(output, 0x74746366);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(4), 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(8), 2);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(12), (uint)headerLength);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(16), (uint)(headerLength + firstDirectory));
        Directory(output.AsSpan(headerLength), first, firstOffsets);
        Directory(output.AsSpan(headerLength + firstDirectory), other, secondOffsets);
        data.ToArray().CopyTo(output, dataStart);
        return output;
    }

    private List<(string Tag, byte[] Data)> Tables() =>
    [
        ("OS/2", Os2()),
        ("head", Head()),
        ("name", Name()),
        ("post", Post()),
        .. ExtraTables,
    ];

    private byte[] Sfnt(List<(string Tag, byte[] Data)> tables)
    {
        var start = 12 + (tables.Count * 16);
        using var data = new MemoryStream();
        var offsets = Place(tables, start, data);
        var output = new byte[start + data.Length];
        Directory(output, tables, offsets);
        data.ToArray().CopyTo(output, start);
        return output;
    }

    private List<int> Place(List<(string Tag, byte[] Data)> tables, int start, MemoryStream data)
    {
        var offsets = new List<int>();
        foreach (var (_, bytes) in tables)
        {
            offsets.Add(start + (int)data.Length);
            data.Write(bytes);
            while (data.Length % 4 != 0) data.WriteByte(0);
        }
        return offsets;
    }

    private void Directory(Span<byte> output, List<(string Tag, byte[] Data)> tables, List<int> offsets)
    {
        BinaryPrimitives.WriteUInt32BigEndian(output, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(output[4..], (ushort)tables.Count);
        for (var index = 0; index < tables.Count; index++)
        {
            var record = output[(12 + (index * 16))..];
            Encoding.ASCII.GetBytes(tables[index].Tag).CopyTo(record);
            BinaryPrimitives.WriteUInt32BigEndian(record[8..], (uint)offsets[index]);
            BinaryPrimitives.WriteUInt32BigEndian(record[12..], (uint)tables[index].Data.Length);
        }
    }

    private byte[] Head()
    {
        var head = new byte[54];
        BinaryPrimitives.WriteUInt32BigEndian(head, 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(8), 0x12345678);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(12), 0x5F0F3CF5);
        BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(44), (ushort)((Weight >= 700 ? 1 : 0) | (Italic ? 2 : 0)));
        return head;
    }

    private byte[] Os2()
    {
        var os2 = new byte[96];
        BinaryPrimitives.WriteUInt16BigEndian(os2, 4);
        BinaryPrimitives.WriteUInt16BigEndian(os2.AsSpan(4), Weight);
        BinaryPrimitives.WriteUInt16BigEndian(os2.AsSpan(6), WidthClass);
        BinaryPrimitives.WriteUInt16BigEndian(os2.AsSpan(8), FsType);
        BinaryPrimitives.WriteInt16BigEndian(os2.AsSpan(30), 0x0800);
        Panose.CopyTo(os2.AsSpan(32));
        BinaryPrimitives.WriteUInt32BigEndian(os2.AsSpan(42), UnicodeRange1);
        BinaryPrimitives.WriteUInt16BigEndian(os2.AsSpan(62), (ushort)(Italic ? 1 : 64));
        BinaryPrimitives.WriteUInt32BigEndian(os2.AsSpan(78), 1);
        return os2;
    }

    private byte[] Post()
    {
        var post = new byte[32];
        BinaryPrimitives.WriteUInt32BigEndian(post, 0x00030000);
        BinaryPrimitives.WriteUInt32BigEndian(post.AsSpan(12), FixedPitch ? 1u : 0u);
        return post;
    }

    private byte[] Name()
    {
        var entries = new List<(ushort Id, string Text)> { (1, Family), (2, Subfamily), (4, $"{Family} {Subfamily}"), (16, Family) };
        if (LicenseDescription is not null) entries.Add((13, LicenseDescription));
        if (LicenseUrl is not null) entries.Add((14, LicenseUrl));

        var storage = new MemoryStream();
        var records = new List<byte[]>();
        foreach (var (id, text) in entries.OrderBy(entry => entry.Id))
        {
            var bytes = NameRecord.Platform == 1 ? Encoding.Latin1.GetBytes(text.Replace('é', '\u008E')) : Encoding.BigEndianUnicode.GetBytes(text);
            var record = new byte[12];
            BinaryPrimitives.WriteUInt16BigEndian(record, NameRecord.Platform);
            BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(2), NameRecord.Encoding);
            BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(4), NameRecord.Language);
            BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(6), id);
            BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(8), (ushort)bytes.Length);
            BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(10), (ushort)storage.Length);
            storage.Write(bytes);
            records.Add(record);
        }

        var header = 6 + (records.Count * 12);
        var output = new byte[header + storage.Length];
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(2), (ushort)records.Count);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(4), (ushort)header);
        for (var index = 0; index < records.Count; index++) records[index].CopyTo(output, 6 + (index * 12));
        storage.ToArray().CopyTo(output, header);
        return output;
    }
}
