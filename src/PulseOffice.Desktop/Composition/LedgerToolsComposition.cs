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

internal static class LedgerToolsComposition
{
    public static IServiceCollection AddLedgerTools(this IServiceCollection services)
    {
        services.AddSingleton<ISheetRandomFill, SheetRandomFill>();
        services.AddSingleton<ISheetSelectionSet, SheetSelectionSet>();
        services.AddSingleton<LedgerMiscCommands>();
        services.AddSingleton<LedgerSheetExtraCommands>();
        services.AddSingleton<LedgerDocumentExtraCommands>();
        services.AddSingleton<PulseOffice.Formulas.IFormulaCatalog, PulseOffice.Formulas.FormulaCatalog>();
        services.AddSingleton<PulseOffice.Formulas.Editor.IFormulaEditorTexts, PulseOffice.Formulas.Editor.FormulaEditorTexts>();
        services.AddSingleton<PulseOffice.Formulas.Editor.IFormulaPrompt, PulseOffice.Formulas.Editor.FormulaPrompt>();
        services.AddSingleton<LedgerGalleryCommands>();
        services.AddSingleton<LedgerFinalCommands>();
        services.AddSingleton<LedgerSelectionExtraCommands>();
        services.AddSingleton<PulseOffice.Document.Themes.IThemePalettes, PulseOffice.Document.Themes.ThemePalettes>();
        services.AddSingleton<PulseOffice.Sheets.Styling.ISheetThemeApplier, PulseOffice.Sheets.Styling.SheetThemeApplier>();
        services.TryAddSingleton<PulseOffice.Formats.Interchange.ICsvNumberParser, PulseOffice.Formats.Interchange.CsvNumberParser>();
        services.AddSingleton<PulseOffice.Sheets.Values.IExternalCellValues, PulseOffice.Formats.Interchange.ExternalCellValues>();
        services.AddSingleton<PulseOffice.Sheets.Engine.Operations.IExternalDataWriter, PulseOffice.Sheets.Engine.Operations.ExternalDataWriter>();
        services.AddSingleton<PulseOffice.Formats.Interchange.ITabularSources>(provider =>
            new PulseOffice.Formats.Interchange.TabularSources(provider.GetRequiredService<PulseOffice.Foundation.Text.IDelimitedText>()));
        services.AddSingleton<PulseOffice.Sheets.UI.ExternalData.IExternalDataFetcher, PulseOffice.Sheets.UI.ExternalData.ExternalDataFetcher>();
        services.AddSingleton<PulseOffice.Sheets.UI.ExternalData.IExternalDataService, PulseOffice.Sheets.UI.ExternalData.ExternalDataService>();
        services.AddSingleton<PulseOffice.Sheets.UI.ExternalData.IExternalLinkSchedule, PulseOffice.Sheets.UI.ExternalData.ExternalLinkSchedule>();
        services.AddSingleton<PulseOffice.Sheets.UI.ExternalData.ExternalLinkAutoRefresh>();
        services.AddSingleton<PulseOffice.Data.UI.ITabularSnapshots, PulseOffice.Data.UI.TabularSnapshots>();
        services.AddSingleton<PulseOffice.Data.UI.ITableCellTextStyles, PulseOffice.Data.UI.TableCellTextStyles>();
        services.AddSingleton<PulseOffice.Data.UI.IDataTableRenderer, PulseOffice.Data.UI.DataTableRenderer>();
        services.AddSingleton<LedgerTailCommands>();
        services.AddSingleton<ISeriesFill, SeriesFill>();
        services.AddSingleton<ISheetValueWrites, SheetValueWrites>();
        services.AddSingleton<LedgerPartialCommands>();
        services.AddSingleton<LedgerDatabaseRangeCommands>();
        services.AddSingleton<LedgerTableInsertCommands>();
        services.AddSingleton<ILedgerTableLooks, LedgerTableLooks>();
        services.AddSingleton<LedgerObjectCommands>();
        services.AddSingleton<LedgerChangeCommands>();
        services.AddSingleton<ILedgerChangeResolution, LedgerChangeResolution>();
        services.AddSingleton<LedgerReviewSource>();
        services.AddSingleton<PulseOffice.Scripting.IScriptEngine, PulseOffice.Scripting.JavaScript.JavaScriptEngine>();
        services.AddSingleton<PulseOffice.Scripting.Python.IronPythonScriptEngine>();
        services.AddSingleton<PulseOffice.Scripting.CPython.IPythonDiscovery>(provider => new PulseOffice.Scripting.CPython.PythonDiscovery(provider.GetRequiredService<PulseOffice.Platform.IHostSystem>()));
        services.AddSingleton(provider => new PulseOffice.Scripting.CPython.StandalonePythonCatalog(provider.GetRequiredService<PulseOffice.Platform.IHostSystem>()));
        services.AddSingleton<PulseOffice.Scripting.CPython.IStandalonePythonStore>(provider => new PulseOffice.Scripting.CPython.StandalonePythonStore(
            Path.Combine(provider.GetRequiredService<IUserDataPaths>().ConfigurationDirectory, "python-runtime"),
            provider.GetRequiredService<HttpClient>(),
            provider.GetRequiredService<PulseOffice.Scripting.CPython.StandalonePythonCatalog>(),
            provider.GetRequiredService<PulseOffice.Scripting.CPython.IPythonDiscovery>(),
            provider.GetRequiredService<ILogger<PulseOffice.Scripting.CPython.StandalonePythonStore>>(),
            provider.GetRequiredService<PulseOffice.Platform.IHostSystem>()));
        services.AddSingleton<PulseOffice.Scripting.CPython.IPythonRuntimeLocator, PulseOffice.Scripting.CPython.PythonRuntimeLocator>();
        services.AddSingleton<PulseOffice.Scripting.CPython.ICPythonHost, PulseOffice.Scripting.CPython.CPythonHost>();
        services.AddSingleton<PulseOffice.Scripting.CPython.IPythonDownloadConsent, PulseOffice.Scripting.Editor.PythonDownloadConsent>();
        services.AddSingleton<PulseOffice.Scripting.CPython.CPythonScriptEngine>();
        services.AddSingleton<PulseOffice.Scripting.IScriptEngine>(provider =>
            provider.GetRequiredService<ISettingsService>().Get<string>(ShellSettingKeys.PythonEngine) == "cpython"
                ? provider.GetRequiredService<PulseOffice.Scripting.CPython.CPythonScriptEngine>()
                : provider.GetRequiredService<PulseOffice.Scripting.Python.IronPythonScriptEngine>());
        services.AddSingleton<LedgerMacroCommands>();
        services.AddSingleton<ActiveShellViewOptions>();
        services.AddSingleton<IShellViewOptions>(
            provider => provider.GetRequiredService<ActiveShellViewOptions>());
        services.AddSingleton<LedgerShellViewCommands>();
        services.AddSingleton<PulseOffice.UI.Controls.Zoom.IZoomLadder, PulseOffice.UI.Controls.Zoom.ZoomLadder>();
        services.AddSingleton<LedgerZoomCommands>();
        services.AddSingleton<PulseOffice.Shell.IDocumentSaver, PulseOffice.Shell.ModuleDocumentSaver>();
        services.AddSingleton<PulseOffice.Shell.IUnsavedChangesGuard, PulseOffice.Shell.UnsavedChangesGuard>();
        services.AddSingleton<PulseOffice.Shell.ISignedSaveGuard, PulseOffice.Shell.SignedSaveGuard>();
        services.AddSingleton<PulseOffice.Formats.Packaging.Legacy.ILegacyOdfUpgrader, PulseOffice.Formats.Packaging.Legacy.LegacyOdfUpgrader>();
        services.AddSingleton<PulseOffice.Formats.Packaging.Encryption.OpenPgp.IOpenPgp, PulseOffice.Formats.Packaging.Encryption.OpenPgp.BouncyCastleOpenPgp>();
        services.AddSingleton<PulseOffice.Formats.Packaging.Encryption.OpenPgp.IOpenPgpProtectionFactory, PulseOffice.Formats.Packaging.Encryption.OpenPgp.OpenPgpProtectionFactory>();
        services.AddSingleton<PulseOffice.Formats.Packaging.Encryption.IOdfPackageEncryption, PulseOffice.Formats.Packaging.Encryption.OdfPackageEncryption>();
        services.AddSingleton<PulseOffice.UI.Controls.Prompts.IDocumentPasswordPrompt, PulseOffice.UI.Controls.Prompts.DocumentPasswordPrompt>();
        services.AddSingleton<PulseOffice.Shell.IDocumentPasswordGate, PulseOffice.Shell.DocumentPasswordGate>();
        services.AddSingleton<Func<PulseOffice.Shell.IUnsavedChangesGuard>>(
            provider => provider.GetRequiredService<PulseOffice.Shell.IUnsavedChangesGuard>);
        services.AddSingleton<Document.Data.IDataTableLoader>(provider =>
            new Document.Data.DataTableLoader(
                provider.GetRequiredService<Data.DatabaseProviderRegistry>(),
                provider.GetRequiredService<Data.IConnectionDirectory>(),
                provider.GetRequiredService<Data.IReadOnlySqlPolicy>()));
        services.AddSingleton<Data.UI.IDataImportFlow>(provider =>
            new Data.UI.DataImportFlow(
                provider.GetRequiredService<Data.IConnectionDirectory>(),
                provider.GetRequiredService<Data.DatabaseProviderRegistry>(),
                provider.GetRequiredService<Document.Data.IDataTableLoader>(),
                provider.GetRequiredService<PulseOffice.UI.Controls.Services.IDialogService>(),
                provider.GetRequiredService<IIconCatalog>(),
                provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>(),
                provider.GetRequiredService<PulseOffice.Localization.IDiagnosticTexts>()));
        services.AddSingleton<LedgerCommentCommands>();
        services.AddSingleton<LedgerAnalysisCommands>();
        services.AddSingleton<LedgerToolCommands>();
        services.AddSingleton<LedgerStyleCommands>();
        services.AddSingleton<ISheetSnapshots, SheetSnapshots>();
        services.AddSingleton<IWorkbookCheckpoints, WorkbookCheckpoints>();
        services.AddSingleton<IAutomaticRefreshHistory, AutomaticRefreshHistory>();
        services.AddSingleton<ISheetDuplicator, SheetDuplicator>();
        services.AddSingleton<ISheetFillAcross, SheetFillAcross>();
        services.AddSingleton<ISheetRenaming, SheetRenaming>();
        services.AddSingleton<IChartSheetReferences, ChartSheetReferences>();
        services.AddSingleton<IObjectPlacementMementos, ObjectPlacementMementos>();
        services.AddSingleton<IChartSourceLayout, ChartSourceLayout>();
        services.AddSingleton<IChartSourceReferences, ChartSourceReferences>();
        services.AddSingleton<IDefinedNameRules, DefinedNameRules>();
        services.AddSingleton<PulseOffice.Document.Embedding.ITableStructure, PulseOffice.Document.Embedding.TableStructure>();
        services.AddSingleton<IGoalSeek, GoalSeek>();
        services.AddSingleton<ISolver, Solver>();
        services.AddSingleton<PulseOffice.Sheets.UI.Analysis.ISolverConstraintRows>(_ => new PulseOffice.Sheets.UI.Analysis.SolverConstraintRows(System.Globalization.CultureInfo.CurrentCulture));
        services.AddSingleton<IScenarioManager, ScenarioManager>();
        services.AddSingleton<PulseOffice.Sheets.UI.Dialogs.IValidationRules, PulseOffice.Sheets.UI.Dialogs.ValidationRules>();
        services.AddSingleton<PulseOffice.Sheets.Engine.Operations.IValidationPlacement, PulseOffice.Sheets.Engine.Operations.ValidationPlacement>();
        services.AddSingleton<PulseOffice.Sheets.UI.Dialogs.IScenarioPlanner, PulseOffice.Sheets.UI.Dialogs.ScenarioPlanner>();
        services.AddSingleton<ISheetRangeReferences, SheetRangeReferences>();
        services.AddSingleton<PulseOffice.Sheets.UI.Dialogs.ISheetReferenceReader, PulseOffice.Sheets.UI.Dialogs.SheetReferenceReader>();
        services.AddSingleton<PulseOffice.Sheets.UI.Dialogs.ISheetReferenceSources, PulseOffice.Sheets.UI.Dialogs.SheetReferenceSources>();
        services.AddSingleton<IDependencyTracer, DependencyTracer>();
        services.AddSingleton<IMultipleOperations, MultipleOperations>();
        services.AddSingleton<LedgerStructureCommands>();
        services.AddSingleton<IWorksheetTabPolicy, WorksheetTabPolicy>();
        services.AddSingleton<IQueryRefreshRunner, QueryRefreshRunner>();
        services.AddSingleton<LedgerSheetCommands>();

        return services;
    }
}
