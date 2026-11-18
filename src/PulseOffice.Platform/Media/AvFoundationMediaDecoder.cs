using System.Runtime.InteropServices;

namespace PulseOffice.Platform.Media;

public sealed class AvFoundationMediaDecoder : IMediaDecoder
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string CoreMedia = "/System/Library/Frameworks/CoreMedia.framework/CoreMedia";
    private const string CoreVideo = "/System/Library/Frameworks/CoreVideo.framework/CoreVideo";
    private const string AvFoundation = "/System/Library/Frameworks/AVFoundation.framework/AVFoundation";
    private const uint PixelFormatBgra = 0x42475241;
    private const uint FormatLinearPcm = 0x6C70636D;

    private readonly Lazy<bool> _loaded = new(() => NativeLibrary.TryLoad(AvFoundation, out _));

    public bool IsAvailable => OperatingSystem.IsMacOS() && _loaded.Value;

    public Task<MediaInfo?> ProbeAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Task.Run(() => Guarded(() => Probe(path)), ct);
    }

    public IMediaAudioStream? OpenAudio(string path, TimeSpan start, PcmFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        return Guarded<IMediaAudioStream>(() =>
        {
            var pool = objc_autoreleasePoolPush();
            try
            {
                var settings = Send(Class("NSMutableDictionary"), "dictionary");
                Put(settings, "AVFormatIDKey", Number(FormatLinearPcm));
                Put(settings, "AVSampleRateKey", Number((uint)format.SampleRate));
                Put(settings, "AVNumberOfChannelsKey", Number((uint)format.Channels));
                Put(settings, "AVLinearPCMBitDepthKey", Number(16));
                Put(settings, "AVLinearPCMIsFloatKey", Number(0));
                Put(settings, "AVLinearPCMIsBigEndianKey", Number(0));
                Put(settings, "AVLinearPCMIsNonInterleaved", Number(0));
                return OpenReader(path, "soun", settings, start) is { } reader ? new AudioStream(reader) : null;
            }
            finally
            {
                objc_autoreleasePoolPop(pool);
            }
        });
    }

    public IMediaVideoStream? OpenVideo(string path, MediaInfo info, TimeSpan start)
    {
        ArgumentNullException.ThrowIfNull(info);
        return Guarded<IMediaVideoStream>(() =>
        {
            var pool = objc_autoreleasePoolPush();
            try
            {
                var settings = Send(Class("NSMutableDictionary"), "dictionary");
                Put(settings, "PixelFormatType", Number(PixelFormatBgra));
                return OpenReader(path, "vide", settings, start) is { } reader ? new VideoStream(reader) : null;
            }
            finally
            {
                objc_autoreleasePoolPop(pool);
            }
        });
    }

    private MediaInfo? Probe(string path)
    {
        var pool = objc_autoreleasePoolPush();
        try
        {
            var asset = Asset(path);
            if (asset == IntPtr.Zero) return null;
            var duration = Seconds(Duration(asset));
            var audio = Track(asset, "soun");
            var video = Track(asset, "vide");
            var size = video == IntPtr.Zero ? default : objc_msgSend_size(video, Selector("naturalSize"));
            var width = (int)Math.Abs(size.Width);
            var height = (int)Math.Abs(size.Height);
            return duration > 0
                ? new MediaInfo(TimeSpan.FromSeconds(duration), audio != IntPtr.Zero, width > 0 && height > 0, width, height)
                : null;
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    private static T? Guarded<T>(Func<T?> action) where T : class
    {
        try
        {
            return action();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or MarshalDirectiveException)
        {
            return null;
        }
    }

    private IntPtr Asset(string path)
    {
        var url = Send(Class("NSURL"), "fileURLWithPath:", Text(path));
        return url == IntPtr.Zero ? IntPtr.Zero : Send(Class("AVURLAsset"), "URLAssetWithURL:options:", url, IntPtr.Zero);
    }

    private IntPtr Track(IntPtr asset, string mediaType)
    {
        var tracks = Send(asset, "tracksWithMediaType:", Text(mediaType));
        return tracks == IntPtr.Zero || objc_msgSend_count(tracks, Selector("count")) == 0
            ? IntPtr.Zero
            : objc_msgSend_index(tracks, Selector("objectAtIndex:"), 0);
    }

    private CMTime Duration(IntPtr asset) =>
        RuntimeInformation.ProcessArchitecture == Architecture.X64
            ? StretDuration(asset)
            : objc_msgSend_time(asset, Selector("duration"));

    private CMTime StretDuration(IntPtr asset)
    {
        objc_msgSend_stret_time(out var time, asset, Selector("duration"));
        return time;
    }

    private double Seconds(CMTime time) => CMTimeGetSeconds(time);

    private IntPtr Text(string value) => Send(Class("NSString"), "stringWithUTF8String:", value);

    private IntPtr Number(uint value) => objc_msgSend_uint(Class("NSNumber"), Selector("numberWithUnsignedInt:"), value);

    private void Put(IntPtr dictionary, string key, IntPtr value) =>
        objc_msgSend_void2(dictionary, Selector("setObject:forKey:"), value, Text(key));

    private IntPtr Class(string name) => objc_getClass(name);

    private IntPtr Selector(string name) => sel_registerName(name);

    private IntPtr Send(IntPtr target, string selector) => objc_msgSend(target, Selector(selector));

    private IntPtr Send(IntPtr target, string selector, IntPtr argument) => objc_msgSend(target, Selector(selector), argument);

    private IntPtr Send(IntPtr target, string selector, IntPtr first, IntPtr second) => objc_msgSend(target, Selector(selector), first, second);

    private IntPtr Send(IntPtr target, string selector, string argument) => objc_msgSend_text(target, Selector(selector), argument);

    private Reader? OpenReader(string path, string mediaType, IntPtr settings, TimeSpan start)
    {
        var asset = Asset(path);
        if (asset == IntPtr.Zero) return null;
        var track = Track(asset, mediaType);
        if (track == IntPtr.Zero) return null;
        var reader = Send(Class("AVAssetReader"), "assetReaderWithAsset:error:", asset, IntPtr.Zero);
        if (reader == IntPtr.Zero) return null;
        var output = Send(Class("AVAssetReaderTrackOutput"), "assetReaderTrackOutputWithTrack:outputSettings:", track, settings);
        if (output == IntPtr.Zero) return null;
        Send(reader, "addOutput:", output);
        if (start > TimeSpan.Zero)
        {
            var duration = Seconds(Duration(asset));
            var range = new CMTimeRange(
                CMTimeMakeWithSeconds(start.TotalSeconds, 600),
                CMTimeMakeWithSeconds(Math.Max(0, duration - start.TotalSeconds), 600));
            objc_msgSend_range(reader, Selector("setTimeRange:"), range);
        }

        if (objc_msgSend_bool(reader, Selector("startReading")) == 0) return null;
        Send(reader, "retain");
        Send(output, "retain");
        return new Reader(this, reader, output);
    }

    private sealed class Reader : IDisposable
    {
        private readonly AvFoundationMediaDecoder _owner;
        private readonly IntPtr _reader;
        private readonly IntPtr _output;

        public Reader(AvFoundationMediaDecoder owner, IntPtr reader, IntPtr output)
        {
            _owner = owner;
            _reader = reader;
            _output = output;
        }

        public T? Next<T>(Func<IntPtr, T?> read) where T : class
        {
            var pool = objc_autoreleasePoolPush();
            try
            {
                while (true)
                {
                    var sample = _owner.Send(_output, "copyNextSampleBuffer");
                    if (sample == IntPtr.Zero) return null;
                    try
                    {
                        if (read(sample) is { } value) return value;
                    }
                    finally
                    {
                        CFRelease(sample);
                    }
                }
            }
            finally
            {
                objc_autoreleasePoolPop(pool);
            }
        }

        public double Seconds(IntPtr sample) => _owner.Seconds(CMSampleBufferGetPresentationTimeStamp(sample));

        public void Dispose()
        {
            _owner.Send(_reader, "cancelReading");
            _owner.Send(_output, "release");
            _owner.Send(_reader, "release");
        }
    }

    private sealed class AudioStream(Reader reader) : IMediaAudioStream
    {
        private byte[] _pending = [];
        private int _offset;

        public int Read(Span<byte> pcm)
        {
            if (_offset >= _pending.Length)
            {
                if (reader.Next(Copy) is not { } next) return 0;
                _pending = next;
                _offset = 0;
            }

            var count = Math.Min(pcm.Length, _pending.Length - _offset);
            _pending.AsSpan(_offset, count).CopyTo(pcm);
            _offset += count;
            return count;
        }

        public void Dispose() => reader.Dispose();

        private byte[]? Copy(IntPtr sample)
        {
            var block = CMSampleBufferGetDataBuffer(sample);
            if (block == IntPtr.Zero) return null;
            var length = (int)CMBlockBufferGetDataLength(block);
            if (length <= 0) return null;
            var bytes = new byte[length];
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return CMBlockBufferCopyDataBytes(block, 0, (nuint)length, handle.AddrOfPinnedObject()) == 0 ? bytes : null;
            }
            finally
            {
                handle.Free();
            }
        }
    }

    private sealed class VideoStream(Reader reader) : IMediaVideoStream
    {
        public MediaFrame? Next() => reader.Next(Copy);

        public void Dispose() => reader.Dispose();

        private MediaFrame? Copy(IntPtr sample)
        {
            var image = CMSampleBufferGetImageBuffer(sample);
            if (image == IntPtr.Zero) return null;
            if (CVPixelBufferLockBaseAddress(image, 1) != 0) return null;
            try
            {
                var width = (int)CVPixelBufferGetWidth(image);
                var height = (int)CVPixelBufferGetHeight(image);
                var stride = (int)CVPixelBufferGetBytesPerRow(image);
                var source = CVPixelBufferGetBaseAddress(image);
                if (width <= 0 || height <= 0 || source == IntPtr.Zero) return null;
                var bgra = new byte[width * height * 4];
                for (var row = 0; row < height; row++)
                    Marshal.Copy(source + row * stride, bgra, row * width * 4, width * 4);
                return new MediaFrame(width, height, bgra, TimeSpan.FromSeconds(reader.Seconds(sample)));
            }
            finally
            {
                CVPixelBufferUnlockBaseAddress(image, 1);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CMTime(long value, int timescale, uint flags, long epoch)
    {
        public readonly long Value = value;
        public readonly int Timescale = timescale;
        public readonly uint Flags = flags;
        public readonly long Epoch = epoch;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CMTimeRange(CMTime start, CMTime duration)
    {
        public readonly CMTime Start = start;
        public readonly CMTime Duration = duration;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CGSize(double width, double height)
    {
        public readonly double Width = width;
        public readonly double Height = height;
    }

    [DllImport(ObjectiveC)]
    private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC)]
    private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC)]
    private static extern IntPtr objc_autoreleasePoolPush();

    [DllImport(ObjectiveC)]
    private static extern void objc_autoreleasePoolPop(IntPtr pool);

    [DllImport(ObjectiveC)]
    private static extern IntPtr objc_msgSend(IntPtr target, IntPtr selector);

    [DllImport(ObjectiveC)]
    private static extern IntPtr objc_msgSend(IntPtr target, IntPtr selector, IntPtr argument);

    [DllImport(ObjectiveC)]
    private static extern IntPtr objc_msgSend(IntPtr target, IntPtr selector, IntPtr first, IntPtr second);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_text(IntPtr target, IntPtr selector, [MarshalAs(UnmanagedType.LPUTF8Str)] string argument);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_uint(IntPtr target, IntPtr selector, uint argument);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void2(IntPtr target, IntPtr selector, IntPtr first, IntPtr second);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern nuint objc_msgSend_count(IntPtr target, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_index(IntPtr target, IntPtr selector, nuint index);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern sbyte objc_msgSend_bool(IntPtr target, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern CGSize objc_msgSend_size(IntPtr target, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern CMTime objc_msgSend_time(IntPtr target, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend_stret")]
    private static extern void objc_msgSend_stret_time(out CMTime result, IntPtr target, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_range(IntPtr target, IntPtr selector, CMTimeRange range);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(IntPtr value);

    [DllImport(CoreMedia)]
    private static extern double CMTimeGetSeconds(CMTime time);

    [DllImport(CoreMedia)]
    private static extern CMTime CMTimeMakeWithSeconds(double seconds, int preferredTimescale);

    [DllImport(CoreMedia)]
    private static extern CMTime CMSampleBufferGetPresentationTimeStamp(IntPtr sample);

    [DllImport(CoreMedia)]
    private static extern IntPtr CMSampleBufferGetDataBuffer(IntPtr sample);

    [DllImport(CoreMedia)]
    private static extern IntPtr CMSampleBufferGetImageBuffer(IntPtr sample);

    [DllImport(CoreMedia)]
    private static extern nuint CMBlockBufferGetDataLength(IntPtr block);

    [DllImport(CoreMedia)]
    private static extern int CMBlockBufferCopyDataBytes(IntPtr block, nuint offset, nuint length, IntPtr destination);

    [DllImport(CoreVideo)]
    private static extern int CVPixelBufferLockBaseAddress(IntPtr buffer, ulong flags);

    [DllImport(CoreVideo)]
    private static extern int CVPixelBufferUnlockBaseAddress(IntPtr buffer, ulong flags);

    [DllImport(CoreVideo)]
    private static extern IntPtr CVPixelBufferGetBaseAddress(IntPtr buffer);

    [DllImport(CoreVideo)]
    private static extern nuint CVPixelBufferGetBytesPerRow(IntPtr buffer);

    [DllImport(CoreVideo)]
    private static extern nuint CVPixelBufferGetWidth(IntPtr buffer);

    [DllImport(CoreVideo)]
    private static extern nuint CVPixelBufferGetHeight(IntPtr buffer);
}
