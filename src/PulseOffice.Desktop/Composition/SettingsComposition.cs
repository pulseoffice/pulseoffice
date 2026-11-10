using PulseOffice.Formats.Intake;
using PulseOffice.Documents;
using PulseOffice.Foundation.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PulseOffice.Diagnostics;
using PulseOffice.Platform;
using PulseOffice.Remote;
using PulseOffice.Remote.Cloud;
using PulseOffice.Sheets;
using PulseOffice.Settings;
using PulseOffice.Foundation.Windowing;
using PulseOffice.Shell;
using PulseOffice.Shell.Surfaces;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Formats;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Formats.Pdf;
using PulseOffice.Printing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Document.Forms;
using PulseOffice.Security;
using PulseOffice.Text.Scripts;
using PulseOffice.Text.Spelling;
using PulseOffice.Text.Spelling.Hunspell;
using PulseOffice.Sheets.UI;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shortcuts;
using PulseOffice.UI.Theming;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.Text.Rich;
using Avalonia;

namespace PulseOffice.Desktop.Composition;

internal static class SettingsComposition
{
    public static IServiceCollection AddSettingsAndTheming(this IServiceCollection services, IUserDataPaths paths)
    {
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IColorContrast, WcagColorContrast>();
        services.AddSingleton<IAccentPalette, ReadableAccentPalette>();
        services.AddSingleton<IAccentService, AccentService>();

        services.AddSingleton<ISettingsProvider, ShellSettings>();
        services.AddSingleton<ISettingsProvider, UserIdentitySettings>();
        services.AddSingleton<ISettingsProvider, PulseOffice.Updates.UpdateSettings>();
        services.AddSingleton<PulseOffice.Foundation.Identity.SystemUserIdentity>();
        services.AddSingleton<PulseOffice.Foundation.Identity.IUserIdentity, SettingsUserIdentity>();
        services.AddSingleton<ISettingsStore>(_ => new InMemorySettingsStore(SettingScope.Policy));

        services.AddSingleton<ISettingsStore>(provider => new JsonSettingsStore(
            SettingScope.User,
            paths.SettingsFile,
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILoggerFactory>().CreateLogger<JsonSettingsStore>()));
        services.AddSingleton<ISettingsStore>(_ => new InMemorySettingsStore(SettingScope.Document));
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<PulseOffice.Settings.Navigation.ISettingsPageBuilder, PulseOffice.Settings.Navigation.SettingsPageBuilder>();
        services.AddSingleton<PulseOffice.Settings.Navigation.ISettingsNavigator, PulseOffice.Settings.Navigation.SettingsNavigator>();
        services.AddSingleton<PulseOffice.Settings.Navigation.ISettingValueComparer, PulseOffice.Settings.Navigation.SettingValueComparer>();
        services.AddSingleton<PulseOffice.Settings.Navigation.ISettingStates, PulseOffice.Settings.Navigation.SettingStates>();
        services.AddSingleton<PulseOffice.Settings.Navigation.ISettingOptionSource>(provider =>
            new PulseOffice.UI.Controls.Settings.ScannerDeviceOptions(
                provider.GetRequiredService<IScannerServiceProvider>(), ShellSettingKeys.ScannerDevices));
        services.AddSingleton<PulseOffice.Settings.Navigation.ISettingOptionSources, PulseOffice.Settings.Navigation.SettingOptionSources>();
        services.AddSingleton<PulseOffice.UI.Controls.Settings.ISettingEditor, PulseOffice.Shell.InterfaceModeSettingEditor>();
        services.AddSingleton<PulseOffice.UI.Controls.Settings.ISettingEditor, PulseOffice.UI.Controls.Settings.OptionSourceSettingEditor>();
        services.AddSingleton<PulseOffice.UI.Controls.Settings.ISettingEditor, PulseOffice.UI.Controls.Settings.SwitchSettingEditor>();
        services.AddSingleton<PulseOffice.UI.Controls.Settings.ISettingEditor, PulseOffice.UI.Controls.Settings.ChoiceSettingEditor>();
        services.AddSingleton<PulseOffice.UI.Controls.Settings.ISettingEditor, PulseOffice.UI.Controls.Settings.NumberSettingEditor>();
        services.AddSingleton<PulseOffice.UI.Controls.Settings.ISettingEditor, PulseOffice.UI.Controls.Settings.ColorSettingEditor>();
        services.AddSingleton<PulseOffice.UI.Controls.Settings.ISettingEditor, PulseOffice.UI.Controls.Settings.PathSettingEditor>();
        services.AddSingleton<PulseOffice.UI.Controls.Settings.ISettingEditor, PulseOffice.UI.Controls.Settings.TextSettingEditor>();
        services.AddSingleton<PulseOffice.UI.Controls.Settings.ISettingEditorFactory, PulseOffice.UI.Controls.Settings.SettingEditorFactory>();
        services.AddSingleton<PulseOffice.UI.Controls.Settings.SettingsViewServices>();
        services.AddSingleton<PulseOffice.UI.Controls.Settings.ISettingsDialog, PulseOffice.UI.Controls.Settings.SettingsDialog>();
        services.AddSingleton<PulseOffice.Shell.About.IAboutDialog, PulseOffice.Shell.About.AboutDialog>();
        services.AddSingleton<PulseOffice.Updates.IInstalledVersion, PulseOffice.Updates.EntryAssemblyVersion>();
        services.AddSingleton<PulseOffice.Updates.IUpdateEndpoints, PulseOffice.Updates.GitHubUpdateEndpoints>();
        services.AddSingleton<PulseOffice.Updates.IReleaseDocument, PulseOffice.Updates.GitHubReleaseDocument>();
        services.AddSingleton<PulseOffice.Updates.IReleaseFeed, PulseOffice.Updates.GitHubReleaseFeed>();
        services.AddSingleton<PulseOffice.Updates.IUpdatePackageChoice, PulseOffice.Updates.UpdatePackageChoice>();
        services.AddSingleton<PulseOffice.Updates.IUpdateTrustKey, PulseOffice.Updates.EmbeddedUpdateTrustKey>();
        services.AddSingleton<PulseOffice.Updates.IUpdateTrust, PulseOffice.Updates.EcdsaUpdateTrust>();
        services.AddSingleton<PulseOffice.Updates.IChecksumList, PulseOffice.Updates.Sha256SumsList>();
        services.AddSingleton<PulseOffice.Updates.IUpdateDownloader, PulseOffice.Updates.UpdateDownloader>();
        services.AddSingleton<PulseOffice.Updates.IUpdateStateStore, PulseOffice.Updates.JsonUpdateStateStore>();
        services.TryAddSingleton<PulseOffice.Platform.IEnvironmentVariables, PulseOffice.Platform.SystemEnvironmentVariables>();
        services.AddSingleton<PulseOffice.Platform.IAppImageLocation, PulseOffice.Platform.EnvironmentAppImageLocation>();
        services.AddSingleton<PulseOffice.Platform.Linux.IAppImageLauncherScript, PulseOffice.Platform.Linux.AppImageLauncherScript>();
        services.AddSingleton<DesktopIntegrationProvider>();
        services.AddSingleton(provider => provider.GetRequiredService<DesktopIntegrationProvider>().Select());
        services.AddSingleton<PulseOffice.Shell.Desktop.IDesktopIntegrationPreferencesStore, PulseOffice.Shell.Desktop.JsonDesktopIntegrationPreferencesStore>();
        services.AddSingleton<PulseOffice.Shell.Desktop.IDesktopIntegrationCoordinator, PulseOffice.Shell.Desktop.DesktopIntegrationCoordinator>();
        services.AddSingleton<UpdateInstallerProvider>();
        services.AddSingleton(provider => provider.GetRequiredService<UpdateInstallerProvider>().Select());
        services.AddSingleton<PulseOffice.Updates.IUpdateService, PulseOffice.Updates.UpdateService>();
        services.AddSingleton<PulseOffice.Shell.Updates.IUpdateCoordinator, PulseOffice.Shell.Updates.UpdateCoordinator>();

        return services;
    }
}
