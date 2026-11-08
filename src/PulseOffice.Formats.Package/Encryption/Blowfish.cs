using System.Numerics;

namespace PulseOffice.Formats.Packaging.Encryption;

internal sealed class BlowfishTables
{
    private const int Words = 18 + 4 * 256;

    private readonly Lazy<uint[]> _digits = new(ComputeDigits);

    public uint[] InitialWords => _digits.Value;

    private static uint[] ComputeDigits()
    {
        var bits = Words * 32;
        var guard = 64;
        var one = BigInteger.One << (bits + guard);
        var pi = 16 * ArcTangent(5, one) - 4 * ArcTangent(239, one);
        var fraction = (pi - 3 * one) >> guard;

        var words = new uint[Words];
        for (var index = 0; index < Words; index++)
            words[index] = (uint)((fraction >> (bits - 32 * (index + 1))) & uint.MaxValue);
        return words;
    }

    private static BigInteger ArcTangent(int inverse, BigInteger one)
    {
        var square = (BigInteger)inverse * inverse;
        var power = one / inverse;
        var sum = power;
        for (var term = 1; !power.IsZero; term++)
        {
            power /= square;
            var value = power / (2 * term + 1);
            sum = term % 2 == 1 ? sum - value : sum + value;
        }

        return sum;
    }
}

internal sealed class Blowfish
{
    private readonly uint[] _p = new uint[18];
    private readonly uint[,] _s = new uint[4, 256];

    public Blowfish(BlowfishTables tables, ReadOnlySpan<byte> key)
    {
        if (key.Length is < 1 or > 56) throw new InvalidDataException("Invalid Blowfish key length.");

        var initial = tables.InitialWords;
        Array.Copy(initial, _p, 18);
        for (var box = 0; box < 4; box++)
            for (var entry = 0; entry < 256; entry++)
                _s[box, entry] = initial[18 + box * 256 + entry];

        var position = 0;
        for (var index = 0; index < 18; index++)
        {
            uint data = 0;
            for (var k = 0; k < 4; k++)
            {
                data = (data << 8) | key[position];
                position = (position + 1) % key.Length;
            }

            _p[index] ^= data;
        }

        uint left = 0, right = 0;
        for (var index = 0; index < 18; index += 2)
        {
            Encrypt(ref left, ref right);
            _p[index] = left;
            _p[index + 1] = right;
        }

        for (var box = 0; box < 4; box++)
            for (var entry = 0; entry < 256; entry += 2)
            {
                Encrypt(ref left, ref right);
                _s[box, entry] = left;
                _s[box, entry + 1] = right;
            }
    }

    public void EncryptBlock(Span<byte> block)
    {
        var left = (uint)(block[0] << 24 | block[1] << 16 | block[2] << 8 | block[3]);
        var right = (uint)(block[4] << 24 | block[5] << 16 | block[6] << 8 | block[7]);
        Encrypt(ref left, ref right);
        for (var k = 0; k < 4; k++)
        {
            block[k] = (byte)(left >> (24 - 8 * k));
            block[4 + k] = (byte)(right >> (24 - 8 * k));
        }
    }

    private void Encrypt(ref uint left, ref uint right)
    {
        for (var round = 0; round < 16; round++)
        {
            left ^= _p[round];
            right ^= F(left);
            (left, right) = (right, left);
        }

        (left, right) = (right, left);
        right ^= _p[16];
        left ^= _p[17];
    }

    private uint F(uint x) =>
        ((_s[0, x >> 24] + _s[1, (x >> 16) & 0xFF]) ^ _s[2, (x >> 8) & 0xFF]) + _s[3, x & 0xFF];
}
