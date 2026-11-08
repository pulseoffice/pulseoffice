namespace PulseOffice.Scripting.CPython;

public sealed record ProvisioningProgress(string Stage, double Fraction, string? Detail = null);

public sealed record PythonDownloadOffer(string Version, string Source, long Bytes, string Destination);

public interface IPythonDownloadConsent
{
    Task<bool> AllowAsync(PythonDownloadOffer offer, CancellationToken ct = default);

    IProgress<ProvisioningProgress> Progress { get; }
}

public interface IPythonRuntimeLocator
{
    PythonRuntimeDescriptor? Locate();
}
