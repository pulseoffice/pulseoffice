using System.Runtime.InteropServices;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Platform.Mac;

public sealed class VideoToolboxVideoEncoder : IVideoEncoder
{
    private const int Timescale = 600;
    private const uint PixelFormat32Bgra = 0x42475241;
    private const int CodecH264 = 0x61766331;

    private readonly VideoFrameFitter _fitter = new();
    private readonly SampleTiming _timing = new();

    public bool IsAvailable => OperatingSystem.IsMacOS();

    public string? UnavailableHint => null;

    public Task<OperationResult<string>> EncodeAsync(
        IVideoFrameSource frames, int fps, string outputPath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(outputPath);

        if (frames.Count == 0)
            return Task.FromResult(OperationResult<string>.Fail(Diagnostic.Error("VIDEO003", "No frames to encode.")));

        if (_fitter.Fit(frames) is not { } fitted)
            return Task.FromResult(OperationResult<string>.Fail(Diagnostic.Error("VIDEO004", "Frames do not match the declared size.")));

        return Task.Run(() => Guarded(fitted, outputPath, ct), ct);
    }

    private async Task<OperationResult<string>> Guarded(IVideoFrameSource fitted, string outputPath, CancellationToken ct)
    {
        try
        {
            return await Encode(fitted, outputPath, ct).ConfigureAwait(false);
        }
        catch (VideoFrameException ex)
        {
            return Failure(ex.Code, ex.Message);
        }
        catch (Mp4TooLargeException ex)
        {
            return Failure("VIDEO005", ex.Message);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return Failure("VIDEO001", ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Failure("VIDEO002", ex.Message);
        }
    }

    private OperationResult<string> Failure(string code, string message) =>
        OperationResult<string>.Fail(Diagnostic.Error(code, message).With(message));

    private async Task<OperationResult<string>> Encode(IVideoFrameSource frames, string outputPath, CancellationToken ct)
    {
        var width = frames.Width;
        var height = frames.Height;
        var collected = new EncodedStream();

        VtOutputCallback callback = (_, sourceFrameRefCon, status, _, sampleBuffer) =>
        {
            if (status != 0 || sampleBuffer == IntPtr.Zero) return;
            collected.Collect((long)sourceFrameRefCon, sampleBuffer);
        };

        var handle = GCHandle.Alloc(callback);
        var durations = new List<long>(frames.Count);
        long presentation = 0;

        try
        {
            var create = VTCompressionSessionCreate(
                IntPtr.Zero, width, height, CodecH264, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                callback, IntPtr.Zero, out var session);

            if (create != 0 || session == IntPtr.Zero)
                return Failure("VIDEO001", $"VTCompressionSessionCreate failed ({create}).");

            try
            {
                ConfigureSession(session);

                for (var i = 0; i < frames.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var frame = await frames.RenderAsync(i, ct).ConfigureAwait(false);
                    var ticks = (long)Math.Max(1, Math.Round(frame.DurationMs / 1000.0 * Timescale));
                    durations.Add(ticks);

                    var pixel = CopyToPixelBuffer(frame.Bgra, width, height);
                    if (pixel == IntPtr.Zero)
                        return Failure("VIDEO001", "CVPixelBufferCreate failed.");

                    var pts = new CMTime(presentation, Timescale);
                    var duration = new CMTime(ticks, Timescale);
                    presentation += ticks;

                    var encode = VTCompressionSessionEncodeFrame(
                        session, pixel, pts, duration, IntPtr.Zero, (IntPtr)(long)i, out _);

                    CFRelease(pixel);

                    if (encode != 0)
                        return Failure("VIDEO001", $"VTCompressionSessionEncodeFrame failed ({encode}).");
                }

                VTCompressionSessionCompleteFrames(session, new CMTime(long.MaxValue, Timescale));
            }
            finally
            {
                VTCompressionSessionInvalidate(session);
                CFRelease(session);
            }
        }
        finally
        {
            handle.Free();
        }

        if (collected.Sps is null || collected.Pps is null || collected.Samples.Count == 0)
            return Failure("VIDEO002", "VideoToolbox produced no encoded frames.");

        if (_timing.Durations(collected.Samples.Keys.ToList(), durations) is not { } sampleDurations)
            return Failure("VIDEO002", "VideoToolbox dropped the first frame.");

        var samples = collected.Samples.Values.ToList();

        var mp4 = new Mp4Writer(width, height, Timescale, collected.Sps, collected.Pps);
        var bytes = mp4.Build(samples, sampleDurations);

        await File.WriteAllBytesAsync(outputPath, bytes, ct).ConfigureAwait(false);

        return File.Exists(outputPath)
            ? OperationResult<string>.Ok(outputPath)
            : Failure("VIDEO002", "The mp4 file was not written.");
    }

    private void ConfigureSession(IntPtr session)
    {
        var vt = NativeConstants.VideoToolbox;
        var cf = NativeConstants.CoreFoundation;

        if (vt.Constant("kVTCompressionPropertyKey_RealTime") is { } realTime)
            VTSessionSetProperty(session, realTime, cf.Constant("kCFBooleanFalse") ?? IntPtr.Zero);

        if (vt.Constant("kVTCompressionPropertyKey_AllowFrameReordering") is { } reorder)
            VTSessionSetProperty(session, reorder, cf.Constant("kCFBooleanFalse") ?? IntPtr.Zero);

        if (vt.Constant("kVTCompressionPropertyKey_MaxKeyFrameInterval") is { } maxKey)
        {
            var one = 1;
            var number = CFNumberCreate(IntPtr.Zero, 9, ref one);
            VTSessionSetProperty(session, maxKey, number);
            CFRelease(number);
        }
    }

    private IntPtr CopyToPixelBuffer(byte[] bgra, int width, int height)
    {
        if (CVPixelBufferCreate(IntPtr.Zero, width, height, PixelFormat32Bgra, IntPtr.Zero, out var pixel) != 0 || pixel == IntPtr.Zero)
            return IntPtr.Zero;

        if (CVPixelBufferLockBaseAddress(pixel, 0) != 0)
        {
            CFRelease(pixel);
            return IntPtr.Zero;
        }

        try
        {
            var target = CVPixelBufferGetBaseAddress(pixel);
            var stride = (long)CVPixelBufferGetBytesPerRow(pixel);
            var row = width * 4;

            for (var y = 0; y < height; y++)
                Marshal.Copy(bgra, y * row, (IntPtr)((long)target + y * stride), row);
        }
        finally
        {
            CVPixelBufferUnlockBaseAddress(pixel, 0);
        }

        return pixel;
    }

    private sealed class EncodedStream
    {
        public byte[]? Sps { get; private set; }

        public byte[]? Pps { get; private set; }

        public SortedDictionary<long, byte[]> Samples { get; } = [];

        public void Collect(long index, IntPtr sampleBuffer)
        {
            if (Sps is null && CMSampleBufferGetFormatDescription(sampleBuffer) is var format && format != IntPtr.Zero)
            {
                Sps = ParameterSet(format, 0);
                Pps = ParameterSet(format, 1);
            }

            var block = CMSampleBufferGetDataBuffer(sampleBuffer);
            if (block == IntPtr.Zero) return;

            var length = (int)CMBlockBufferGetDataLength(block);
            if (length <= 0) return;

            var data = new byte[length];
            var pin = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                if (CMBlockBufferCopyDataBytes(block, 0, (nuint)length, pin.AddrOfPinnedObject()) == 0)
                    Samples[index] = data;
            }
            finally
            {
                pin.Free();
            }
        }

        private byte[]? ParameterSet(IntPtr format, nuint index)
        {
            var status = CMVideoFormatDescriptionGetH264ParameterSetAtIndex(
                format, index, out var pointer, out var size, out _, out _);

            if (status != 0 || pointer == IntPtr.Zero || size == 0) return null;

            var buffer = new byte[(int)size];
            Marshal.Copy(pointer, buffer, 0, buffer.Length);
            return buffer;
        }
    }

