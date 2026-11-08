using PulseOffice.UI.Icons;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.UI;
using PulseOffice.UI.Commands;

namespace PulseOffice.Desktop.Composition;

public sealed class ShellComposition(IServiceProvider services)
{
    private readonly IServiceProvider _services = services;

    public CommandRegistry Compose()
    {
        var registry = _services.GetRequiredService<CommandRegistry>();
        registry.Use(CommandSurface.Menu, PulseMenuLayout.Build());
        registry.Use(CommandSurface.Ribbon, PulseRibbonLayout.Build());
        registry.Use(CommandSurface.ContextMenu, PulseContextLayout.Build());

        registry.RegisterRange(_services.GetRequiredService<ShellCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<ShellInterfaceCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<ShellAutoCorrectCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<ShellDictionaryCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<ShellPrintCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<ShellDocumentCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<ShellPasswordCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<ShellSendCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<ShellRemoteCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<ShellSignatureCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<ShellFormCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<ShellFontCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<ShellMacroRecorderCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<ShellTemplateCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioEditCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioZoomCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioDialogCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioListCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioStyleCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioSectionCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioReviewCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioExportCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioMailMergeCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Scripting.FolioMacroCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioMasterCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioToolCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioViewCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioClipboardCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioBreakCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioNavigationCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioTextToolCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioShapeCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioSendCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioCellCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioAccessibilityCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioAutoTextCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioDrawCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioSpellingCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioTitlePageCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioCharacterCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioParagraphCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioRibbonCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioInsertCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioPageCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioFindCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioTableCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioObjectCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioObjectInsertCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioImageCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioChartCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioContentControlCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioFieldCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioObjectLinkCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Documents.UI.Commands.FolioIndexCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckEditCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckFieldCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckCharacterCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckFormatCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckRibbonCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckTextDialogCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckTextFrameCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckZoomCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckSlideSetupCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.ObjectDialogs.DeckObjectDialogCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckInsertCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckConnectorCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckSlideCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckNavigationCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckFormControlCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckDrawCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckImageCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckClipboardCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckFindCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckSpellingCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckViewCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckArrangeCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.Remote.DeckRemoteCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckAnimatedImageCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckMediaCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckLinkCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckFormFieldCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckFontworkCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckMotionCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckStyleCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckPanelCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckExtraInsertCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.Themes.DeckThemeCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.Design.DeckLayoutCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckMasterCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckConvertCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckObjectCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckExportCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckTableCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckToolCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckMacroCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<PulseOffice.Presentations.UI.DeckFormCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerPrintCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerCompareCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<ShellScannerCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerWebCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerObjectFileCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerRedactionCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerPanelCommands>().GetCommands());

        var panel = _services.GetRequiredService<PulseOffice.UI.Controls.ISidePanelHost>();

        var styleSources = _services.GetRequiredService<PulseOffice.Shell.StylePanels.ModuleStylesSource>();
        styleSources.Register(ModuleKind.Sheets, new Sheets.UI.LedgerStylesSource(
            _services.GetRequiredService<Sheets.UI.IActiveSheetContext>(),
            _services.GetRequiredService<LedgerStyleCommands>(),
            _services.GetRequiredService<ITextCatalog>()));
        styleSources.Register(ModuleKind.Presentations, new PulseOffice.Presentations.UI.DeckStylesSource(
            _services.GetRequiredService<PulseOffice.Presentations.UI.IActiveDeckContext>(),
            _services.GetRequiredService<PulseOffice.Presentations.UI.DeckStyleCommands>(),
            _services.GetRequiredService<ITextCatalog>()));
        styleSources.Register(ModuleKind.Documents, _services.GetRequiredService<PulseOffice.Documents.UI.StyleCatalog.FolioStylesSource>());
        var navigatorSources = _services.GetRequiredService<PulseOffice.Shell.Navigation.ModuleNavigatorSource>();
        navigatorSources.Register(ModuleKind.Documents, _services.GetRequiredService<PulseOffice.Documents.UI.Editing.FolioNavigatorSource>());
        navigatorSources.Register(ModuleKind.Presentations, _services.GetRequiredService<PulseOffice.Presentations.UI.DeckNavigatorSource>());
        navigatorSources.Register(ModuleKind.Sheets, _services.GetRequiredService<Sheets.UI.LedgerNavigatorSource>());
        panel.Register(new PulseOffice.UI.Controls.SidePanelPage("navigator", () => new PulseOffice.UI.Controls.Navigation.NavigatorPanel(
            navigatorSources, _services.GetRequiredService<IIconCatalog>(), _services.GetRequiredService<ITextCatalog>())));
        var reviewSources = _services.GetRequiredService<PulseOffice.Shell.Navigation.ModuleReviewSource>();
        reviewSources.Register(ModuleKind.Documents, _services.GetRequiredService<PulseOffice.Documents.UI.Editing.FolioReviewSource>());
        reviewSources.Register(ModuleKind.Sheets, _services.GetRequiredService<Sheets.UI.LedgerReviewSource>());
        reviewSources.Register(ModuleKind.Presentations, _services.GetRequiredService<PulseOffice.Presentations.UI.DeckReviewSource>());
        panel.Register(new PulseOffice.UI.Controls.SidePanelPage("review", () => new PulseOffice.UI.Controls.Review.ReviewPanel(
            reviewSources, _services.GetRequiredService<IIconCatalog>(), _services.GetRequiredService<ITextCatalog>(),
            requests: _services.GetRequiredService<PulseOffice.UI.Controls.Review.IReviewPanelRequests>())));
        _services.GetRequiredService<ITextReviewEditWatch>().Start();
        panel.Register(new PulseOffice.UI.Controls.SidePanelPage(PulseOffice.UI.Controls.Spelling.TextReviewSession.PageKey, () => new PulseOffice.UI.Controls.Spelling.TextReviewPanel(
            _services.GetRequiredService<PulseOffice.UI.Controls.Spelling.ITextReviewSession>(), _services.GetRequiredService<ITextCatalog>(), _services.GetRequiredService<IIconCatalog>(),
            openDictionaries: () => OpenDictionaries(registry))));
        panel.Register(new PulseOffice.UI.Controls.SidePanelPage("styles", () => new PulseOffice.UI.Controls.StylePanels.StylesPanel(
            styleSources, new PulseOffice.UI.Controls.StylePanels.StylesPanelServices(
                _services.GetRequiredService<PulseOffice.UI.Controls.StylePanels.IStyleRecents>(),
                _services.GetRequiredService<IIconCatalog>(),
                _services.GetRequiredService<ITextCatalog>()))));
        registry.RegisterRange(_services.GetRequiredService<LedgerPivotCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerStructureCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerSheetCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerDataCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerExternalDataCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerMergeCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerImageCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerImageCommands>().GetDialogCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerFillCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerFillCommands>().GetDialogCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerPasteCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerSelectionCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerFilterCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerFormatDialogCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerConditionCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerRibbonCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerFunctionCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerTextCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerScriptCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerNameCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerViewCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerCalculationCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerCellShiftCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerSheetToolsCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerSheetToolsCommands>().GetDialogCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerInsertCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerInsertCommands>().GetDialogCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerDataDialogCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerSparklineCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerFormCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerFormDialogCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerDrawCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerSpellingCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerSignatureCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerMiscCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerSheetExtraCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerDocumentExtraCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerGalleryCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerFinalCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerSelectionExtraCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerTailCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerPartialCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerDatabaseRangeCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerTableInsertCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerObjectCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerObjectCommands>().GetDialogCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerChangeCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerChangeCommands>().GetDialogCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerMacroCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerShellViewCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerZoomCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerCommentCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerAnalysisCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerToolCommands>().GetCommands());
        registry.RegisterRange(_services.GetRequiredService<LedgerStyleCommands>().GetCommands());

        return registry;
    }

    private void OpenDictionaries(CommandRegistry registry)
    {
        if (registry.Find(new CommandId("shell.tools.dictionaries")) is not { } command) return;
        _ = _services.GetRequiredService<PulseOffice.Shell.CommandExecutor>().ExecuteAsync(command, _services.GetRequiredService<PulseOffice.Shell.IDocumentWorkspace>().Context);
    }
}
