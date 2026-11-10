using System.Diagnostics;
using System.Globalization;
using PulseOffice.Platform;

namespace PulseOffice.Platform.Linux;

[System.Runtime.Versioning.SupportedOSPlatform("linux")]
public sealed class AppImageUpdateInstaller(IAppImageLocation location, int processId) : IUpdateInstaller
{
    private const string SwapScript = """
        #!/bin/sh
        while kill -0 "$1" 2>/dev/null; do sleep 1; done
        current="$2"
        staged="$3"
        if mv -f "$staged" "$current"; then
          chmod +x "$current"
        fi
        nohup "$current" >/dev/null 2>&1 &
        rm -f "$0"
        """;

    public AppImageUpdateInstaller(IAppImageLocation location)
        : this(location, Environment.ProcessId)
    {
    }

    private string? CurrentImage => location.Current;

    public bool CanInstall => CurrentImage is { } image && Writable(Path.GetDirectoryName(image)!);

    public Task<StagedUpdate> StageAsync(string packagePath, CancellationToken ct)
    {
        var current = CurrentImage ?? throw new InvalidOperationException("PulseOffice is not running from an AppImage.");
        var staged = Path.Combine(Path.GetDirectoryName(current)!, $".{Path.GetFileName(current)}.update");
        File.Delete(staged);
        using (var source = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var target = new FileStream(staged, new FileStreamOptions
               {
                   Mode = FileMode.CreateNew,
                   Access = FileAccess.Write,
                   Share = FileShare.None,
                   UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                                    | UnixFileMode.OtherRead | UnixFileMode.OtherExecute,
               }))
            source.CopyTo(target);
        return Task.FromResult(new StagedUpdate(packagePath, staged));
    }

    public void InstallAfterExit(StagedUpdate staged)
    {
        ArgumentNullException.ThrowIfNull(staged);
        var current = CurrentImage ?? throw new InvalidOperationException("PulseOffice is not running from an AppImage.");
        var script = Path.Combine(Path.GetTempPath(), $"pulseoffice-update-{Guid.NewGuid():N}.sh");
        File.WriteAllText(script, SwapScript.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");

        var start = new ProcessStartInfo("/bin/sh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { script, processId.ToString(CultureInfo.InvariantCulture), current, staged.StagedPath })
            start.ArgumentList.Add(argument);
        using var _ = Process.Start(start) ?? throw new InvalidOperationException("The update helper could not start.");
    }

    private static bool Writable(string directory)
    {
        try
        {
            var probe = Path.Combine(directory, $".pulseoffice-write-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
