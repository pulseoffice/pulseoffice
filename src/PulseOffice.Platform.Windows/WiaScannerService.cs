using System.Diagnostics;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Platform.Windows;

public sealed class WiaScannerService : IScannerService
{
    private const string ListScript =
        """
        $manager = New-Object -ComObject WIA.DeviceManager
        foreach ($info in $manager.DeviceInfos) {
          if ($info.Type -eq 1) {
            $name = $info.Properties.Item('Name').Value
            Write-Output ("{0}|{1}" -f $info.DeviceID, $name)
          }
        }
        """;

    public string DisplayName => "WIA";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public async Task<OperationResult<IReadOnlyList<ScannerDevice>>> DevicesAsync(
        CancellationToken ct = default)
    {
        var listing = await RunAsync(ListScript, ct).ConfigureAwait(false);

        if (!listing.Succeeded)
            return OperationResult<IReadOnlyList<ScannerDevice>>.Fail(listing.Diagnostics[0]);

        var devices = listing.Value!
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('|', 2))
            .Where(parts => parts.Length == 2)
            .Select(parts => new ScannerDevice(parts[0], parts[1]))
            .ToList();

        return OperationResult<IReadOnlyList<ScannerDevice>>.Ok(devices);
    }

    public async Task<OperationResult<string>> ScanAsync(
        ScanRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var script = ScanScript(request);

        var scan = await RunAsync(script, ct).ConfigureAwait(false);

        if (!scan.Succeeded) return OperationResult<string>.Fail(scan.Diagnostics[0]);

        return File.Exists(request.TargetPath)
            ? OperationResult<string>.Ok(request.TargetPath)
            : OperationResult<string>.Fail(Diagnostic.Error("SCAN003", "The scanner produced no image."));
    }

    private static string ScanScript(ScanRequest request) =>
        $$"""
          $manager = New-Object -ComObject WIA.DeviceManager
          $chosen = $null
          foreach ($info in $manager.DeviceInfos) {
            if ($info.Type -eq 1) {
              if ([string]::IsNullOrEmpty({{Quoted(request.DeviceId)}}) -or $info.DeviceID -eq {{Quoted(request.DeviceId)}}) {
                $chosen = $info
                break
              }
            }
          }
          if ($chosen -eq $null) { throw 'no scanner' }
          $device = $chosen.Connect()
          $item = $device.Items.Item(1)
          $item.Properties.Item('6147').Value = {{request.Resolution}}
          $item.Properties.Item('6148').Value = {{request.Resolution}}
          $item.Properties.Item('6146').Value = {{(request.Colour ? 1 : 2)}}
          $image = $item.Transfer('{B96B3CAF-0728-11D3-9D7B-0000F81EF32E}')
          $image.SaveFile({{Quoted(request.TargetPath)}})
          """;

    private static string Quoted(string? value) =>
        "'" + (value ?? string.Empty).Replace("'", "''", StringComparison.Ordinal) + "'";

    private static async Task<OperationResult<string>> RunAsync(string script, CancellationToken ct)
    {
        try
        {
            var outcome = await new ExternalProcess().RunAsync(
                "powershell", ["-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script))], ct: ct).ConfigureAwait(false);
            if (outcome is null)
                return OperationResult<string>.Fail(Diagnostic.Error("SCAN002", "powershell").With("powershell"));

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
