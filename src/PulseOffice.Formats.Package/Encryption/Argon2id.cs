using System.Buffers.Binary;
using System.Numerics;

namespace PulseOffice.Formats.Packaging.Encryption;

internal sealed class Argon2id
{
    private const int BlockWords = 128;
    private const int SyncPoints = 4;
    private const uint Version = 0x13;
    private const int MaxMemoryKiB = 1024 * 1024;

    private readonly Blake2b _blake = new();

    public byte[] Derive(byte[] password, byte[] salt, int iterations, int memoryKiB, int lanes, int length)
    {
        if (iterations is < 1 or > 64 || lanes is < 1 or > 64 || length is < 4 or > 1024
            || memoryKiB < 8 * lanes || memoryKiB > MaxMemoryKiB)
            throw new InvalidDataException("Unsupported Argon2 parameters.");

        var segment = memoryKiB / (SyncPoints * lanes);
        var laneLength = segment * SyncPoints;
        var memory = new ulong[(long)lanes * laneLength * BlockWords];
        var scratch = new Scratch();

        var initial = InitialHash(password, salt, iterations, memoryKiB, lanes, length);
        for (var lane = 0; lane < lanes; lane++)
            for (uint first = 0; first < 2; first++)
            {
                var seed = new byte[72];
                initial.CopyTo(seed, 0);
                BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(64), first);
                BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(68), (uint)lane);
                ToWords(VariableHash(seed, 1024), Block(memory, lane * laneLength + (int)first));
            }

        for (var pass = 0; pass < iterations; pass++)
            for (var slice = 0; slice < SyncPoints; slice++)
                for (var lane = 0; lane < lanes; lane++)
                    FillSegment(memory, scratch, pass, lane, slice, lanes, laneLength, segment, iterations);

        var final = Block(memory, laneLength - 1).ToArray();
        for (var lane = 1; lane < lanes; lane++)
        {
            var last = Block(memory, lane * laneLength + laneLength - 1);
            for (var word = 0; word < BlockWords; word++) final[word] ^= last[word];
        }

        return VariableHash(ToBytes(final), length);
    }

    private sealed class Scratch
    {
        public ulong[] R { get; } = new ulong[BlockWords];
        public ulong[] Q { get; } = new ulong[BlockWords];
        public ulong[] Zero { get; } = new ulong[BlockWords];
        public ulong[] Input { get; } = new ulong[BlockWords];
        public ulong[] Addresses { get; } = new ulong[BlockWords];
        public ulong[] Temporary { get; } = new ulong[BlockWords];
    }

    private static Span<ulong> Block(ulong[] memory, int index) => memory.AsSpan(index * BlockWords, BlockWords);

    private byte[] InitialHash(byte[] password, byte[] salt, int iterations, int memoryKiB, int lanes, int length)
    {
        using var buffer = new MemoryStream();
        void Word(uint value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            buffer.Write(bytes);
        }

        Word((uint)lanes);
        Word((uint)length);
        Word((uint)memoryKiB);
        Word((uint)iterations);
        Word(Version);
        Word(2);
        Word((uint)password.Length);
        buffer.Write(password);
        Word((uint)salt.Length);
        buffer.Write(salt);
        Word(0);
        Word(0);
        return _blake.Hash(buffer.ToArray(), 64);
    }

    private byte[] VariableHash(byte[] input, int length)
    {
        var prefixed = new byte[input.Length + 4];
        BinaryPrimitives.WriteUInt32LittleEndian(prefixed, (uint)length);
        input.CopyTo(prefixed, 4);
        if (length <= 64) return _blake.Hash(prefixed, length);

        var output = new byte[length];
        var current = _blake.Hash(prefixed, 64);
        Array.Copy(current, output, 32);
        var position = 32;
        while (length - position > 64)
        {
            current = _blake.Hash(current, 64);
            Array.Copy(current, 0, output, position, 32);
            position += 32;
        }

        current = _blake.Hash(current, length - position);
        Array.Copy(current, 0, output, position, current.Length);
        return output;
    }

    private void FillSegment(ulong[] memory, Scratch scratch, int pass, int lane, int slice, int lanes, int laneLength, int segment, int iterations)
    {
        var independent = pass == 0 && slice < SyncPoints / 2;
        if (independent)
        {
            Array.Clear(scratch.Input);
            scratch.Input[0] = (ulong)pass;
            scratch.Input[1] = (ulong)lane;
            scratch.Input[2] = (ulong)slice;
            scratch.Input[3] = (ulong)(lanes * laneLength);
            scratch.Input[4] = (ulong)iterations;
            scratch.Input[5] = 2;
        }

        var start = pass == 0 && slice == 0 ? 2 : 0;
        if (independent && start == 2) NextAddresses(scratch);

        for (var index = start; index < segment; index++)
        {
            var current = lane * laneLength + slice * segment + index;
            var previous = current % laneLength == 0 ? current + laneLength - 1 : current - 1;

            ulong pseudo;
            if (independent)
            {
                if (index % BlockWords == 0) NextAddresses(scratch);
                pseudo = scratch.Addresses[index % BlockWords];
            }
            else
            {
                pseudo = memory[previous * BlockWords];
            }

            var referenceLane = pass == 0 && slice == 0 ? lane : (int)((pseudo >> 32) % (ulong)lanes);
            var reference = ReferenceIndex(pass, slice, index, segment, laneLength, referenceLane == lane, (uint)pseudo);
            Compress(Block(memory, previous), Block(memory, referenceLane * laneLength + reference), Block(memory, current), pass > 0, scratch);
        }
    }

    private void NextAddresses(Scratch scratch)
    {
        scratch.Input[6]++;
        Compress(scratch.Zero, scratch.Input, scratch.Temporary, false, scratch);
        Compress(scratch.Zero, scratch.Temporary, scratch.Addresses, false, scratch);
    }

    private int ReferenceIndex(int pass, int slice, int index, int segment, int laneLength, bool sameLane, uint pseudo)
    {
        int area;
        if (pass == 0)
            area = slice == 0 ? index - 1 : sameLane ? slice * segment + index - 1 : slice * segment + (index == 0 ? -1 : 0);
        else
            area = sameLane ? laneLength - segment + index - 1 : laneLength - segment + (index == 0 ? -1 : 0);

        ulong relative = pseudo;
        relative = (relative * relative) >> 32;
        relative = (ulong)area - 1 - (((ulong)area * relative) >> 32);
        var startPosition = pass != 0 && slice != SyncPoints - 1 ? (slice + 1) * segment : 0;
        return (int)(((ulong)startPosition + relative) % (ulong)laneLength);
    }

    private void Compress(ReadOnlySpan<ulong> x, ReadOnlySpan<ulong> y, Span<ulong> target, bool accumulate, Scratch scratch)
    {
        var r = scratch.R;
        var q = scratch.Q;
        for (var word = 0; word < BlockWords; word++) r[word] = x[word] ^ y[word];
        r.CopyTo(q, 0);

        for (var row = 0; row < 8; row++)
            Permute(q, row * 16, 1);
        for (var column = 0; column < 8; column++)
            Permute(q, column * 2, 16);

        if (accumulate)
            for (var word = 0; word < BlockWords; word++) target[word] ^= q[word] ^ r[word];
        else
            for (var word = 0; word < BlockWords; word++) target[word] = q[word] ^ r[word];
    }

    private void Permute(ulong[] v, int start, int stride)
    {
        Span<int> index = stackalloc int[16];
        if (stride == 1)
            for (var k = 0; k < 16; k++) index[k] = start + k;
        else
            for (var k = 0; k < 8; k++)
            {
                index[k * 2] = start + k * 16;
                index[k * 2 + 1] = start + k * 16 + 1;
            }

        Mix(v, index[0], index[4], index[8], index[12]);
        Mix(v, index[1], index[5], index[9], index[13]);
        Mix(v, index[2], index[6], index[10], index[14]);
        Mix(v, index[3], index[7], index[11], index[15]);
        Mix(v, index[0], index[5], index[10], index[15]);
        Mix(v, index[1], index[6], index[11], index[12]);
        Mix(v, index[2], index[7], index[8], index[13]);
        Mix(v, index[3], index[4], index[9], index[14]);
    }

    private void Mix(ulong[] v, int a, int b, int c, int d)
    {
        v[a] = Mul(v[a], v[b]);
        v[d] = BitOperations.RotateRight(v[d] ^ v[a], 32);
        v[c] = Mul(v[c], v[d]);
        v[b] = BitOperations.RotateRight(v[b] ^ v[c], 24);
        v[a] = Mul(v[a], v[b]);
        v[d] = BitOperations.RotateRight(v[d] ^ v[a], 16);
        v[c] = Mul(v[c], v[d]);
        v[b] = BitOperations.RotateRight(v[b] ^ v[c], 63);
    }

    private ulong Mul(ulong x, ulong y) => x + y + 2 * (x & 0xFFFFFFFFUL) * (y & 0xFFFFFFFFUL);

    private void ToWords(byte[] bytes, Span<ulong> words)
    {
        for (var index = 0; index < BlockWords; index++)
            words[index] = BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(index * 8));
    }

    private byte[] ToBytes(ulong[] words)
    {
        var bytes = new byte[words.Length * 8];
        for (var index = 0; index < words.Length; index++)
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(index * 8), words[index]);
        return bytes;
    }
}
