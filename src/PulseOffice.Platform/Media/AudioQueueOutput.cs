using System.Runtime.InteropServices;

namespace PulseOffice.Platform.Media;

public sealed class AudioQueueOutputFactory : IAudioOutputFactory
{
    public bool IsAvailable => OperatingSystem.IsMacOS();

    public IAudioOutput? Open(PcmFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (!IsAvailable) return null;
        try
        {
            var output = new AudioQueueOutput(format);
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

public sealed class AudioQueueOutput : IAudioOutput
{
    private const string AudioToolbox = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";
    private const uint FormatLinearPcm = 0x6C70636D;
    private const uint SignedPackedFlags = 0x4 | 0x8;
    private const int BufferCount = 4;
    private const int BufferBytes = 19_200;
    private const int AudioDataOffset = 8;
    private const int AudioDataByteSizeOffset = 16;

    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _free = new(BufferCount, BufferCount);
    private readonly Queue<IntPtr> _idle = new();
    private readonly OutputCallback _callback;
    private readonly IntPtr _queue;
    private readonly int _bytesPerSecond;
    private long _playedBytes;
    private bool _started;
    private volatile bool _disposed;

    public AudioQueueOutput(PcmFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        _callback = Released;
        _bytesPerSecond = format.BytesPerSecond;
        var description = new StreamDescription(format.SampleRate, FormatLinearPcm, SignedPackedFlags,
            (uint)(format.Channels * 2), 1, (uint)(format.Channels * 2), (uint)format.Channels, 16);
        IsOpen = AudioQueueNewOutput(ref description, Marshal.GetFunctionPointerForDelegate(_callback),
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out _queue) == 0;
        if (!IsOpen) return;
        for (var index = 0; index < BufferCount; index++)
            if (AudioQueueAllocateBuffer(_queue, BufferBytes, out var buffer) == 0) _idle.Enqueue(buffer);
        IsOpen = _idle.Count == BufferCount;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void OutputCallback(IntPtr user, IntPtr queue, IntPtr buffer);

    public bool IsOpen { get; }

    public TimeSpan Played => TimeSpan.FromSeconds((double)Interlocked.Read(ref _playedBytes) / _bytesPerSecond);

    public void Write(ReadOnlySpan<byte> pcm)
    {
        while (!pcm.IsEmpty)
        {
            while (!_free.Wait(20)) ObjectDisposedException.ThrowIf(_disposed, this);
            ObjectDisposedException.ThrowIf(_disposed, this);
            var count = Math.Min(pcm.Length, BufferBytes);
            lock (_gate)
            {
                var buffer = _idle.Dequeue();
                var data = Marshal.ReadIntPtr(buffer, AudioDataOffset);
                Marshal.Copy(pcm[..count].ToArray(), 0, data, count);
                Marshal.WriteInt32(buffer, AudioDataByteSizeOffset, count);
                AudioQueueEnqueueBuffer(_queue, buffer, 0, IntPtr.Zero);
                if (!_started) _started = AudioQueueStart(_queue, IntPtr.Zero) == 0;
            }

            pcm = pcm[count..];
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_gate)
        {
            if (IsOpen || _queue != IntPtr.Zero)
            {
                AudioQueueStop(_queue, 1);
                AudioQueueDispose(_queue, 1);
            }
        }

        _free.Dispose();
        GC.KeepAlive(_callback);
    }

    private void Released(IntPtr user, IntPtr queue, IntPtr buffer)
    {
        if (_disposed) return;
        Interlocked.Add(ref _playedBytes, Marshal.ReadInt32(buffer, AudioDataByteSizeOffset));
        lock (_gate) _idle.Enqueue(buffer);
        _free.Release();
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct StreamDescription(double sampleRate, uint formatId, uint formatFlags, uint bytesPerPacket,
        uint framesPerPacket, uint bytesPerFrame, uint channelsPerFrame, uint bitsPerChannel)
    {
        public readonly double SampleRate = sampleRate;
        public readonly uint FormatId = formatId;
        public readonly uint FormatFlags = formatFlags;
        public readonly uint BytesPerPacket = bytesPerPacket;
        public readonly uint FramesPerPacket = framesPerPacket;
        public readonly uint BytesPerFrame = bytesPerFrame;
        public readonly uint ChannelsPerFrame = channelsPerFrame;
        public readonly uint BitsPerChannel = bitsPerChannel;
        public readonly uint Reserved = 0;
    }

    [DllImport(AudioToolbox)]
    private static extern int AudioQueueNewOutput(ref StreamDescription format, IntPtr callback, IntPtr user,
        IntPtr runLoop, IntPtr runLoopMode, uint flags, out IntPtr queue);

    [DllImport(AudioToolbox)]
    private static extern int AudioQueueAllocateBuffer(IntPtr queue, uint size, out IntPtr buffer);

    [DllImport(AudioToolbox)]
    private static extern int AudioQueueEnqueueBuffer(IntPtr queue, IntPtr buffer, uint descriptionCount, IntPtr descriptions);

    [DllImport(AudioToolbox)]
    private static extern int AudioQueueStart(IntPtr queue, IntPtr startTime);

    [DllImport(AudioToolbox)]
    private static extern int AudioQueueStop(IntPtr queue, byte immediate);

    [DllImport(AudioToolbox)]
    private static extern int AudioQueueDispose(IntPtr queue, byte immediate);
}
