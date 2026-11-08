using System.Buffers.Binary;
using System.Numerics;

namespace PulseOffice.Formats.Packaging.Encryption;

internal sealed class Blake2b
{
    private static readonly ulong[] InitialVector =
    [
        0x6A09E667F3BCC908UL, 0xBB67AE8584CAA73BUL, 0x3C6EF372FE94F82BUL, 0xA54FF53A5F1D36F1UL,
        0x510E527FADE682D1UL, 0x9B05688C2B3E6C1FUL, 0x1F83D9ABFB41BD6BUL, 0x5BE0CD19137E2179UL,
    ];

    private static readonly byte[,] Sigma =
    {
        { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
        { 14, 10, 4, 8, 9, 15, 13, 6, 1, 12, 0, 2, 11, 7, 5, 3 },
        { 11, 8, 12, 0, 5, 2, 15, 13, 10, 14, 3, 6, 7, 1, 9, 4 },
        { 7, 9, 3, 1, 13, 12, 11, 14, 2, 6, 5, 10, 4, 0, 15, 8 },
        { 9, 0, 5, 7, 2, 4, 10, 15, 14, 1, 11, 12, 6, 8, 3, 13 },
        { 2, 12, 6, 10, 0, 11, 8, 3, 4, 13, 7, 5, 15, 14, 1, 9 },
        { 12, 5, 1, 15, 14, 13, 4, 10, 0, 7, 6, 3, 9, 2, 8, 11 },
        { 13, 11, 7, 14, 12, 1, 3, 9, 5, 0, 15, 4, 8, 6, 2, 10 },
        { 6, 15, 14, 9, 11, 3, 0, 8, 12, 2, 13, 7, 1, 4, 10, 5 },
        { 10, 2, 8, 4, 7, 6, 1, 5, 15, 11, 9, 14, 3, 12, 13, 0 },
    };

    public byte[] Hash(ReadOnlySpan<byte> data, int outputLength)
    {
        if (outputLength is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(outputLength));

        var state = (ulong[])InitialVector.Clone();
        state[0] ^= 0x01010000UL ^ (ulong)outputLength;

        var block = new byte[128];
        ulong counter = 0;
        var offset = 0;
        while (data.Length - offset > 128)
        {
            counter += 128;
            Compress(state, data.Slice(offset, 128), counter, last: false);
            offset += 128;
        }

        var remaining = data.Length - offset;
        data[offset..].CopyTo(block);
        counter += (ulong)remaining;
        Compress(state, block, counter, last: true);

        var output = new byte[64];
        for (var index = 0; index < 8; index++)
            BinaryPrimitives.WriteUInt64LittleEndian(output.AsSpan(index * 8), state[index]);
        return output[..outputLength];
    }

    private void Compress(ulong[] state, ReadOnlySpan<byte> block, ulong counter, bool last)
    {
        Span<ulong> message = stackalloc ulong[16];
        for (var index = 0; index < 16; index++)
            message[index] = BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(index * 8, 8));

        Span<ulong> work = stackalloc ulong[16];
        state.CopyTo(work);
        InitialVector.CopyTo(work[8..]);
        work[12] ^= counter;
        if (last) work[14] = ~work[14];

        for (var round = 0; round < 12; round++)
        {
            var row = round % 10;
            Mix(work, 0, 4, 8, 12, message[Sigma[row, 0]], message[Sigma[row, 1]]);
            Mix(work, 1, 5, 9, 13, message[Sigma[row, 2]], message[Sigma[row, 3]]);
            Mix(work, 2, 6, 10, 14, message[Sigma[row, 4]], message[Sigma[row, 5]]);
            Mix(work, 3, 7, 11, 15, message[Sigma[row, 6]], message[Sigma[row, 7]]);
            Mix(work, 0, 5, 10, 15, message[Sigma[row, 8]], message[Sigma[row, 9]]);
            Mix(work, 1, 6, 11, 12, message[Sigma[row, 10]], message[Sigma[row, 11]]);
            Mix(work, 2, 7, 8, 13, message[Sigma[row, 12]], message[Sigma[row, 13]]);
            Mix(work, 3, 4, 9, 14, message[Sigma[row, 14]], message[Sigma[row, 15]]);
        }

        for (var index = 0; index < 8; index++) state[index] ^= work[index] ^ work[index + 8];
    }

    private void Mix(Span<ulong> v, int a, int b, int c, int d, ulong x, ulong y)
    {
        v[a] = v[a] + v[b] + x;
        v[d] = BitOperations.RotateRight(v[d] ^ v[a], 32);
        v[c] = v[c] + v[d];
        v[b] = BitOperations.RotateRight(v[b] ^ v[c], 24);
        v[a] = v[a] + v[b] + y;
        v[d] = BitOperations.RotateRight(v[d] ^ v[a], 16);
        v[c] = v[c] + v[d];
        v[b] = BitOperations.RotateRight(v[b] ^ v[c], 63);
    }
}
