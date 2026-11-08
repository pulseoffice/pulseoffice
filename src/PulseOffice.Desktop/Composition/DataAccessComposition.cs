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

internal static class DataAccessComposition
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IUserDataPaths paths)
    {
        services.AddSingleton<Data.ICredentialStore>(provider =>
            new Data.VaultCredentialStore(provider.GetRequiredService<ISecretVaultProvider>()));
        services.AddSingleton(provider =>
        {
            var registry = new Data.DatabaseProviderRegistry();
            var credentials = provider.GetRequiredService<Data.ICredentialStore>();

            registry.Register(new Data.Sqlite.SqliteDatabaseProvider(credentials));
            registry.Register(new Data.PostgreSql.PostgreSqlDatabaseProvider(credentials));
            registry.Register(new Data.MySql.MySqlDatabaseProvider(credentials));
            registry.Register(new Data.SqlServer.SqlServerDatabaseProvider(credentials));
            registry.Register(new Data.Odbc.OdbcDatabaseProvider(credentials));

            return registry;
        });
        services.AddSingleton<Data.IConnectionDirectory>(provider =>
            new Data.FileConnectionDirectory(
                paths.Configuration("connections.json"),
                provider.GetRequiredService<Data.ICredentialStore>(),
                privateFiles: provider.GetRequiredService<PulseOffice.Platform.IPrivateFiles>()));
        services.AddSingleton<Data.IReadOnlySqlPolicy, Data.ReadOnlySqlPolicy>();
        services.AddSingleton<Data.IExternalQueryConsentLedger, Data.ExternalQueryConsentLedger>();
        services.AddSingleton<Data.IExternalQueryGate, Data.UI.ExternalQueryConsent>();
        services.AddSingleton<Sheets.Queries.IQueryRefresher>(provider =>
            new Sheets.Queries.QueryRefresher(
                provider.GetRequiredService<Data.DatabaseProviderRegistry>(),
                provider.GetRequiredService<Data.IConnectionDirectory>(),
                provider.GetRequiredService<Data.IExternalQueryGate>(),
                readOnly: provider.GetRequiredService<Data.IReadOnlySqlPolicy>()));
        services.AddSingleton<Sheets.Queries.IQueryValueConverter, Sheets.Queries.QueryValueConverter>();
        services.AddSingleton<Sheets.Queries.IPivotSourceRefresher>(provider =>
            new Sheets.Queries.PivotSourceRefresher(
                provider.GetRequiredService<Data.DatabaseProviderRegistry>(),
                provider.GetRequiredService<Data.IConnectionDirectory>(),
                provider.GetRequiredService<Data.IExternalQueryGate>(),
                provider.GetRequiredService<Sheets.Queries.IQueryValueConverter>(),
                provider.GetRequiredService<Data.IReadOnlySqlPolicy>()));

        return services;
    }
}
