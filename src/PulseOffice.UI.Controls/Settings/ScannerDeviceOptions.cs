using PulseOffice.Platform;
using PulseOffice.Settings.Navigation;

namespace PulseOffice.UI.Controls.Settings;

public sealed class ScannerDeviceOptions(IScannerServiceProvider scanners, string name) : ISettingOptionSource
{
    private readonly IScannerServiceProvider _scanners = scanners ?? throw new ArgumentNullException(nameof(scanners));

    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public async Task<IReadOnlyList<SettingOption>> LoadAsync(CancellationToken ct = default)
    {
        var scanner = _scanners.Current;
        if (!scanner.IsAvailable) return [];

        var listing = await scanner.DevicesAsync(ct).ConfigureAwait(false);

        return listing.Succeeded && listing.Value is { } devices
            ? [.. devices.Select(device => new SettingOption(
                device.Id,
                string.IsNullOrWhiteSpace(device.DisplayName) ? device.Id : device.DisplayName))]
            : [];
    }
}
