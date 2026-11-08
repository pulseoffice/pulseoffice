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

internal static class ObjectsAndScriptingComposition
{
    public static IServiceCollection AddObjectsAndScripting(this IServiceCollection services)
    {
        services.AddSingleton<PulseOffice.UI.Controls.Objects.IChosenFileReader, PulseOffice.UI.Controls.Objects.ChosenFileReader>();
        services.AddSingleton<PulseOffice.UI.Controls.Objects.IRasterImageFiles, PulseOffice.UI.Controls.Objects.RasterImageFiles>();
        services.AddSingleton<PulseOffice.UI.Controls.Objects.IBitmapPixels, PulseOffice.UI.Controls.Objects.BitmapPixels>();
        services.TryAddSingleton<PulseOffice.Drawing.Assets.IImageExtents, PulseOffice.Drawing.Assets.ImageExtents>();
        services.AddSingleton<PulseOffice.UI.Controls.Objects.IRasterRecolor, PulseOffice.UI.Controls.Objects.RasterRecolor>();
        services.AddSingleton<ISheetImageInsertion, SheetImageInsertion>();
        services.AddSingleton<IScannerServiceProvider, ScannerServiceProvider>();
        services.AddSingleton<ShellScannerCommands>();
        services.AddSingleton<IShellLaunch, SystemShellLaunch>();
        services.AddSingleton<IShellOpener, ShellOpener>();
        services.AddSingleton<PulseOffice.Platform.ITemporaryFiles, PulseOffice.Platform.TemporaryFiles>();
        services.AddSingleton<PulseOffice.UI.Controls.Objects.IExternalEditWatcher, PulseOffice.UI.Controls.Objects.ExternalEditWatcher>();
        services.AddSingleton(provider => new Formats.Interchange.HtmlSpreadsheetWriter(
            provider.GetRequiredService<Sheets.Formatting.ICellPresenter>()));
        services.AddSingleton<LedgerWebCommands>();
        services.AddSingleton<LedgerObjectFileCommands>();
        services.AddSingleton<PulseOffice.Text.Redaction.ITextRedactor, PulseOffice.Text.Redaction.TextRedactor>();
        services.AddSingleton<PulseOffice.UI.Controls.Prompts.IRedactionPrompt, PulseOffice.UI.Controls.Prompts.RedactionPrompt>();
        services.AddSingleton<PulseOffice.UI.Controls.Prompts.IZoomPrompt, PulseOffice.UI.Controls.Prompts.ZoomPrompt>();
        services.AddSingleton<PulseOffice.UI.Controls.Zoom.IZoomChoice, PulseOffice.UI.Controls.Zoom.ZoomChoice>();
        services.AddSingleton<PulseOffice.Drawing.Assets.IQrVersionTable, PulseOffice.Drawing.Assets.QrVersionTable>();
        services.AddSingleton<PulseOffice.Drawing.Assets.IQrReedSolomon, PulseOffice.Drawing.Assets.QrReedSolomon>();
        services.AddSingleton<PulseOffice.Drawing.Assets.IQrPenalty, PulseOffice.Drawing.Assets.QrPenalty>();
        services.AddSingleton<PulseOffice.Drawing.Assets.IQrEncoder>(provider => new PulseOffice.Drawing.Assets.QrEncoder(
            provider.GetRequiredService<PulseOffice.Drawing.Assets.IQrVersionTable>(),
            provider.GetRequiredService<PulseOffice.Drawing.Assets.IQrReedSolomon>(),
            provider.GetRequiredService<PulseOffice.Drawing.Assets.IQrPenalty>()));
        services.AddSingleton<PulseOffice.UI.Controls.Prompts.IQrCodePrompt, PulseOffice.UI.Controls.Prompts.QrCodePrompt>();
        services.AddSingleton<PulseOffice.UI.Controls.Prompts.ITextConversionPrompt, PulseOffice.UI.Controls.Prompts.TextConversionPrompt>();
        services.AddSingleton<ISheetRedaction, SheetRedaction>();
        services.AddSingleton<IDocumentSharing, FileDocumentSharing>();
        services.AddSingleton<LedgerRedactionCommands>();
        services.AddSingleton<LedgerPanelCommands>();
        services.AddSingleton<Scripting.IScriptOutput, Scripting.ScriptOutput>();
        services.AddSingleton<PulseOffice.Scripting.Editor.IScriptApiSection, PulseOffice.Scripting.Editor.ShellScriptApi>();
        services.AddSingleton<PulseOffice.Scripting.Editor.IScriptApiSection, SheetScriptApi>();
        services.AddSingleton<PulseOffice.Scripting.Editor.IScriptApiProvider, PulseOffice.Scripting.Editor.ScriptApiProvider>();
        services.AddSingleton<PulseOffice.Scripting.Editor.Facades.IScriptClipboardOperations, PulseOffice.Scripting.Editor.Facades.ScriptClipboardOperations>();
        services.AddSingleton<PulseOffice.Scripting.Editor.Facades.IScriptDispatcher>(provider =>
            new PulseOffice.Scripting.Editor.Facades.ReadOnlyGuardedDispatcher(
                new PulseOffice.Scripting.Editor.Facades.UiThreadScriptDispatcher(),
                () => provider.GetRequiredService<IDocumentWorkspace>().Active?.IsReadOnly == true));
        services.AddSingleton<Sheets.UI.Scripting.LedgerScriptHost>();
        services.TryAddSingleton<PulseOffice.Scripting.IScriptExecutionPermissions, PulseOffice.Scripting.ScriptExecutionPermissions>();
        services.AddSingleton<PulseOffice.Scripting.IMacroFingerprints, PulseOffice.Scripting.Sha256MacroFingerprints>();
        services.AddSingleton<PulseOffice.Scripting.IMacroWriteGrants>(provider => new PulseOffice.Scripting.FileMacroWriteGrants(
            provider.GetRequiredService<IUserDataPaths>().Configuration("macro-file-permissions.json"),
            provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PulseOffice.Scripting.FileMacroWriteGrants>>()));
        services.AddSingleton<PulseOffice.Scripting.IMacroPermissionPolicy, PulseOffice.Scripting.MacroPermissionPolicy>();
        services.TryAddSingleton<PulseOffice.Scripting.IMacroFileFolder>(provider => new PulseOffice.Scripting.MacroFileFolder(provider.GetRequiredService<PulseOffice.Platform.IHostSystem>()));
        services.TryAddSingleton<PulseOffice.Scripting.ScriptFilesFacade>();
        services.AddSingleton<PulseOffice.Scripting.Editor.IMacroWriteConfirmation, PulseOffice.Scripting.Editor.MacroWriteConfirmation>();
        services.AddSingleton<PulseOffice.Scripting.Editor.IMacroWriteAccess, PulseOffice.Scripting.Editor.MacroWriteAccess>();
        services.AddSingleton<PulseOffice.Scripting.Editor.IGuardedMacroRuns, PulseOffice.Scripting.Editor.GuardedMacroRuns>();
        services.AddSingleton<PulseOffice.Scripting.Editor.IMacroEditorLauncher, PulseOffice.Scripting.Editor.MacroEditorLauncher>();
        services.AddSingleton<Sheets.UI.Scripting.DocumentFacade>();
        services.AddSingleton<Sheets.UI.Scripting.IScriptReferences, Sheets.UI.Scripting.ScriptReferences>();
        services.AddSingleton<Sheets.UI.Scripting.IScriptColors, Sheets.UI.Scripting.ScriptColors>();
        services.AddSingleton<Sheets.UI.Scripting.IScriptProtection, Sheets.UI.Scripting.ScriptProtection>();
        services.AddSingleton<Sheets.UI.Scripting.SheetFacade>();
        services.AddSingleton<Sheets.UI.Scripting.PivotFacade>();
        services.AddSingleton<Sheets.UI.Scripting.PrintFacade>();
        services.AddSingleton<Sheets.UI.Scripting.FormatRuleFacade>();
        services.AddSingleton<Sheets.UI.Scripting.IScriptSheetEdits, Sheets.UI.Scripting.ScriptSheetEdits>();
        services.AddSingleton<Sheets.UI.Scripting.ValidationFacade>();
        services.AddSingleton<Sheets.UI.Scripting.FilterFacade>();
        services.AddSingleton<Sheets.UI.Scripting.ScenarioFacade>();
        services.AddSingleton<Sheets.UI.Scripting.IScriptGlobals, Sheets.UI.Scripting.ScriptGlobals>();
        services.AddSingleton<PulseOffice.Scripting.IModuleScriptGlobals>(provider => provider.GetRequiredService<Sheets.UI.Scripting.IScriptGlobals>());
        services.AddSingleton<Scripting.Editor.IScriptDocumentation, Scripting.Editor.ScriptDocumentation>();

        return services;
    }
}
