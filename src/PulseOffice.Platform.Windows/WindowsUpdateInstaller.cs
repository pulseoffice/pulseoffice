using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using PulseOffice.Platform;

namespace PulseOffice.Platform.Windows;

public sealed class WindowsUpdateInstaller(string executableDirectory, string executableName, int processId) : IUpdateInstaller
{
    private const string Uninstaller = "Uninstall.exe";

    private const string Script = """
        param([int]$ProcessId, [string]$Installer, [string]$Application, [string]$Sha256)
        Wait-Process -Id $ProcessId -ErrorAction SilentlyContinue
        $actual = (Get-FileHash -LiteralPath $Installer -Algorithm SHA256 -ErrorAction SilentlyContinue).Hash
        if (-not $Sha256 -or $actual -ne $Sha256) {
          if (Test-Path -LiteralPath $Application) { Start-Process -FilePath $Application }
          Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue
          exit 1
        }
        $run = Start-Process -FilePath $Installer -ArgumentList '/S' -Verb RunAs -Wait -PassThru -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $Application) { Start-Process -FilePath $Application }
        Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue
        """;

    public WindowsUpdateInstaller()
        : this(AppContext.BaseDirectory, Path.GetFileName(Environment.ProcessPath ?? "PulseOffice.exe"), Environment.ProcessId)
    {
    }

    public bool CanInstall => File.Exists(Path.Combine(executableDirectory, Uninstaller));

    public async Task<StagedUpdate> StageAsync(string packagePath, CancellationToken ct)
    {
        await using var package = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var hash = await SHA256.HashDataAsync(package, ct).ConfigureAwait(false);
        return new StagedUpdate(packagePath, packagePath, Convert.ToHexString(hash));
    }

    public IReadOnlyList<string> HelperArguments(StagedUpdate staged, string script)
    {
        ArgumentNullException.ThrowIfNull(staged);
        if (staged.Sha256 is not { Length: 64 } sha256) throw new InvalidOperationException("The update was not staged with its checksum.");
        return
        [
            "-NoProfile", "-ExecutionPolicy", "Bypass", "-WindowStyle", "Hidden", "-File", script,
            "-ProcessId", processId.ToString(CultureInfo.InvariantCulture),
            "-Installer", staged.StagedPath,
            "-Application", Path.Combine(executableDirectory, executableName),
            "-Sha256", sha256,
        ];
    }

    public void InstallAfterExit(StagedUpdate staged)
    {
        var script = Path.Combine(Path.GetTempPath(), $"pulseoffice-update-{Guid.NewGuid():N}.ps1");
        var arguments = HelperArguments(staged, script);
        File.WriteAllText(script, Script);

        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var _ = Process.Start(start) ?? throw new InvalidOperationException("The update helper could not start.");
    }
}
