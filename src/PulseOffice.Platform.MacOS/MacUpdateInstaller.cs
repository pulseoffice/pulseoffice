using System.Diagnostics;
using System.Text.RegularExpressions;
using PulseOffice.Platform;

namespace PulseOffice.Platform.MacOS;

public sealed partial class MacUpdateInstaller(IExternalProcess processes, string executableDirectory, int processId) : IUpdateInstaller
{
    private const string BundleSuffix = ".app";

    private const string SwapScript = """
        #!/bin/sh
        while kill -0 "$1" 2>/dev/null; do sleep 1; done
        current="$2"
        staged="$3"
        previous="$current.previous"
        rm -rf "$previous"
        if mv "$current" "$previous"; then
          if mv "$staged" "$current"; then
            rm -rf "$previous"
          else
            mv "$previous" "$current"
          fi
        fi
        open "$current"
        rm -f "$0"
        """;

    public MacUpdateInstaller(IExternalProcess processes)
        : this(processes, AppContext.BaseDirectory, Environment.ProcessId)
    {
    }

    private string? CurrentBundle
    {
        get
        {
            var macOs = new DirectoryInfo(executableDirectory.TrimEnd(Path.DirectorySeparatorChar));
            if (macOs is not { Name: "MacOS", Parent: { Name: "Contents", Parent: { } bundle } }) return null;
            return bundle.Name.EndsWith(BundleSuffix, StringComparison.OrdinalIgnoreCase) ? bundle.FullName : null;
        }
    }

    public bool CanInstall => CurrentBundle is { } bundle && Writable(Path.GetDirectoryName(bundle)!);

    public async Task<StagedUpdate> StageAsync(string packagePath, CancellationToken ct)
    {
        var current = CurrentBundle ?? throw new InvalidOperationException("PulseOffice is not running from an application bundle.");
        var team = await TeamAsync(current, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The running application is not signed with a Developer ID.");

        var mount = Directory.CreateTempSubdirectory("pulseoffice-update-").FullName;
        await RequireAsync("/usr/bin/hdiutil", ["attach", "-nobrowse", "-readonly", "-noautoopen", "-mountpoint", mount, packagePath], ct).ConfigureAwait(false);
        try
        {
            var candidate = Directory.EnumerateDirectories(mount, "*" + BundleSuffix).SingleOrDefault()
                ?? throw new InvalidOperationException("The disk image holds no single application.");
            await RequireAsync("/usr/bin/codesign", ["--verify", "--deep", "--strict", candidate], ct).ConfigureAwait(false);
            if (await TeamAsync(candidate, ct).ConfigureAwait(false) != team)
                throw new InvalidOperationException("The new application is signed by a different developer.");

            var staging = Path.Combine(Path.GetDirectoryName(packagePath)!, "staged");
            var staged = Path.Combine(staging, Path.GetFileName(current));
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            Directory.CreateDirectory(staging);
            await RequireAsync("/usr/bin/ditto", [candidate, staged], ct).ConfigureAwait(false);
            await RequireAsync("/usr/bin/codesign", ["--verify", "--deep", "--strict", staged], ct).ConfigureAwait(false);
            return new StagedUpdate(packagePath, staged);
        }
        finally
        {
            await processes.RunAsync("/usr/bin/hdiutil", ["detach", mount, "-force"], ct: CancellationToken.None).ConfigureAwait(false);
            try
            {
                Directory.Delete(mount);
            }
            catch (IOException)
            {
            }
        }
    }

    public void InstallAfterExit(StagedUpdate staged)
    {
        ArgumentNullException.ThrowIfNull(staged);
        var current = CurrentBundle ?? throw new InvalidOperationException("PulseOffice is not running from an application bundle.");
        var script = Path.Combine(Path.GetTempPath(), $"pulseoffice-update-{Guid.NewGuid():N}.sh");
        File.WriteAllText(script, SwapScript.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");

        var start = new ProcessStartInfo("/usr/bin/nohup") { UseShellExecute = false };
        foreach (var argument in new[] { "/bin/sh", script, processId.ToString(System.Globalization.CultureInfo.InvariantCulture), current, staged.StagedPath })
            start.ArgumentList.Add(argument);
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        using var _ = Process.Start(start) ?? throw new InvalidOperationException("The update helper could not start.");
    }

    private async Task<string?> TeamAsync(string bundle, CancellationToken ct)
    {
        var outcome = await processes.RunAsync("/usr/bin/codesign", ["-dv", "--verbose=2", bundle], ct: ct).ConfigureAwait(false);
        if (outcome is not { Succeeded: true }) return null;
        var match = TeamPattern().Match(outcome.Error + outcome.Output);
        return match.Success && match.Groups[1].Value != "not set" ? match.Groups[1].Value : null;
    }

    private async Task RequireAsync(string executable, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var outcome = await processes.RunAsync(executable, arguments, ct: ct).ConfigureAwait(false);
        if (outcome is not { Succeeded: true })
            throw new InvalidOperationException(outcome?.Explanation(Path.GetFileName(executable)) ?? $"{executable} could not start.");
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

    [GeneratedRegex(@"TeamIdentifier=(\S+)")]
    private static partial Regex TeamPattern();
}
