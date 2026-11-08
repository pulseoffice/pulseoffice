namespace PulseOffice.Drawing.Assets;

public interface IQrReedSolomon
{
    byte[] Remainder(ReadOnlySpan<byte> data, int degree);
}

public sealed class QrReedSolomon : IQrReedSolomon
{
    public byte[] Remainder(ReadOnlySpan<byte> data, int degree)
    {
        var divisor = Divisor(degree);
        var remainder = new byte[degree];

        foreach (var value in data)
        {
            var factor = (byte)(value ^ remainder[0]);

            Array.Copy(remainder, 1, remainder, 0, degree - 1);
            remainder[degree - 1] = 0;

            for (var i = 0; i < degree; i++)
                remainder[i] ^= Multiply(divisor[i], factor);
        }

        return remainder;
    }

    private byte[] Divisor(int degree)
    {
        var result = new byte[degree];
        result[degree - 1] = 1;

        byte root = 1;

        for (var i = 0; i < degree; i++)
        {
            for (var j = 0; j < degree; j++)
            {
                result[j] = Multiply(result[j], root);
                if (j + 1 < degree) result[j] ^= result[j + 1];
            }

            root = Multiply(root, 2);
        }

        return result;
    }

    private byte Multiply(byte a, byte b)
    {
        var result = 0;

        for (var i = 7; i >= 0; i--)
        {
            result = (result << 1) ^ ((result >> 7) * 0x11D);
            result ^= ((b >> i) & 1) * a;
        }

        return (byte)result;
    }
}