    private delegate void VtOutputCallback(
        IntPtr outputCallbackRefCon, IntPtr sourceFrameRefCon, int status, uint infoFlags, IntPtr sampleBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CMTime(long value, int timescale)
    {
        public readonly long Value = value;
        public readonly int Timescale = timescale;
        public readonly uint Flags = 1;
        public readonly long Epoch = 0;
    }


    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string CoreVideo = "/System/Library/Frameworks/CoreVideo.framework/CoreVideo";
    private const string CoreMedia = "/System/Library/Frameworks/CoreMedia.framework/CoreMedia";
    private const string VideoToolboxLib = "/System/Library/Frameworks/VideoToolbox.framework/VideoToolbox";

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(IntPtr cf);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFNumberCreate(IntPtr allocator, int type, ref int value);

    [DllImport(CoreVideo)]
    private static extern int CVPixelBufferCreate(
        IntPtr allocator, nint width, nint height, uint pixelFormatType, IntPtr pixelBufferAttributes,
        out IntPtr pixelBufferOut);

    [DllImport(CoreVideo)]
    private static extern int CVPixelBufferLockBaseAddress(IntPtr pixelBuffer, ulong lockFlags);

    [DllImport(CoreVideo)]
    private static extern int CVPixelBufferUnlockBaseAddress(IntPtr pixelBuffer, ulong unlockFlags);

    [DllImport(CoreVideo)]
    private static extern IntPtr CVPixelBufferGetBaseAddress(IntPtr pixelBuffer);

    [DllImport(CoreVideo)]
    private static extern nuint CVPixelBufferGetBytesPerRow(IntPtr pixelBuffer);

    [DllImport(CoreMedia)]
    private static extern IntPtr CMSampleBufferGetDataBuffer(IntPtr sbuf);

    [DllImport(CoreMedia)]
    private static extern IntPtr CMSampleBufferGetFormatDescription(IntPtr sbuf);

    [DllImport(CoreMedia)]
    private static extern nuint CMBlockBufferGetDataLength(IntPtr theBuffer);

    [DllImport(CoreMedia)]
    private static extern int CMBlockBufferCopyDataBytes(
        IntPtr theSourceBuffer, nuint offsetToData, nuint dataLength, IntPtr destination);

    [DllImport(CoreMedia)]
    private static extern int CMVideoFormatDescriptionGetH264ParameterSetAtIndex(
        IntPtr videoDesc, nuint parameterSetIndex, out IntPtr parameterSetPointerOut,
        out nuint parameterSetSizeOut, out nuint parameterSetCountOut, out int nalUnitHeaderLengthOut);

    [DllImport(VideoToolboxLib)]
    private static extern int VTCompressionSessionCreate(
        IntPtr allocator, int width, int height, int codecType, IntPtr encoderSpecification,
        IntPtr sourceImageBufferAttributes, IntPtr compressedDataAllocator,
        VtOutputCallback outputCallback, IntPtr outputCallbackRefCon, out IntPtr compressionSessionOut);

    [DllImport(VideoToolboxLib)]
    private static extern int VTSessionSetProperty(IntPtr session, IntPtr propertyKey, IntPtr propertyValue);

    [DllImport(VideoToolboxLib)]
    private static extern int VTCompressionSessionEncodeFrame(
        IntPtr session, IntPtr imageBuffer, CMTime presentationTimeStamp, CMTime duration,
        IntPtr frameProperties, IntPtr sourceFrameRefCon, out uint infoFlagsOut);

    [DllImport(VideoToolboxLib)]
    private static extern int VTCompressionSessionCompleteFrames(
        IntPtr session, CMTime completeUntilPresentationTimeStamp);

    [DllImport(VideoToolboxLib)]
    private static extern void VTCompressionSessionInvalidate(IntPtr session);
}
