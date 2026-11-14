using System.Diagnostics;
using System.Globalization;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Platform;

public interface IVideoEncoder
{
    bool IsAvailable { get; }

    string? UnavailableHint { get; }

    Task<OperationResult<string>> EncodeAsync(
        IVideoFrameSource frames, int fps, string outputPath, CancellationToken ct = default);
}

public interface IVideoEncoderProvider
{
    IVideoEncoder Current { get; }
}

public sealed class FfmpegVideoEncoder(IExecutableLocator executables, IFfmpegEncoderChoice encoders) : IVideoEncoder
{
    private readonly VideoFrameFitter _fitter = new();

    public bool IsAvailable => executables.Exists("ffmpeg");

    public string? UnavailableHint => "ffmpeg";

    public async Task<OperationResult<string>> EncodeAsync(
        IVideoFrameSource frames, int fps, string outputPath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(outputPath);

        if (frames.Count == 0)
            return OperationResult<string>.Fail(Diagnostic.Error("VIDEO003", "No frames to encode."));

        if (_fitter.Fit(frames) is not { } fitted)
            return OperationResult<string>.Fail(Diagnostic.Error("VIDEO004", "Frames do not match the declared size."));

        if (await encoders.ChooseAsync(ct).ConfigureAwait(false) is not { } encoder)
            return OperationResult<string>.Fail(Diagnostic.Error("VIDEO007", "ffmpeg offers no H.264 or MPEG-4 encoder.").With("ffmpeg offers no H.264 or MPEG-4 encoder."));

        Process? process = null;

        try
        {
            process = Process.Start(StartInfo(fitted, Math.Max(1, fps), encoder, outputPath));
            if (process is null)
                return OperationResult<string>.Fail(Diagnostic.Error("VIDEO001", "ffmpeg").With("ffmpeg"));

            var error = process.StandardError.ReadToEndAsync(CancellationToken.None);

            var written = await Feed(process, fitted, Math.Max(1, fps), ct).ConfigureAwait(false);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            var message = (await error.ConfigureAwait(false)).Trim();

            if (!written || process.ExitCode != 0)
            {
                Discard(outputPath);
                return OperationResult<string>.Fail(Diagnostic.Error("VIDEO001", message).With(message));
            }

            return File.Exists(outputPath)
                ? OperationResult<string>.Ok(outputPath)
                : OperationResult<string>.Fail(Diagnostic.Error("VIDEO002", "ffmpeg produced no file.").With("ffmpeg produced no file."));
        }
        catch (OperationCanceledException)
        {
            Stop(process);
            Discard(outputPath);
            throw;
        }
        catch (VideoFrameException ex)
        {
            Stop(process);
            Discard(outputPath);
            return OperationResult<string>.Fail(Diagnostic.Error(ex.Code, ex.Message).With(ex.Message));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            Stop(process);
            Discard(outputPath);
            return OperationResult<string>.Fail(Diagnostic.Error("VIDEO001", ex.Message).With(ex.Message));
        }
        finally
        {
            process?.Dispose();
        }
    }

    private ProcessStartInfo StartInfo(IVideoFrameSource fitted, int fps, FfmpegEncoder encoder, string outputPath)
    {
        var info = new ProcessStartInfo("ffmpeg")
        {
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        var size = string.Create(CultureInfo.InvariantCulture, $"{fitted.Width}x{fitted.Height}");

        string[] arguments =
        [
            "-y", "-f", "rawvideo", "-pixel_format", "bgra", "-video_size", size,
            "-framerate", fps.ToString(CultureInfo.InvariantCulture), "-i", "-",
            "-c:v", encoder.Name, .. encoder.Options, "-movflags", "+faststart", outputPath,
        ];
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        return info;
    }

    private async Task<bool> Feed(Process process, IVideoFrameSource fitted, int fps, CancellationToken ct)
    {
        var stdin = process.StandardInput.BaseStream;

        try
        {
            for (var index = 0; index < fitted.Count; index++)
            {
                var frame = await fitted.RenderAsync(index, ct).ConfigureAwait(false);
                var repeats = Math.Max(1, (int)Math.Round(frame.DurationMs / 1000.0 * fps));
                for (var i = 0; i < repeats; i++)
                    await stdin.WriteAsync(frame.Bgra, ct).ConfigureAwait(false);
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        finally
        {
            try
            {
                stdin.Close();
            }
            catch (IOException)
            {
            }
        }
    }

    private void Stop(Process? process)
    {
        try
        {
            if (process is { HasExited: false }) process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
        }
    }

    private void Discard(string outputPath)
    {
        try
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

public sealed class UnavailableVideoEncoder(string hint) : IVideoEncoder
{
    public bool IsAvailable => false;

    public string? UnavailableHint => hint;

    public Task<OperationResult<string>> EncodeAsync(
        IVideoFrameSource frames, int fps, string outputPath, CancellationToken ct = default) =>
        Task.FromResult(OperationResult<string>.Fail(Diagnostic.Error("VIDEO010", hint).With(hint)));
}
