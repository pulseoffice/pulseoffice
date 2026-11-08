namespace PulseOffice.Typography.MicroType;

public interface ILzcompDecoder
{
    byte[] Decode(byte[] data, int offset, int length, byte version);
}

public sealed class LzcompDecoder(MtxLimits limits) : ILzcompDecoder
{
    private const int LengthWidth = 3;
    private const int DistanceWidth = 3;
    private const int MinimumLength = 2;
    private const int LongDistance = 512;
    private const int PreloadSize = (2 * 32 * 96) + (4 * 256);

    public LzcompDecoder() : this(MtxLimits.Default)
    {
    }

    public byte[] Decode(byte[] data, int offset, int length, byte version)
    {
        ArgumentNullException.ThrowIfNull(data);
        var bits = new MostSignificantFirstBitReader(data, offset, length);
        var runLength = version != 1 && bits.ReadBit();
        var distances = new AdaptiveHuffmanTree(bits, 1 << DistanceWidth);
        var lengths = new AdaptiveHuffmanTree(bits, 1 << LengthWidth);
        var outputLength = (int)bits.ReadBits(24);
        if (outputLength > limits.MaxBlockBytes) throw new InvalidDataException("Compressed font block declares too large an output.");

        var ranges = DistanceRanges(outputLength);
        var dup2 = 256 + ((1 << LengthWidth) * ranges);
        var symbols = new AdaptiveHuffmanTree(bits, dup2 + 3);
        ByteSink sink = runLength ? new RunLengthByteSink(limits.MaxBlockBytes) : new PlainByteSink(limits.MaxBlockBytes);

        var window = new byte[PreloadSize + outputLength];
        FillPreload(window);
        var position = PreloadSize;
        var end = PreloadSize + outputLength;

        while (position < end)
        {
            var symbol = symbols.ReadSymbol();
            if (symbol < 256) Put(window, ref position, (byte)symbol, sink);
            else if (symbol == dup2) Put(window, ref position, window[position - 2], sink);
            else if (symbol == dup2 + 1) Put(window, ref position, window[position - 4], sink);
            else if (symbol == dup2 + 2) Put(window, ref position, window[position - 6], sink);
            else Copy(window, ref position, end, symbol, ranges, lengths, distances, sink);
        }

        return sink.ToArray();
    }

    private void Copy(byte[] window, ref int position, int end, int symbol, int ranges, AdaptiveHuffmanTree lengths, AdaptiveHuffmanTree distances, ByteSink sink)
    {
        var code = symbol - 256;
        var distanceSymbols = (code / (1 << LengthWidth)) + 1;
        if (distanceSymbols > ranges) throw new InvalidDataException("Copy command uses too many distance symbols.");

        var length = ReadLength(code % (1 << LengthWidth), lengths, end);
        var distance = 0L;
        for (var index = 0; index < distanceSymbols; index++)
            distance = (distance << DistanceWidth) | (uint)distances.ReadSymbol();
        distance++;
        if (distance >= LongDistance) length++;

        var start = position - distance - length + 1;
        if (start < 0 || length > end - position) throw new InvalidDataException("Copy command points outside the decompressed data.");

        var source = (int)start;
        for (var index = 0; index < length; index++)
            Put(window, ref position, window[source + index], sink);
    }

    private long ReadLength(int first, AdaptiveHuffmanTree lengths, int end)
    {
        const int stopBit = 1 << (LengthWidth - 1);
        var bits = first;
        var value = 0L;
        while (true)
        {
            value = (value << (LengthWidth - 1)) | (uint)(bits & (stopBit - 1));
            if (value > end) throw new InvalidDataException("Copy command length is out of range.");
            if ((bits & stopBit) == 0) return value + MinimumLength;
            bits = lengths.ReadSymbol();
        }
    }

    private void Put(byte[] window, ref int position, byte value, ByteSink sink)
    {
        window[position++] = value;
        sink.Accept(value);
    }

    private int DistanceRanges(int length)
    {
        var ranges = 1;
        while ((1L << (DistanceWidth * ranges)) < length) ranges++;
        return ranges;
    }

    private void FillPreload(byte[] preload)
    {
        var index = 0;
        for (var high = 0; high < 32; high++)
            for (var low = 0; low < 96; low++)
            {
                preload[index++] = (byte)high;
                preload[index++] = (byte)low;
            }

        for (var value = 0; value < 256; value++)
            for (var repeat = 0; repeat < 4; repeat++)
                preload[index++] = (byte)value;
    }
}
