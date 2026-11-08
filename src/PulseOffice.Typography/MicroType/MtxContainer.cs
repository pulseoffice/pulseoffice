namespace PulseOffice.Typography.MicroType;

internal sealed record MtxBlocks(byte[] Tables, byte[] PushData, byte[] Instructions);

internal sealed class MtxContainerReader(ILzcompDecoder lzcomp, MtxLimits limits)
{
    private const int HeaderLength = 10;

    public MtxBlocks Read(byte[] mtx)
    {
        ArgumentNullException.ThrowIfNull(mtx);
        if (mtx.Length > limits.MaxInputBytes) throw new InvalidDataException("MicroType Express font is too large.");
        if (mtx.Length < HeaderLength) throw new InvalidDataException("MicroType Express header is truncated.");

        var version = mtx[0];
        var second = UInt24(mtx, 4);
        var third = UInt24(mtx, 7);
        if (second < HeaderLength || third < second || third > mtx.Length)
            throw new InvalidDataException("MicroType Express block offsets are inconsistent.");

        return new MtxBlocks(
            lzcomp.Decode(mtx, HeaderLength, second - HeaderLength, version),
            lzcomp.Decode(mtx, second, third - second, version),
            lzcomp.Decode(mtx, third, mtx.Length - third, version));
    }

    private int UInt24(byte[] data, int offset) => (data[offset] << 16) | (data[offset + 1] << 8) | data[offset + 2];
}
