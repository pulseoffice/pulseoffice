using System.Diagnostics;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Platform;

public sealed record Printer(string Id, string DisplayName, bool IsDefault);

public enum PrintSides
{
    OneSided,
    LongEdge,
    ShortEdge,
}

public sealed record PrintCapabilities(bool ChoosePrinter, bool Duplex, bool NativeCopies, bool NativeCollate, bool Verified);

public sealed record PrintRequest
{
    public required string FilePath { get; init; }

    public string? PrinterId { get; init; }

    public int Copies { get; init; } = 1;

    public string? PageRange { get; init; }

    public PrintSides Sides { get; init; } = PrintSides.OneSided;

    public bool Collate { get; init; } = true;

    public string JobName { get; init; } = "PulseOffice";
}

public interface IPrintService
{
    PrintCapabilities Capabilities { get; }

    Task<IReadOnlyList<Printer>> PrintersAsync(CancellationToken ct = default);

    Task<OperationResult<bool>> PrintAsync(PrintRequest request, CancellationToken ct = default);
}

public interface IPrintServiceProvider
{
    IPrintService Current { get; }
}

public sealed class PrintServiceProvider(IHostSystem host, IExternalProcess processes) : IPrintServiceProvider
{
    private IPrintService? _current;

    public IPrintService Current => _current ??= host.IsWindows
        ? new WindowsPrintService(processes)
        : new CupsPrintService(processes);
}

public sealed class CupsPrintService(IExternalProcess processes) : IPrintService
{
    public PrintCapabilities Capabilities { get; } = new(ChoosePrinter: true, Duplex: true, NativeCopies: true, NativeCollate: true, Verified: true);

    public async Task<IReadOnlyList<Printer>> PrintersAsync(CancellationToken ct = default)
    {
        var listing = await RunAsync("lpstat", ["-e"], ct).ConfigureAwait(false);
        if (!listing.Succeeded) return [];

        var names = listing.Value!
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(name => name.Length > 0 && !name.Contains(' '))
            .ToList();

        var fallback = await DefaultPrinterAsync(ct).ConfigureAwait(false);

        return [.. names.Select(name => new Printer(name, DisplayNameOf(name), name == fallback))];
    }

    private async Task<string?> DefaultPrinterAsync(CancellationToken ct)
    {
        var result = await RunAsync("lpstat", ["-d"], ct).ConfigureAwait(false);
        if (!result.Succeeded) return null;

        var line = result.Value!.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var separator = line?.LastIndexOf(':') ?? -1;

        return separator < 0 ? null : line![(separator + 1)..].Trim();
    }

    private static string DisplayNameOf(string id) =>
        string.Join(' ', id.Replace('_', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public async Task<OperationResult<bool>> PrintAsync(PrintRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var arguments = new List<string>();

        if (!string.IsNullOrEmpty(request.PrinterId)) arguments.AddRange(["-d", request.PrinterId]);
        if (request.Copies > 1) arguments.AddRange(["-n", request.Copies.ToString()]);
        if (!string.IsNullOrEmpty(request.PageRange)) arguments.AddRange(["-P", request.PageRange]);
        if (request.Copies > 1) arguments.AddRange(["-o", request.Collate ? "collate=true" : "collate=false"]);
        if (request.Sides != PrintSides.OneSided)
            arguments.AddRange(["-o", request.Sides == PrintSides.LongEdge ? "sides=two-sided-long-edge" : "sides=two-sided-short-edge"]);

        arguments.AddRange(["-t", request.JobName, Path.GetFullPath(request.FilePath)]);

        var result = await RunAsync("lp", arguments, ct).ConfigureAwait(false);

        return result.Succeeded
            ? OperationResult<bool>.Ok(true)
            : OperationResult<bool>.Fail(Diagnostic.Error("PRN001", result.Diagnostics[0].Message).With(result.Diagnostics[0].Message));
    }

    private async Task<OperationResult<string>> RunAsync(
        string command, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        try
        {
            var outcome = await processes.RunAsync(
                command, arguments, new Dictionary<string, string> { ["LC_ALL"] = "C", ["LANG"] = "C" }, ct: ct).ConfigureAwait(false);
            if (outcome is null) return OperationResult<string>.Fail(Diagnostic.Error("PRN002", command).With(command));

            return outcome.Succeeded
                ? OperationResult<string>.Ok(outcome.Output)
                : OperationResult<string>.Fail(Diagnostic.Error("PRN003", outcome.Explanation(command)).With(outcome.Explanation(command)));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            return OperationResult<string>.Fail(Diagnostic.Error("PRN004", ex.Message).With(ex.Message));
        }
    }
}

public sealed class WindowsPrintService(IExternalProcess processes) : IPrintService
{
    private const string ListingScript =
        "Get-CimInstance Win32_Printer | ForEach-Object { $_.Name + [char]9 + $_.Default }";

    public PrintCapabilities Capabilities { get; } = new(ChoosePrinter: true, Duplex: false, NativeCopies: false, NativeCollate: false, Verified: false);

    public async Task<IReadOnlyList<Printer>> PrintersAsync(CancellationToken ct = default)
    {
        try
        {
            var outcome = await processes.RunAsync(
                "powershell", ["-NoProfile", "-NonInteractive", "-Command", ListingScript], new Dictionary<string, string>(), ct: ct).ConfigureAwait(false);
            if (outcome is null || !outcome.Succeeded) return [];
            return [.. outcome.Output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => line.Split('\t'))
                .Where(parts => parts.Length == 2 && parts[0].Length > 0)
                .Select(parts => new Printer(parts[0], parts[0], string.Equals(parts[1], "True", StringComparison.OrdinalIgnoreCase)))];
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            return [];
        }
    }

    public Task<OperationResult<bool>> PrintAsync(PrintRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var start = new ProcessStartInfo(request.FilePath) { UseShellExecute = true, Verb = "print" };
            if (!string.IsNullOrEmpty(request.PrinterId))
            {
                start.Verb = "printto";
                start.Arguments = "\"" + request.PrinterId.Replace("\"", string.Empty, StringComparison.Ordinal) + "\"";
            }
            using var process = Process.Start(start);
            return Task.FromResult(OperationResult<bool>.Ok(true));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return Task.FromResult(OperationResult<bool>.Fail(Diagnostic.Error("PRN005", ex.Message).With(ex.Message)));
        }
    }
}
