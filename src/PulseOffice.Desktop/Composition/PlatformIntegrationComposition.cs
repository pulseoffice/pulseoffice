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

internal static class PlatformIntegrationComposition
{
    public static IServiceCollection AddPlatformIntegration(this IServiceCollection services)
    {
        services.AddLocalization();
        services.TryAddSingleton<PulseOffice.Platform.IHostSystem, PulseOffice.Platform.RuntimeHostSystem>();
        services.TryAddSingleton<PulseOffice.Platform.IPrivateFilesFactory, PulseOffice.Platform.PrivateFilesFactory>();
        services.TryAddSingleton<PulseOffice.Platform.IPrivateFiles>(provider => provider.GetRequiredService<PulseOffice.Platform.IPrivateFilesFactory>().Create());
        services.AddSingleton<PulseOffice.Sheets.Engine.Recalculation.IWebResourceAccess>(
            provider => new PulseOffice.Sheets.UI.WebResourceAccess(
                provider.GetRequiredService<Settings.ISettingsService>(),
                provider.GetRequiredService<PulseOffice.Foundation.Network.IOutboundHttpClients>()));
        services.AddSingleton<PulseOffice.Foundation.Network.IAddressClassifier, PulseOffice.Foundation.Network.AddressClassifier>();
        services.AddSingleton<PulseOffice.Foundation.Network.IOutboundHttpClients, PulseOffice.Foundation.Network.OutboundHttpClients>();
        services.AddSingleton<PulseOffice.Foundation.Network.INetworkSharePaths, PulseOffice.Foundation.Network.NetworkSharePaths>();
        services.AddSingleton<IObjectOrdering, ObjectOrdering>();
        services.AddSingleton<IObjectAlignment, ObjectAlignment>();
        services.AddSingleton<ISecretVaultProvider, SecretVaultProvider>();
        services.AddSingleton<IExternalProcess, ExternalProcess>();
        services.AddSingleton<IMailComposerProvider, MailComposerProvider>();
        services.AddSingleton<ISharingProcessRunner, SharingProcessRunner>();
        services.AddSingleton<IFileSharingProvider, FileSharingProvider>();

        return services;
    }
}
