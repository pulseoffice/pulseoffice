using PulseOffice.Drawing.Assets.Filters;

namespace PulseOffice.Drawing.Assets.Gif;

public sealed record GifFrame(BgraImage Image, int DelayMilliseconds);

public sealed record GifAnimation(int Width, int Height, IReadOnlyList<GifFrame> Frames, int Loops)
{
    public int Duration => Frames.Sum(frame => Math.Max(20, frame.DelayMilliseconds));

    public GifFrame At(long milliseconds)
    {
        if (Frames.Count == 1 || Duration <= 0) return Frames[0];
        var time = milliseconds % Duration;
        foreach (var frame in Frames)
        {
            time -= Math.Max(20, frame.DelayMilliseconds);
            if (time < 0) return frame;
        }
        return Frames[^1];
    }
}

public interface IGifCodec
{
    byte[] Encode(IReadOnlyList<GifFrame> frames, int loops);

    GifAnimation? Decode(ReadOnlySpan<byte> data);
}

public sealed class GifCodec : IGifCodec
{
    private const int Transparent = 252;
    private const int MaxCodeSize = 12;

    public byte[] Encode(IReadOnlyList<GifFrame> frames, int loops)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count == 0) throw new ArgumentException("At least one frame is required.", nameof(frames));

        var width = frames.Max(frame => frame.Image.Width);
        var height = frames.Max(frame => frame.Image.Height);
        using var output = new MemoryStream();

        output.Write("GIF89a"u8);
        Short(output, width);
        Short(output, height);
        output.WriteByte(0xF7);
        output.WriteByte(Transparent);
        output.WriteByte(0);
        WritePalette(output);

        output.Write([0x21, 0xFF, 0x0B]);
        output.Write("NETSCAPE2.0"u8);
        output.Write([0x03, 0x01]);
        Short(output, Math.Clamp(loops, 0, ushort.MaxValue));
        output.WriteByte(0);

        foreach (var frame in frames)
        {
            output.Write([0x21, 0xF9, 0x04, 0x09]);
            Short(output, Math.Clamp(frame.DelayMilliseconds / 10, 2, ushort.MaxValue));
            output.WriteByte(Transparent);
            output.WriteByte(0);

            output.WriteByte(0x2C);
            Short(output, 0);
            Short(output, 0);
            Short(output, width);
            Short(output, height);
            output.WriteByte(0);

            output.WriteByte(8);
            WriteBlocks(output, Compress(Indices(frame.Image, width, height)));
        }

        output.WriteByte(0x3B);
        return output.ToArray();
    }

    private const long MaxPixels = 16_000_000;
    private const long MaxFrameBytes = 512L * 1024 * 1024;

    public GifAnimation? Decode(ReadOnlySpan<byte> data)
    {
        try
        {
            return DecodeCore(data);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException or OverflowException)
        {
            return null;
        }
    }

    private GifAnimation? DecodeCore(ReadOnlySpan<byte> data)
    {
        if (data.Length < 13 || data[0] != (byte)'G' || data[1] != (byte)'I' || data[2] != (byte)'F') return null;

        var reader = new Reader(data.ToArray());
        reader.Skip(6);
        var width = reader.Short();
        var height = reader.Short();
        var flags = reader.Byte();
        var background = reader.Byte();
        reader.Skip(1);
        var global = (flags & 0x80) != 0 ? reader.Palette(2 << (flags & 7)) : null;
        _ = background;

        if (width == 0 || height == 0 || (long)width * height > MaxPixels) return null;

        var canvas = new byte[width * height * 4];
        long budget = MaxFrameBytes;
        var frames = new List<GifFrame>();
        var loops = 1;
        var delay = 100;
        var transparent = -1;
        var disposal = 0;

        while (reader.HasMore)
        {
            switch (reader.Byte())
            {
                case 0x21:
                    var label = reader.Byte();
                    var block = reader.Blocks();
                    if (label == 0xF9 && block.Length >= 4)
                    {
                        disposal = (block[0] >> 2) & 7;
                        delay = (block[1] | (block[2] << 8)) * 10;
                        transparent = (block[0] & 1) != 0 ? block[3] : -1;
                    }
                    else if (label == 0xFF && block.Length >= 14 && block[11] == 1)
                    {
                        loops = block[12] | (block[13] << 8);
                    }
                    break;
                case 0x2C:
                    var left = reader.Short();
                    var top = reader.Short();
                    var frameWidth = reader.Short();
                    var frameHeight = reader.Short();
                    var frameFlags = reader.Byte();
                    var palette = (frameFlags & 0x80) != 0 ? reader.Palette(2 << (frameFlags & 7)) : global;
                    var interlaced = (frameFlags & 0x40) != 0;
                    var minimum = reader.Byte();
                    if (minimum is < 2 or > 11) return null;
                    budget -= canvas.Length;
                    if (budget < 0 || (long)frameWidth * frameHeight > MaxPixels) return frames.Count == 0 ? null : new GifAnimation(width, height, frames, loops);
                    var indices = Decompress(reader.Blocks(), minimum, frameWidth * frameHeight);
                    if (palette is null) return null;

                    var before = disposal == 3 ? (byte[])canvas.Clone() : null;
                    Paint(canvas, width, height, indices, palette, transparent, left, top, frameWidth, frameHeight, interlaced);
                    frames.Add(new GifFrame(new BgraImage((byte[])canvas.Clone(), width, height), delay));

                    if (disposal == 2) Clear(canvas, width, height, left, top, frameWidth, frameHeight);
                    else if (disposal == 3 && before is not null) canvas = before;

                    delay = 100;
                    transparent = -1;
                    disposal = 0;
                    break;
                case 0x3B:
                    return frames.Count == 0 ? null : new GifAnimation(width, height, frames, loops);
                default:
                    return frames.Count == 0 ? null : new GifAnimation(width, height, frames, loops);
            }
        }

        return frames.Count == 0 ? null : new GifAnimation(width, height, frames, loops);
    }

    private static void Short(Stream output, int value)
    {
        output.WriteByte((byte)(value & 0xFF));
        output.WriteByte((byte)((value >> 8) & 0xFF));
    }

    private static void WritePalette(Stream output)
    {
        for (var index = 0; index < 256; index++)
        {
            if (index >= Transparent)
            {
                output.Write([0, 0, 0]);
                continue;
            }
            output.WriteByte((byte)(index / 42 * 51));
            output.WriteByte((byte)(index / 6 % 7 * 255 / 6));
            output.WriteByte((byte)(index % 6 * 51));
        }
    }

    private static byte[] Indices(BgraImage image, int width, int height)
    {
        var result = new byte[width * height];
        Array.Fill(result, (byte)Transparent);
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
            {
                var offset = (y * image.Width + x) * 4;
                if (image.Pixels[offset + 3] < 128) continue;
                var red = (image.Pixels[offset + 2] * 5 + 127) / 255;
                var green = (image.Pixels[offset + 1] * 6 + 127) / 255;
                var blue = (image.Pixels[offset] * 5 + 127) / 255;
                result[y * width + x] = (byte)(red * 42 + green * 6 + blue);
            }
        return result;
    }

    private static byte[] Compress(byte[] indices)
    {
        const int clear = 256;
        const int end = 257;
        var output = new List<byte>();
        var bitBuffer = 0;
        var bitCount = 0;
        var codeSize = 9;
        var next = 258;
        var table = new Dictionary<(int Prefix, byte Suffix), int>();

        void Emit(int code)
        {
            bitBuffer |= code << bitCount;
            bitCount += codeSize;
            while (bitCount >= 8)
            {
                output.Add((byte)(bitBuffer & 0xFF));
                bitBuffer >>= 8;
                bitCount -= 8;
            }
        }

        Emit(clear);
        if (indices.Length == 0)
        {
            Emit(end);
            if (bitCount > 0) output.Add((byte)bitBuffer);
            return [.. output];
        }

        var prefix = (int)indices[0];
        for (var index = 1; index < indices.Length; index++)
        {
            var symbol = indices[index];
            if (table.TryGetValue((prefix, symbol), out var existing))
            {
                prefix = existing;
                continue;
            }

            Emit(prefix);
            if (next < 1 << MaxCodeSize)
            {
                table[(prefix, symbol)] = next++;
                if (next > 1 << codeSize && codeSize < MaxCodeSize) codeSize++;
            }
            else
            {
                Emit(clear);
                table.Clear();
                codeSize = 9;
                next = 258;
            }
            prefix = symbol;
        }

        Emit(prefix);
        Emit(end);
        if (bitCount > 0) output.Add((byte)bitBuffer);
        return [.. output];
    }

    private static void WriteBlocks(Stream output, byte[] data)
    {
        for (var offset = 0; offset < data.Length; offset += 255)
        {
            var length = Math.Min(255, data.Length - offset);
            output.WriteByte((byte)length);
            output.Write(data, offset, length);
        }
        output.WriteByte(0);
    }

    private static byte[] Decompress(byte[] data, int minimum, int count)
    {
        var clear = 1 << minimum;
        var end = clear + 1;
        var codeSize = minimum + 1;
        var prefixes = new int[4096];
        var suffixes = new byte[4096];
        var lengths = new int[4096];
        for (var code = 0; code < clear; code++)
        {
            suffixes[code] = (byte)code;
            lengths[code] = 1;
            prefixes[code] = -1;
        }

        var output = new byte[count];
        var written = 0;
        var next = end + 1;
        var previous = -1;
        var bitBuffer = 0;
        var bitCount = 0;
        var position = 0;
        var stack = new byte[4096];

        while (written < count)
        {
            while (bitCount < codeSize && position < data.Length)
            {
                bitBuffer |= data[position++] << bitCount;
                bitCount += 8;
            }
            if (bitCount < codeSize) break;

            var code = bitBuffer & ((1 << codeSize) - 1);
            bitBuffer >>= codeSize;
            bitCount -= codeSize;

            if (code == clear)
            {
                codeSize = minimum + 1;
                next = end + 1;
                previous = -1;
                continue;
            }
            if (code == end) break;

            int first;
            if (previous == -1)
            {
                if (code >= next) break;
                output[written++] = suffixes[code];
                previous = code;
                continue;
            }

            if (code > next) break;
            var current = code < next ? code : previous;
            var depth = 0;
            for (var walk = current; walk >= 0 && depth < stack.Length; walk = prefixes[walk]) stack[depth++] = suffixes[walk];
            first = stack[depth - 1];
            for (var index = depth - 1; index >= 0 && written < count; index--) output[written++] = stack[index];
            if (code >= next && written < count) output[written++] = (byte)first;

            if (next < 4096)
            {
                prefixes[next] = previous;
                suffixes[next] = (byte)first;
                lengths[next] = lengths[previous] + 1;
                next++;
                if (next == 1 << codeSize && codeSize < MaxCodeSize) codeSize++;
            }
            previous = code;
        }

        return output;
    }

    private static void Paint(byte[] canvas, int width, int height, byte[] indices, byte[] palette, int transparent,
        int left, int top, int frameWidth, int frameHeight, bool interlaced)
    {
        var rows = interlaced ? InterlacedRows(frameHeight) : Enumerable.Range(0, frameHeight);
        var source = 0;
        foreach (var row in rows)
        {
            for (var column = 0; column < frameWidth; column++, source++)
            {
                if (source >= indices.Length) return;
                var index = indices[source];
                if (index == transparent) continue;
                var x = left + column;
                var y = top + row;
                if (x >= width || y >= height || index * 3 + 2 >= palette.Length) continue;
                var offset = (y * width + x) * 4;
                canvas[offset] = palette[index * 3 + 2];
                canvas[offset + 1] = palette[index * 3 + 1];
                canvas[offset + 2] = palette[index * 3];
                canvas[offset + 3] = 255;
            }
        }
    }

    private static IEnumerable<int> InterlacedRows(int height)
    {
        foreach (var (start, step) in new[] { (0, 8), (4, 8), (2, 4), (1, 2) })
            for (var row = start; row < height; row += step) yield return row;
    }

    private static void Clear(byte[] canvas, int width, int height, int left, int top, int frameWidth, int frameHeight)
    {
        if (left >= width) return;
        for (var y = top; y < Math.Min(height, top + frameHeight); y++)
            Array.Clear(canvas, (y * width + left) * 4, Math.Max(0, Math.Min(frameWidth, width - left)) * 4);
    }

    private sealed class Reader(byte[] data)
    {
        private int _position;

        public bool HasMore => _position < data.Length;

        public void Skip(int count) => _position += count;

        public byte Byte() => _position < data.Length ? data[_position++] : (byte)0x3B;

        public int Short() => Byte() | (Byte() << 8);

        public byte[] Palette(int entries)
        {
            var length = Math.Min(entries * 3, data.Length - _position);
            var palette = data.AsSpan(_position, Math.Max(0, length)).ToArray();
            _position += Math.Max(0, length);
            return palette;
        }

        public byte[] Blocks()
        {
            using var output = new MemoryStream();
            while (HasMore)
            {
                var length = data[_position++];
                if (length == 0) break;
                var available = Math.Min(length, data.Length - _position);
                output.Write(data, _position, available);
                _position += available;
            }
            return output.ToArray();
        }
    }
}
