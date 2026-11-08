using PulseOffice.UI.Controls.Notifications;
using System.Globalization;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Settings;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

using PulseOffice.Shell;
using PulseOffice.Sheets.UI;

namespace PulseOffice.Desktop.Composition;

public sealed class ShellScannerCommands(
    IDocumentWorkspace workspace,
    IDocumentOperationsCatalog operations,
    IScannerServiceProvider scanners,
    ISettingsService settings,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts,
    IDiagnosticTexts diagnostics,
    PulseOffice.Platform.ITemporaryFiles temporary)
{
    private const string DeviceSetting = ShellSettingKeys.ScannerDevice;
    private const string ResolutionSetting = ShellSettingKeys.ScannerResolution;
    private const string ColourSetting = ShellSettingKeys.ScannerColour;

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("shell.scan.selectSource", ChooseAsync),
        Command("shell.scan.request", ScanAsync),
    ];

    private CommandDefinition Command(string id, Func<CancellationToken, Task> run) => new()
    {
        Id = new CommandId(id),
        IconKey = id,
        MenuPath = "insert/scanner",
        Surface = CommandSurface.Advanced,
        RequiredState = DocumentState.DocumentOpen,
        ForbiddenState = DocumentState.ReadOnly,
        EnabledWhen = _ => workspace.Active is { } document && operations.For(document) is not null,
        Execute = (_, ct) => run(ct),
    };

    private async Task ChooseAsync(CancellationToken ct)
    {
        var scanner = scanners.Current;

        if (!scanner.IsAvailable)
        {
            status.Report(texts["scanner.unavailable"], NotificationSeverity.Warning);
            return;
        }

        var listing = await scanner.DevicesAsync(ct).ConfigureAwait(true);

        if (!listing.Succeeded)
        {
            status.Report(diagnostics.Describe(listing.Diagnostics[0]), NotificationSeverity.Error);
            return;
        }

        if (listing.Value!.Count == 0)
        {
            status.Report(texts["scanner.none"], NotificationSeverity.Warning);
            return;
        }

        var devices = listing.Value!;

        var answers = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(
                texts.Format("scanner.explanation", scanner.DisplayName),
                [
                    new ChoiceField("device", texts["scanner.device"],
                        [.. devices.Select(device => device.DisplayName)]),
                    new NumberField("resolution", texts["scanner.resolution"], Resolution, 72, 1200),
                    new CheckField("colour", texts["scanner.colour"], Colour),
                ],
                texts),
            new DialogOptions { TitleKey = "command.shell.scan.selectSource.title", PreferredWidth = 460 },
            ct).ConfigureAwait(true);

        if (answers is null) return;

        var chosen = devices[(int)Convert.ToInt64(answers["device"], CultureInfo.InvariantCulture)];

        settings.Set(DeviceSetting, chosen.Id);
        settings.Set(ResolutionSetting, (long)Convert.ToDouble(answers["resolution"], CultureInfo.InvariantCulture));
        settings.Set(ColourSetting, answers["colour"] is true);

        status.Report(texts.Format("scanner.chosen", chosen.DisplayName));
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        var scanner = scanners.Current;

        if (!scanner.IsAvailable)
        {
            status.Report(texts["scanner.unavailable"], NotificationSeverity.Warning);
            return;
        }

        if (workspace.Active is not { } requester) return;

        var target = Path.Combine(temporary.NewFolder("scan-"), "scan.png");
        var activity = status.BeginActivity(texts["scanner.scanning"]);

        try
        {
            var scan = await scanner.ScanAsync(new ScanRequest
            {
                DeviceId = settings.Get<string>(DeviceSetting),
                Resolution = (int)Resolution,
                Colour = Colour,
                TargetPath = target,
            }, ct).ConfigureAwait(true);

            if (!scan.Succeeded)
            {
                status.Report(diagnostics.Describe(scan.Diagnostics[0]), NotificationSeverity.Error);
                return;
            }

            var bytes = await File.ReadAllBytesAsync(target, ct).ConfigureAwait(true);

            var inserted = workspace.Documents.Contains(requester) && !requester.IsReadOnly
                && operations.For(requester)?.InsertImage(requester, bytes, "scan.png") == true;
            status.Report(
                texts[inserted ? "scanner.inserted" : "status.image.unreadable"],
                inserted ? NotificationSeverity.Success : NotificationSeverity.Error);
        }
        finally
        {
            status.EndActivity(activity);

            if (File.Exists(target)) File.Delete(target);
        }
    }

    private long Resolution => settings.Get<long>(ResolutionSetting) is > 0 and var stored ? stored : 300;

    private bool Colour => settings.Get<bool>(ColourSetting);
}
