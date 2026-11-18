using System.Globalization;
using System.Runtime.InteropServices;
using Vortice.MediaFoundation;

namespace PulseOffice.Platform.Media;

public sealed class MediaFoundationMediaDecoder : IMediaDecoder
{
    private static readonly Guid MajorTypeVideo = new("73646976-0000-0010-8000-00AA00389B71");
    private static readonly Guid MajorTypeAudio = new("73647561-0000-0010-8000-00AA00389B71");

    public bool IsAvailable => OperatingSystem.IsWindows();

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
            var reader = new Reader(path, SourceReaderIndex.FirstAudioStream, false);
            using var type = MediaFactory.MFCreateMediaType();
            type.Set(MediaTypeAttributeKeys.MajorType, MajorTypeAudio);
            type.Set(MediaTypeAttributeKeys.Subtype, AudioFormatGuids.Pcm);
            type.Set(MediaTypeAttributeKeys.AudioNumChannels, (uint)format.Channels);
            type.Set(MediaTypeAttributeKeys.AudioSamplesPerSecond, (uint)format.SampleRate);
            type.Set(MediaTypeAttributeKeys.AudioBitsPerSample, 16u);
            type.Set(MediaTypeAttributeKeys.AudioBlockAlignment, (uint)(format.Channels * 2));
            type.Set(MediaTypeAttributeKeys.AudioAvgBytesPerSecond, (uint)format.BytesPerSecond);
            reader.Use(type, start);
            return new AudioStream(reader);
        });
    }

    public IMediaVideoStream? OpenVideo(string path, MediaInfo info, TimeSpan start)
    {
        ArgumentNullException.ThrowIfNull(info);
        return Guarded<IMediaVideoStream>(() =>
        {
            var reader = new Reader(path, SourceReaderIndex.FirstVideoStream, true);
            using var type = MediaFactory.MFCreateMediaType();
            type.Set(MediaTypeAttributeKeys.MajorType, MajorTypeVideo);
            type.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.Rgb32);
            reader.Use(type, start);
            return new VideoStream(reader, info.Width, info.Height, start);
        });
    }

    private static MediaInfo? Probe(string path)
    {
        using var reader = new Reader(path, SourceReaderIndex.AllStreams, false);
        var duration = TimeSpan.FromTicks(Convert.ToInt64(
            reader.Source.GetPresentationAttribute(SourceReaderIndex.MediaSource, PresentationDescriptionAttributeKeys.Duration),
            CultureInfo.InvariantCulture));
        var hasAudio = reader.Has(SourceReaderIndex.FirstAudioStream);
        var size = reader.FrameSize();
        return duration > TimeSpan.Zero
            ? new MediaInfo(duration, hasAudio, size is not null, size?.Width ?? 0, size?.Height ?? 0)
            : null;
    }

    private static T? Guarded<T>(Func<T?> action) where T : class
    {
        try
        {
            return action();
        }
        catch (Exception ex) when (ex is DllNotFoundException or SharpGen.Runtime.SharpGenException or COMException
            or EntryPointNotFoundException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private sealed class Reader : IDisposable
    {
        private readonly SourceReaderIndex _stream;

        public Reader(string path, SourceReaderIndex stream, bool videoProcessing)
        {
            MediaFactory.MFStartup(false);
            _stream = stream;
            using var attributes = MediaFactory.MFCreateAttributes(1);
            if (videoProcessing) attributes.Set(SourceReaderAttributeKeys.EnableVideoProcessing, 1u);
            Source = MediaFactory.MFCreateSourceReaderFromURL(path, attributes);
        }

        public IMFSourceReader Source { get; }

        public void Use(IMFMediaType type, TimeSpan start)
        {
            Source.SetStreamSelection(SourceReaderIndex.AllStreams, false);
            Source.SetStreamSelection(_stream, true);
            Source.SetCurrentMediaType(_stream, type);
            if (start > TimeSpan.Zero) Source.SetCurrentPosition(start.Ticks);
        }

        public bool Has(SourceReaderIndex stream)
        {
            try
            {
                using var type = Source.GetNativeMediaType(stream, 0);
                return true;
            }
            catch (SharpGen.Runtime.SharpGenException)
            {
                return false;
            }
        }

        public (int Width, int Height)? FrameSize()
        {
            try
            {
                using var type = Source.GetNativeMediaType(SourceReaderIndex.FirstVideoStream, 0);
                var packed = type.GetUInt64(MediaTypeAttributeKeys.FrameSize);
                var width = (int)(packed >> 32);
                var height = (int)(packed & 0xFFFFFFFF);
                return width > 0 && height > 0 ? (width, height) : null;
            }
            catch (SharpGen.Runtime.SharpGenException)
            {
                return null;
            }
        }

        public byte[]? Next(out TimeSpan time)
        {
            while (true)
            {
                using var sample = Source.ReadSample(_stream, SourceReaderControlFlag.None, out _, out var flags, out var timestamp);
                time = TimeSpan.FromTicks(timestamp);
                if ((flags & (SourceReaderFlag.EndOfStream | SourceReaderFlag.Error)) != 0) return null;
                if (sample is null) continue;
                using var buffer = sample.ConvertToContiguousBuffer();
                buffer.Lock(out var address, out _, out var length);
                try
                {
                    var bytes = new byte[length];
                    Marshal.Copy(address, bytes, 0, length);
                    return bytes;
                }
                finally
                {
                    buffer.Unlock();
                }
            }
        }

        public void Dispose()
        {
            Source.Dispose();
            MediaFactory.MFShutdown();
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
                if (reader.Next(out _) is not { } next) return 0;
                _pending = next;
                _offset = 0;
            }

            var count = Math.Min(pcm.Length, _pending.Length - _offset);
            _pending.AsSpan(_offset, count).CopyTo(pcm);
            _offset += count;
            return count;
        }

        public void Dispose() => reader.Dispose();
    }

    private sealed class VideoStream(Reader reader, int width, int height, TimeSpan start) : IMediaVideoStream
    {
        public MediaFrame? Next()
        {
            while (reader.Next(out var time) is { } bytes)
            {
                if (time < start || bytes.Length < width * height * 4) continue;
                var bgra = bytes.Length == width * height * 4 ? bytes : bytes[..(width * height * 4)];
                for (var alpha = 3; alpha < bgra.Length; alpha += 4) bgra[alpha] = 255;
                return new MediaFrame(width, height, bgra, time);
            }

            return null;
        }

        public void Dispose() => reader.Dispose();
    }
}
