using PulseOffice.Formats.Intake;
using PulseOffice.Documents;
using PulseOffice.Foundation.Pages;
using Avalonia;
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
using PulseOffice.Agent.UI;
using PulseOffice.Documents.UI;
using PulseOffice.Documents.Layout;

namespace PulseOffice.Desktop.Composition;

public static class ServiceConfiguration
{
    public static ServiceProviderOptions ProviderOptions => new() { ValidateOnBuild = true, ValidateScopes = true };

    public static ServiceProvider Build(Application application, ILoggerFactory loggerFactory, PulseOffice.UI.Controls.Fonts.FontFamilyMappingTable fontMappings) =>
        Register(application, loggerFactory, fontMappings).BuildServiceProvider(ProviderOptions);

    public static ServiceCollection Register(Application application, ILoggerFactory loggerFactory, PulseOffice.UI.Controls.Fonts.FontFamilyMappingTable fontMappings)
    {
        var services = new ServiceCollection();
        var paths = new UserDataPaths();
        services.AddSingleton<IUserDataPaths>(paths);
        services.AddSingleton(fontMappings);
        services.AddFonts(paths);

        services.AddSingleton(application);
        services.AddSingleton(loggerFactory);
        services.AddLogging();

        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<IWindowChromeProvider, WindowChromeProvider>();
        services.AddSingleton<IApplicationVisibility>(provider => provider.GetRequiredService<PulseOffice.Platform.IHostSystem>().IsMacOS ? new PulseOffice.Platform.MacOS.MacApplicationVisibility() : new UnsupportedApplicationVisibility());

        services.AddPlatformIntegration();
        services.AddDataAccess(paths);
        services.AddDrawing(paths);
        services.AddSettingsAndTheming(paths);
        services.AddSharedControls();
        services.AddCommanding(paths);
        services.AddLedgerCore();
        services.AddDeckDesign(paths);
        services.AddDocumentFormats();
        services.AddDeckEditing();
        services.AddPrintingAndCloud(paths);
        services.AddObjectsAndScripting();
        services.AddLedgerData(paths);
        services.AddProofing(paths);
        services.AddSignatures();
        services.AddLedgerTools();
        services.AddShellWindow(paths);

        services.AddAgent(paths);
        services.AddSheetsEngine();
        services.AddDocumentsModel();
        services.AddFolioMeasurement();
        services.AddDocumentsLayout();
        services.AddFolioUI();

        return services;
    }
}
