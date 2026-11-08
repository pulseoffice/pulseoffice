using System.Buffers.Binary;
using System.Text;
using PulseOffice.Typography.MicroType;

namespace PulseOffice.Typography.OpenType;

public interface IEmbeddedOpenTypeCodec
{
    byte[] Encode(byte[] font, OpenTypeFace face);

    byte[] Decode(byte[] eot);
}

public sealed class EmbeddedOpenTypeCodec(IMicroTypeExpressDecoder microType) : IEmbeddedOpenTypeCodec
{
    private const uint Version = 0x00020002;
    private const ushort MagicNumber = 0x504C;
    private const uint CompressedFlag = 0x00000004;
    private const uint XorEncryptedFlag = 0x10000000;
    private const byte XorKey = 0x50;
    private const int FixedHeaderLength = 82;

    public EmbeddedOpenTypeCodec() : this(new MicroTypeExpressDecoder())
    {
    }

    public byte[] Encode(byte[] font, OpenTypeFace face)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(face);

        using var output = new MemoryStream();
        var header = new byte[FixedHeaderLength];
        var span = header.AsSpan();

        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], (uint)font.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], Version);
        BinaryPrimitives.WriteUInt32LittleEndian(span[12..], 0);
        face.Panose.AsSpan(0, Math.Min(10, face.Panose.Length)).CopyTo(span[16..]);
        span[26] = 1;
        span[27] = face.Italic ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt32LittleEndian(span[28..], (uint)face.Weight);
        BinaryPrimitives.WriteUInt16LittleEndian(span[32..], face.FsType);
        BinaryPrimitives.WriteUInt16LittleEndian(span[34..], MagicNumber);
        for (var index = 0; index < 4; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(span[(36 + (index * 4))..], face.UnicodeRanges.ElementAtOrDefault(index));
        for (var index = 0; index < 2; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(span[(52 + (index * 4))..], face.CodePageRanges.ElementAtOrDefault(index));
        BinaryPrimitives.WriteUInt32LittleEndian(span[60..], face.CheckSumAdjustment);

        output.Write(header, 0, 80);
        WriteName(output, face.Family);
        WriteName(output, face.Subfamily);
        WriteName(output, face.Version ?? string.Empty);
        WriteName(output, face.FullName ?? face.Family);

        WriteUInt16(output, 0);
        WriteUInt16(output, 0);
        output.Write([0x42, 0x53, 0x47, 0x50]);
        output.Write([0xE4, 0x04, 0x00, 0x00]);
        WriteUInt16(output, 0);
        WriteUInt16(output, 0);
        output.Write(new byte[8]);
        output.Write(font);

        var result = output.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(result, (uint)result.Length);
        return result;
    }

    public byte[] Decode(byte[] eot)
    {
        ArgumentNullException.ThrowIfNull(eot);
        if (eot.Length < 80) throw new InvalidDataException("Embedded OpenType data is truncated.");

        var span = eot.AsSpan();
        var total = BinaryPrimitives.ReadUInt32LittleEndian(span);
        var size = BinaryPrimitives.ReadUInt32LittleEndian(span[4..]);
        var flags = BinaryPrimitives.ReadUInt32LittleEndian(span[12..]);
        var magic = BinaryPrimitives.ReadUInt16LittleEndian(span[34..]);

        if (magic != MagicNumber) throw new InvalidDataException("Not an Embedded OpenType file.");
        if (total > eot.Length || size > total || total - size < FixedHeaderLength) throw new InvalidDataException("Embedded OpenType sizes are inconsistent.");

        var data = span[(int)(total - size)..(int)total].ToArray();
        if ((flags & XorEncryptedFlag) != 0)
            for (var index = 0; index < data.Length; index++) data[index] ^= XorKey;

        return (flags & CompressedFlag) != 0 ? microType.Decompress(data) : data;
    }

    private void WriteName(Stream output, string value)
    {
        WriteUInt16(output, 0);
        var bytes = Encoding.Unicode.GetBytes(value);
        WriteUInt16(output, (ushort)bytes.Length);
        output.Write(bytes);
    }

    private void WriteUInt16(Stream output, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
        output.Write(buffer);
    }
}
