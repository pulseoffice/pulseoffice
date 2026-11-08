namespace PulseOffice.Platform;

public sealed record StagedUpdate(string PackagePath, string StagedPath);

public interface IUpdateInstaller
{
    bool CanInstall { get; }

    Task<StagedUpdate> StageAsync(string packagePath, CancellationToken ct);

    void InstallAfterExit(StagedUpdate staged);
}

public sealed class ManualUpdateInstaller : IUpdateInstaller
{
    public bool CanInstall => false;

    public Task<StagedUpdate> StageAsync(string packagePath, CancellationToken ct) =>
        throw new NotSupportedException("Updates are installed by hand on this system.");

    public void InstallAfterExit(StagedUpdate staged) =>
        throw new NotSupportedException("Updates are installed by hand on this system.");
}
