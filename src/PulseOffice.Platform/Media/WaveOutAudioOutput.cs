using System.Runtime.InteropServices;

namespace PulseOffice.Platform.Media;

public sealed class WaveOutAudioOutputFactory : IAudioOutputFactory
{
    public bool IsAvailable => OperatingSystem.IsWindows();

    public IAudioOutput? Open(PcmFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (!IsAvailable) return null;
        try
        {
            var output = new WaveOutAudioOutput(format);
            if (output.IsOpen) return output;
            output.Dispose();
            return null;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }
}

public sealed class WaveOutAudioOutput : IAudioOutput
{
    private const uint WaveMapper = 0xFFFFFFFF;
    private const uint HeaderDone = 0x00000001;
    private const int BufferCount = 4;
    private const int BufferBytes = 19_200;

    private readonly Lock _gate = new();
    private readonly IntPtr _device;
    private readonly IntPtr[] _headers = new IntPtr[BufferCount];
    private readonly IntPtr[] _data = new IntPtr[BufferCount];
    private readonly int _headerSize = Marshal.SizeOf<WaveHeader>();
    private const uint TimeSamples = 0x0002;

    private readonly int _sampleRate;
    private int _next;
    private volatile bool _disposed;

    public WaveOutAudioOutput(PcmFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        var wave = new WaveFormat(1, (ushort)format.Channels, (uint)format.SampleRate,
            (uint)format.BytesPerSecond, (ushort)(format.Channels * 2), 16, 0);
        _sampleRate = format.SampleRate;
        IsOpen = waveOutOpen(out var device, WaveMapper, ref wave, IntPtr.Zero, IntPtr.Zero, 0) == 0;
        _device = device;
        for (var index = 0; index < BufferCount; index++)
        {
            _data[index] = Marshal.AllocHGlobal(BufferBytes);
            _headers[index] = Marshal.AllocHGlobal(_headerSize);
            Marshal.StructureToPtr(new WaveHeader(_data[index], BufferBytes, HeaderDone), _headers[index], false);
        }
    }

    public bool IsOpen { get; }

    public TimeSpan Played
    {
        get
        {
            if (!IsOpen || _disposed) return TimeSpan.Zero;
            var time = new MultimediaTime(TimeSamples, 0, 0);
            if (waveOutGetPosition(_device, ref time, (uint)Marshal.SizeOf<MultimediaTime>()) != 0 || time.Type != TimeSamples) return TimeSpan.Zero;
            return TimeSpan.FromSeconds((double)time.Value / _sampleRate);
        }
    }

    public void Write(ReadOnlySpan<byte> pcm)
    {
        lock (_gate) Queue(pcm);
    }

    private void Queue(ReadOnlySpan<byte> pcm)
    {
        while (!pcm.IsEmpty)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var header = _headers[_next];
            while (!Done(header))
            {
                Thread.Sleep(5);
                ObjectDisposedException.ThrowIf(_disposed, this);
            }

            waveOutUnprepareHeader(_device, header, (uint)_headerSize);
            var count = Math.Min(pcm.Length, BufferBytes);
            Marshal.Copy(pcm[..count].ToArray(), 0, _data[_next], count);
            Marshal.StructureToPtr(new WaveHeader(_data[_next], count, 0), header, false);
            waveOutPrepareHeader(_device, header, (uint)_headerSize);
            waveOutWrite(_device, header, (uint)_headerSize);
            _next = (_next + 1) % BufferCount;
            pcm = pcm[count..];
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_gate)
        {
            if (IsOpen)
            {
                waveOutReset(_device);
                foreach (var header in _headers) waveOutUnprepareHeader(_device, header, (uint)_headerSize);
                waveOutClose(_device);
            }

            foreach (var header in _headers) Marshal.FreeHGlobal(header);
            foreach (var data in _data) Marshal.FreeHGlobal(data);
        }
    }

    private bool Done(IntPtr header) => (Marshal.PtrToStructure<WaveHeader>(header).Flags & HeaderDone) != 0;

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private readonly struct WaveFormat(ushort tag, ushort channels, uint samplesPerSecond, uint averageBytesPerSecond, ushort blockAlign, ushort bitsPerSample, ushort extraSize)
    {
        public readonly ushort Tag = tag;
        public readonly ushort Channels = channels;
        public readonly uint SamplesPerSecond = samplesPerSecond;
        public readonly uint AverageBytesPerSecond = averageBytesPerSecond;
        public readonly ushort BlockAlign = blockAlign;
        public readonly ushort BitsPerSample = bitsPerSample;
        public readonly ushort ExtraSize = extraSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct WaveHeader(IntPtr data, int length, uint flags)
    {
        public readonly IntPtr Data = data;
        public readonly uint BufferLength = (uint)length;
        public readonly uint BytesRecorded = 0;
        public readonly IntPtr User = IntPtr.Zero;
        public readonly uint Flags = flags;
        public readonly uint Loops = 0;
        public readonly IntPtr Next = IntPtr.Zero;
        public readonly IntPtr Reserved = IntPtr.Zero;
    }

    [DllImport("winmm.dll")]
    private static extern int waveOutOpen(out IntPtr device, uint deviceId, ref WaveFormat format, IntPtr callback, IntPtr instance, uint flags);

    [DllImport("winmm.dll")]
    private static extern int waveOutPrepareHeader(IntPtr device, IntPtr header, uint size);

    [DllImport("winmm.dll")]
    private static extern int waveOutUnprepareHeader(IntPtr device, IntPtr header, uint size);

    [DllImport("winmm.dll")]
    private static extern int waveOutWrite(IntPtr device, IntPtr header, uint size);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct MultimediaTime(uint type, uint value, uint padding)
    {
        public readonly uint Type = type;
        public readonly uint Value = value;
        public readonly uint Padding = padding;
    }

    [DllImport("winmm.dll")]
    private static extern int waveOutGetPosition(IntPtr device, ref MultimediaTime time, uint size);

    [DllImport("winmm.dll")]
    private static extern int waveOutReset(IntPtr device);

    [DllImport("winmm.dll")]
    private static extern int waveOutClose(IntPtr device);
}
