namespace PulseOffice.Typography.MicroType;

internal readonly record struct TripletEncoding(int ByteCount, int XBits, int YBits, int DeltaX, int DeltaY, int XSign, int YSign)
{
    public (int X, int Y) Read(CtfCursor glyph)
    {
        var bytes = glyph.ReadBytes(ByteCount - 1).ToArray();
        var bits = new MostSignificantFirstBitReader(bytes, 0, bytes.Length);
        var x = (int)bits.ReadBits(XBits);
        var y = (int)bits.ReadBits(YBits);
        return (XSign * (x + DeltaX), YSign * (y + DeltaY));
    }
}

internal sealed class TripletTable
{
    private readonly TripletEncoding[] _encodings = new TripletEncoding[128];

    public TripletTable()
    {
        for (var index = 0; index < 10; index++)
            _encodings[index] = new TripletEncoding(2, 0, 8, 0, (index / 2) * 256, 0, Sign(index));

        for (var index = 0; index < 10; index++)
            _encodings[10 + index] = new TripletEncoding(2, 8, 0, (index / 2) * 256, 0, Sign(index), 0);

        for (var index = 0; index < 64; index++)
            _encodings[20 + index] = new TripletEncoding(2, 4, 4, 1 + (16 * (index / 16)), 1 + (16 * ((index / 4) % 4)), Sign(index), Sign(index / 2));

        for (var index = 0; index < 36; index++)
            _encodings[84 + index] = new TripletEncoding(3, 8, 8, 1 + (256 * (index / 12)), 1 + (256 * ((index / 4) % 3)), Sign(index), Sign(index / 2));

        for (var index = 0; index < 4; index++)
        {
            _encodings[120 + index] = new TripletEncoding(4, 12, 12, 0, 0, Sign(index), Sign(index / 2));
            _encodings[124 + index] = new TripletEncoding(5, 16, 16, 0, 0, Sign(index), Sign(index / 2));
        }
    }

    public TripletEncoding this[byte flag] => _encodings[flag & 0x7F];

    private int Sign(int index) => index % 2 == 0 ? -1 : 1;
}
