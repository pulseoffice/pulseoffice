using System.Runtime.InteropServices;
using PulseOffice.Foundation.Diagnostics;
using Vortice.MediaFoundation;

namespace PulseOffice.Platform.Windows;

public sealed class MediaFoundationVideoEncoder : IVideoEncoder
{
    private static readonly Guid MajorTypeVideo = new("73646976-0000-0010-8000-00AA00389B71");

    private readonly VideoFrameFitter _fitter = new();

    public bool IsAvailable => OperatingSystem.IsWindows();

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

        return Task.Run(() => Guarded(fitted, Math.Max(1, fps), outputPath, ct), ct);
    }

    private async Task<OperationResult<string>> Guarded(IVideoFrameSource fitted, int fps, string outputPath, CancellationToken ct)
    {
        try
        {
            return await Encode(fitted, fps, outputPath, ct).ConfigureAwait(false);
        }
        catch (VideoFrameException ex)
        {
            return OperationResult<string>.Fail(Diagnostic.Error(ex.Code, ex.Message).With(ex.Message));
        }
        catch (Exception ex) when (ex is DllNotFoundException or SharpGen.Runtime.SharpGenException or COMException
            or IOException or UnauthorizedAccessException)
        {
            return OperationResult<string>.Fail(Diagnostic.Error("VIDEO001", ex.Message).With(ex.Message));
        }
    }

    private async Task<OperationResult<string>> Encode(
        IVideoFrameSource frames, int fps, string outputPath, CancellationToken ct)
    {
        var width = frames.Width;
        var height = frames.Height;

        MediaFactory.MFStartup(false);

        try
        {
            using var sink = MediaFactory.MFCreateSinkWriterFromURL(outputPath, null, null);

            using var outType = MediaFactory.MFCreateMediaType();
            outType.Set(MediaTypeAttributeKeys.MajorType, MajorTypeVideo);
            outType.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.H264);
            outType.Set(MediaTypeAttributeKeys.AvgBitrate, (uint)Math.Clamp((long)width * height * fps / 12, 1_000_000, 20_000_000));
            outType.Set(MediaTypeAttributeKeys.InterlaceMode, 2u);
            MediaFactory.MFSetAttributeSize(outType, MediaTypeAttributeKeys.FrameSize, (uint)width, (uint)height);
            MediaFactory.MFSetAttributeRatio(outType, MediaTypeAttributeKeys.FrameRate, (uint)fps, 1);
            MediaFactory.MFSetAttributeRatio(outType, MediaTypeAttributeKeys.PixelAspectRatio, 1, 1);

            var streamIndex = sink.AddStream(outType);

            using var inType = MediaFactory.MFCreateMediaType();
            inType.Set(MediaTypeAttributeKeys.MajorType, MajorTypeVideo);
            inType.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.Rgb32);
            inType.Set(MediaTypeAttributeKeys.InterlaceMode, 2u);
            inType.Set(MediaTypeAttributeKeys.DefaultStride, (uint)(width * 4));
            MediaFactory.MFSetAttributeSize(inType, MediaTypeAttributeKeys.FrameSize, (uint)width, (uint)height);
            MediaFactory.MFSetAttributeRatio(inType, MediaTypeAttributeKeys.FrameRate, (uint)fps, 1);
            MediaFactory.MFSetAttributeRatio(inType, MediaTypeAttributeKeys.PixelAspectRatio, 1, 1);

            sink.SetInputMediaType(streamIndex, inType, null);
            sink.BeginWriting();

            var frameBytes = width * height * 4;
            long sampleTime = 0;

            for (var index = 0; index < frames.Count; index++)
            {
                ct.ThrowIfCancellationRequested();
                var frame = await frames.RenderAsync(index, ct).ConfigureAwait(false);
                var durationTicks = Math.Max(1, (long)frame.DurationMs * 10_000);

                using var buffer = MediaFactory.MFCreateMemoryBuffer(frameBytes);
                buffer.Lock(out var address, out _, out _);
                Marshal.Copy(frame.Bgra, 0, address, frameBytes);
                buffer.Unlock();
                buffer.CurrentLength = frameBytes;

                using var sample = MediaFactory.MFCreateSample();
                sample.AddBuffer(buffer);
                sample.SampleTime = sampleTime;
                sample.SampleDuration = durationTicks;

                sink.WriteSample(streamIndex, sample);
                sampleTime += durationTicks;
            }

            sink.Finalize();
        }
        finally
        {
            MediaFactory.MFShutdown();
        }

        return File.Exists(outputPath)
            ? OperationResult<string>.Ok(outputPath)
            : OperationResult<string>.Fail(Diagnostic.Error("VIDEO002", "Media Foundation produced no file.").With("Media Foundation produced no file."));
    }
}
