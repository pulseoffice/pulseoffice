using System.Diagnostics;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Platform;

public sealed record ScannerDevice(string Id, string DisplayName);

public sealed record ScanRequest
{
    public string? DeviceId { get; init; }

    public int Resolution { get; init; } = 300;

    public bool Colour { get; init; } = true;

    public required string TargetPath { get; init; }
}

public interface IScannerService
{
    string DisplayName { get; }

    bool IsAvailable { get; }

    Task<OperationResult<IReadOnlyList<ScannerDevice>>> DevicesAsync(CancellationToken ct = default);

    Task<OperationResult<string>> ScanAsync(ScanRequest request, CancellationToken ct = default);
}

public interface IScannerServiceProvider
{
    IScannerService Current { get; }
}

public sealed class SaneScannerService(IExecutableLocator executables, IExternalProcess processes) : IScannerService
{
    public SaneScannerService(IExecutableLocator executables)
        : this(executables, new ExternalProcess())
    {
    }

    public string DisplayName => "SANE";

    public bool IsAvailable => executables.Exists("scanimage");

    public async Task<OperationResult<IReadOnlyList<ScannerDevice>>> DevicesAsync(
        CancellationToken ct = default)
    {
        var listing = await RunAsync("scanimage", ["--formatted-device-list", "%d|%v %m%n"], ct)
            .ConfigureAwait(false);

        if (!listing.Succeeded)
            return OperationResult<IReadOnlyList<ScannerDevice>>.Fail(listing.Diagnostics[0]);

        var devices = listing.Value!
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('|', 2))
            .Where(parts => parts.Length == 2 && parts[0].Length > 0)
            .Select(parts => new ScannerDevice(parts[0], parts[1]))
            .ToList();

        return OperationResult<IReadOnlyList<ScannerDevice>>.Ok(devices);
    }

    public async Task<OperationResult<string>> ScanAsync(
        ScanRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var arguments = new List<string> { "--format=png" };

        if (!string.IsNullOrEmpty(request.DeviceId)) arguments.AddRange(["--device-name", request.DeviceId]);

        arguments.AddRange(["--resolution", request.Resolution.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        arguments.AddRange(["--mode", request.Colour ? "Color" : "Gray"]);
        arguments.AddRange(["--output-file", request.TargetPath]);

        var scan = await RunAsync("scanimage", arguments, ct).ConfigureAwait(false);

        if (!scan.Succeeded) return OperationResult<string>.Fail(scan.Diagnostics[0]);

        return File.Exists(request.TargetPath)
            ? OperationResult<string>.Ok(request.TargetPath)
            : OperationResult<string>.Fail(Diagnostic.Error("SCAN003", "The scanner produced no image."));
    }

    private async Task<OperationResult<string>> RunAsync(
        string executable, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        try
        {
            var outcome = await processes.RunAsync(
                executable, arguments, new Dictionary<string, string> { ["LC_ALL"] = "C" }, ct: ct).ConfigureAwait(false);
            if (outcome is null) return OperationResult<string>.Fail(Diagnostic.Error("SCAN002", executable).With(executable));

            return outcome.Succeeded
                ? OperationResult<string>.Ok(outcome.Output)
                : OperationResult<string>.Fail(Diagnostic.Error("SCAN001", outcome.Error.Trim()).With(outcome.Error.Trim()));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            return OperationResult<string>.Fail(Diagnostic.Error("SCAN002", ex.Message).With(ex.Message));
        }
    }
}

public sealed class UnavailableScannerService(string hint) : IScannerService
{
    public string DisplayName => "—";

    public bool IsAvailable => false;

    public Task<OperationResult<IReadOnlyList<ScannerDevice>>> DevicesAsync(
        CancellationToken ct = default) =>
        Task.FromResult(OperationResult<IReadOnlyList<ScannerDevice>>.Ok([]));

    public Task<OperationResult<string>> ScanAsync(ScanRequest request, CancellationToken ct = default) =>
        Task.FromResult(OperationResult<string>.Fail(Diagnostic.Error("SCAN010", hint).With(hint)));
}
