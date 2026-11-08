using System.Diagnostics;
using System.Globalization;
using PulseOffice.Platform;

namespace PulseOffice.Platform.Windows;

public sealed class WindowsUpdateInstaller(string executableDirectory, string executableName, int processId) : IUpdateInstaller
{
    private const string Uninstaller = "Uninstall.exe";

    private const string Script = """
        param([int]$ProcessId, [string]$Installer, [string]$Application)
        Wait-Process -Id $ProcessId -ErrorAction SilentlyContinue
        $run = Start-Process -FilePath $Installer -ArgumentList '/S' -Verb RunAs -Wait -PassThru -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $Application) { Start-Process -FilePath $Application }
        Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue
        """;

    public WindowsUpdateInstaller()
        : this(AppContext.BaseDirectory, Path.GetFileName(Environment.ProcessPath ?? "PulseOffice.exe"), Environment.ProcessId)
    {
    }

    public bool CanInstall => File.Exists(Path.Combine(executableDirectory, Uninstaller));

    public Task<StagedUpdate> StageAsync(string packagePath, CancellationToken ct) =>
        Task.FromResult(new StagedUpdate(packagePath, packagePath));

    public void InstallAfterExit(StagedUpdate staged)
    {
        ArgumentNullException.ThrowIfNull(staged);
        var script = Path.Combine(Path.GetTempPath(), $"pulseoffice-update-{Guid.NewGuid():N}.ps1");
        File.WriteAllText(script, Script);

        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in new[]
                 {
                     "-NoProfile", "-ExecutionPolicy", "Bypass", "-WindowStyle", "Hidden", "-File", script,
                     "-ProcessId", processId.ToString(CultureInfo.InvariantCulture),
                     "-Installer", staged.StagedPath,
                     "-Application", Path.Combine(executableDirectory, executableName),
                 })
            start.ArgumentList.Add(argument);
        using var _ = Process.Start(start) ?? throw new InvalidOperationException("The update helper could not start.");
    }
}
