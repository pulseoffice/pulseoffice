namespace PulseOffice.Platform.Mac;

public sealed class Mp4Writer(int width, int height, int timescale, byte[] sps, byte[] pps)
{
    public byte[] Build(IReadOnlyList<byte[]> samples, IReadOnlyList<long> durations)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(durations);

        if (samples.Count != durations.Count)
            throw new ArgumentException("Every sample needs a duration.", nameof(durations));

        var ftyp = new LeafBox("ftyp", box => box.Ascii("isom").U32(0x200).Ascii("isom").Ascii("iso2").Ascii("avc1").Ascii("mp41")).Encode();
        var payload = samples.Sum(sample => (long)sample.Length);
        var mdatSize = 8 + payload;
        var track = new TrackLayout(samples, durations, ftyp.Length + 8);
        var moov = Movie(track).Encode();

        var total = ftyp.Length + mdatSize + moov.Length;
        if (mdatSize > uint.MaxValue || total > Array.MaxLength)
            throw new Mp4TooLargeException(total);

        var output = new byte[total];
        var offset = 0;

        Buffer.BlockCopy(ftyp, 0, output, offset, ftyp.Length);
        offset += ftyp.Length;

        new BoxWriter().U32(mdatSize).Ascii("mdat").ToArray().CopyTo(output, offset);
        offset += 8;

        foreach (var sample in samples)
        {
            Buffer.BlockCopy(sample, 0, output, offset, sample.Length);
            offset += sample.Length;
        }

        Buffer.BlockCopy(moov, 0, output, offset, moov.Length);
        return output;
    }

    private Mp4Box Movie(TrackLayout track) =>
        new ContainerBox("moov",
            new LeafBox("mvhd", box => box
                .U32(0).U32(0).U32(0).U32(timescale).U32(track.Duration)
                .U32(0x00010000).U16(0x0100).U16(0).U32(0).U32(0)
                .UnityMatrix().Zeros(24).U32(2)),
            new ContainerBox("trak", Header(track), Media(track)));

    private Mp4Box Header(TrackLayout track) =>
        new LeafBox("tkhd", box => box
            .U32(0x00000007).U32(0).U32(0).U32(1).U32(0).U32((uint)track.Duration)
            .U32(0).U32(0).U16(0).U16(0).U16(0).U16(0)
            .UnityMatrix()
            .U32((uint)(width << 16)).U32((uint)(height << 16)));

    private Mp4Box Media(TrackLayout track) =>
        new ContainerBox("mdia",
            new LeafBox("mdhd", box => box.U32(0).U32(0).U32(0).U32(timescale).U32(track.Duration).U16(0x55c4).U16(0)),
            new LeafBox("hdlr", box => box.U32(0).U32(0).Ascii("vide").U32(0).U32(0).U32(0).Ascii("PulseOffice").Zeros(1)),
            new ContainerBox("minf",
                new LeafBox("vmhd", box => box.U32(0x00000001).U16(0).U16(0).U16(0).U16(0)),
                new ContainerBox("dinf",
                    new LeafBox("dref", box => box.U32(0).U32(1).Box(new LeafBox("url ", inner => inner.U32(0x00000001))))),
                SampleTable(track)));

    private Mp4Box SampleTable(TrackLayout track) =>
        new ContainerBox("stbl",
            new LeafBox("stsd", box => box.U32(0).U32(1).Box(VisualEntry())),
            new LeafBox("stts", box =>
            {
                box.U32(0).U32((uint)track.Runs.Count);
                foreach (var (count, duration) in track.Runs) box.U32((uint)count).U32((uint)duration);
            }),
            new LeafBox("stsz", box =>
            {
                box.U32(0).U32(0).U32((uint)track.Samples.Count);
                foreach (var sample in track.Samples) box.U32((uint)sample.Length);
            }),
            new LeafBox("stsc", box => box.U32(0).U32(1).U32(1).U32((uint)track.Samples.Count).U32(1)),
            new LeafBox("stco", box => box.U32(0).U32(1).U32((uint)track.ChunkOffset)));

    private Mp4Box VisualEntry() =>
        new LeafBox("avc1", box => box
            .Zeros(6).U16(1).Zeros(16)
            .U16((ushort)width).U16((ushort)height)
            .U32(0x00480000).U32(0x00480000).U32(0).U16(1)
            .Zeros(32).U16(0x0018).U16(0xFFFF)
            .Box(new LeafBox("avcC", inner => inner
                .Bytes([1, sps[1], sps[2], sps[3], 0xFF, 0xE1])
                .U16((ushort)sps.Length).Bytes(sps)
                .Bytes([1]).U16((ushort)pps.Length).Bytes(pps))));
}

internal sealed class TrackLayout
{
    public TrackLayout(IReadOnlyList<byte[]> samples, IReadOnlyList<long> durations, long chunkOffset)
    {
        Samples = samples;
        ChunkOffset = chunkOffset;
        Duration = durations.Sum();

        var runs = new List<(int, long)>();
        for (var i = 0; i < durations.Count;)
        {
            var j = i;
            while (j < durations.Count && durations[j] == durations[i]) j++;
            runs.Add((j - i, durations[i]));
            i = j;
        }

        Runs = runs;
    }

    public IReadOnlyList<byte[]> Samples { get; }

    public long ChunkOffset { get; }

    public long Duration { get; }

    public IReadOnlyList<(int Count, long Duration)> Runs { get; }
}

public sealed class Mp4TooLargeException(long size)
    : InvalidOperationException($"The video would be {size} bytes, beyond what a 32-bit mp4 written in memory can hold.");

internal abstract class Mp4Box(string type)
{
    private byte[]? _content;

    public int Size => 8 + Contents.Length;

    private byte[] Contents => _content ??= Content();

    public byte[] Encode()
    {
        var content = Contents;
        return new BoxWriter().U32((uint)(8 + content.Length)).Ascii(type).Bytes(content).ToArray();
    }

    protected abstract byte[] Content();
}

internal sealed class LeafBox(string type, Action<BoxWriter> write) : Mp4Box(type)
{
    protected override byte[] Content()
    {
        var writer = new BoxWriter();
        write(writer);
        return writer.ToArray();
    }
}

internal sealed class ContainerBox(string type, params Mp4Box[] children) : Mp4Box(type)
{
    protected override byte[] Content() => [.. children.SelectMany(child => child.Encode())];
}

internal sealed class BoxWriter
{
    private readonly List<byte> _bytes = [];

    public BoxWriter U32(long value)
    {
        _bytes.AddRange([(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value]);
        return this;
    }

    public BoxWriter U16(int value)
    {
        _bytes.AddRange([(byte)(value >> 8), (byte)value]);
        return this;
    }

    public BoxWriter Ascii(string value) => Bytes(System.Text.Encoding.ASCII.GetBytes(value));

    public BoxWriter Zeros(int count) => Bytes(new byte[count]);

    public BoxWriter Bytes(byte[] value)
    {
        _bytes.AddRange(value);
        return this;
    }

    public BoxWriter Box(Mp4Box box) => Bytes(box.Encode());

    public BoxWriter UnityMatrix() =>
        U32(0x00010000).U32(0).U32(0).U32(0).U32(0x00010000).U32(0).U32(0).U32(0).U32(0x40000000);

    public byte[] ToArray() => [.. _bytes];
}
