namespace PulseOffice.Drawing.Assets;

public sealed class QrBitBuffer
{
    private readonly List<bool> _bits = [];

    public int Count => _bits.Count;

    public void Append(int value, int count)
    {
        for (var i = count - 1; i >= 0; i--) _bits.Add(((value >> i) & 1) == 1);
    }

    public byte[] ToBytes()
    {
        var result = new byte[(_bits.Count + 7) / 8];
        for (var i = 0; i < _bits.Count; i++)
            if (_bits[i]) result[i / 8] |= (byte)(0x80 >> (i % 8));

        return result;
    }
}
