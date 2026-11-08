using PulseOffice.Formats.Intake;
using PulseOffice.UI.Controls.Zoom;
using PulseOffice.UI.Controls.Fonts;
using PulseOffice.Documents.UI.Shell.Snapshot;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Layout.Measurement;
using PulseOffice.Documents.Layout.Measurement.OpenType;
using PulseOffice.Documents.UI.Canvas;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Documents.UI.Fonts;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Documents.UI.Shell;
using PulseOffice.Shell;
using PulseOffice.Typography.OpenType.Metrics;
using PulseOffice.UI.Controls.Text.Rich;

namespace PulseOffice.Documents.UI;

public static class FolioServiceCollectionExtensions
{
    public static IServiceCollection AddFolioMeasurement(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IFontMetricsReader, FontMetricsReader>(_ => new FontMetricsReader());
        services.TryAddSingleton<IFontSubstitutes, MetricCompatibleSubstitutes>();
        services.AddSingleton<IFolioFaces, PlatformFaces>();
        services.AddSingleton<IFontFileLocator, FolioFontLocator>();
        services.AddSingleton<ITextMeasurer>(provider => new OpenTypeMeasurer(
            provider.GetRequiredService<IFontFileLocator>(), new HorizontalHeaderMetrics(), new ExactAdvances(), new FixedPitchMeasurer()));
        services.TryAddSingleton<PulseOffice.Typography.Shaping.TextShaperOptions>();
        services.TryAddSingleton<PulseOffice.Typography.Shaping.HarfBuzzTextShaper>();
        services.TryAddSingleton<PulseOffice.Typography.Shaping.ITextShaper>(provider => new PulseOffice.Typography.Shaping.CachedTextShaper(provider.GetRequiredService<PulseOffice.Typography.Shaping.HarfBuzzTextShaper>()));
        services.TryAddSingleton<PulseOffice.Typography.Shaping.IComplexScriptSegmenter, PulseOffice.Typography.Shaping.ComplexScriptSegmenter>();
        services.AddSingleton<IFaceGlyphMapping, FaceGlyphMapping>();
        services.AddSingleton<IGlyphRunPainter, FontFileRunPainter>();
        services.AddSingleton<IGlyphRunPainter, FormattedRunPainter>();
        return services;
    }

    public static IServiceCollection AddFolioUI(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<PulseOffice.Text.Rich.ICaretWord, PulseOffice.Text.Rich.CaretWord>();
        services.Replace(ServiceDescriptor.Singleton<PulseOffice.Documents.Controls.IContentControlCatalog, PulseOffice.Documents.Controls.RevisionCachedContentControlCatalog>());
        services.AddSingleton<IRunPainting, RunPainting>();
        services.AddSingleton<IPageRenderer, PageRenderer>();
        services.AddSingleton<IBulletPictureObjects, BulletPictureObjects>();
        services.AddSingleton<IFolioChangeBars, FolioChangeBars>();
        services.AddSingleton<IFolioCommentMargin, FolioCommentMargin>();
        services.AddSingleton<IFolioCommentSheet, FolioCommentSheet>();
        services.AddSingleton<IFolioViewMarks, FolioViewMarks>();
        services.AddSingleton<IFolioViewLayout, FolioViewLayout>();
        services.AddSingleton<IFolioDrawingTools, FolioDrawingTools>();
        services.AddSingleton<IFolioDrawingPlacement, FolioDrawingPlacement>();
        services.TryAddSingleton<PulseOffice.Drawing.Freeform.IPathAuthoring, PulseOffice.Drawing.Freeform.PathAuthoring>();
        services.TryAddSingleton<PulseOffice.UI.Controls.Shapes.IAdjustHandleChrome>(_ => new PulseOffice.UI.Controls.Shapes.AdjustHandleChrome());
        services.TryAddSingleton<PulseOffice.Drawing.Freeform.IPathNormalizer, PulseOffice.Drawing.Freeform.PathNormalizer>();
        services.TryAddSingleton<PulseOffice.Drawing.Shapes.IConnectorFramer, PulseOffice.Drawing.Shapes.ConnectorFramer>();
        services.TryAddSingleton<PulseOffice.Drawing.Shapes.IGroupChildLinks, PulseOffice.Drawing.Shapes.GroupChildLinks>();
        services.TryAddSingleton<PulseOffice.UI.Controls.Links.ILinkFollower, PulseOffice.UI.Controls.Links.LinkFollower>();
        services.AddSingleton<IFolioLineNumberMarks, FolioLineNumberMarks>();
        services.AddSingleton<IFolioSpotlight, FolioSpotlight>();
        services.AddSingleton<IFolioFieldNames, FolioFieldNames>();
        services.TryAddSingleton<PulseOffice.UI.Controls.Shapes.IObjectNudgeKeys, PulseOffice.UI.Controls.Shapes.ObjectNudgeKeys>();
        services.TryAddSingleton<PulseOffice.UI.Controls.Rulers.IParagraphRuler, PulseOffice.UI.Controls.Rulers.ParagraphRuler>();
        services.AddSingleton<FolioRulerServices>();
        services.AddSingleton<IPageSurfaceFactory, PageSurfaceFactory>();
        services.AddSingleton<IFolioPrintMarks, FolioPrintMarks>();
        services.AddSingleton<IFolioPrintables, FolioPrintables>();
        services.AddSingleton<IPageDesk, PageDesk>();
        services.AddSingleton<ILineCells, LineCells>();
        services.AddSingleton<ILayoutLines, LayoutLines>();
        services.AddSingleton<IParagraphOrder, ParagraphOrder>();
        services.TryAddSingleton<PulseOffice.Text.Rich.ITextClusters, PulseOffice.Text.Rich.TextClusters>();
        services.TryAddSingleton<PulseOffice.Text.Rich.IWordMotion, PulseOffice.Text.Rich.WordMotion>();
        services.AddSingleton<ICaretNavigator, CaretNavigator>();
        services.AddSingleton<IPreeditLayout, PreeditLayout>();
        services.AddSingleton<IFolioProofing, FolioProofing>();
        services.AddSingleton<IFolioLanguages, FolioLanguages>();
        services.AddDocumentIntake();
        services.AddSingleton<IFolioRichTransfer, FolioRichTransfer>();
        services.AddSingleton<PulseOffice.Exchange.IExchangeParticipant, Exchange.FolioExchange>();
        PulseOffice.Exchange.ExchangeServiceCollectionExtensions.AddObjectExchange(services);
        services.TryAddSingleton<PulseOffice.UI.Controls.Exchange.IExchangePasteChooser, PulseOffice.UI.Controls.Exchange.ExchangePasteChooser>();
        services.AddSingleton<IFolioKeyMap, FolioKeyMap>();
        services.AddSingleton<IFolioEditingKeys, FolioEditingKeys>();
        services.AddSingleton<SpellingWaves>();
        services.AddSingleton<IActiveFolioContext, ActiveFolioContext>();
        services.TryAddSingleton<PulseOffice.Document.Commands.UndoToStatePoint>();
        services.TryAddSingleton<PulseOffice.Document.Commands.RedoToStatePoint>();
        services.TryAddSingleton<PulseOffice.Document.Commands.IHistoryReversals, PulseOffice.Document.Commands.HistoryReversals>();
        services.AddSingleton<PulseOffice.UI.Commands.ICommandEffectGuard, Editing.FolioProtectedContentGuard>();
        services.AddSingleton<PulseOffice.Shell.IDocumentStateWitness, Editing.FolioStateWitness>();
        services.AddSingleton<IContentControlPlaceholders, ContentControlPlaceholders>();
        services.AddSingleton<FolioControlServices>();
        services.AddSingleton<FolioEditingServices>();
        services.TryAddSingleton<PulseOffice.Text.Spelling.IWordCompletion, PulseOffice.Text.Spelling.WordCompletion>();
        services.AddSingleton<IFolioTypingAssist, FolioTypingAssist>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IFolioFieldRefresh, FolioFieldRefresh>();
        services.AddSingleton<PulseOffice.Documents.Fields.IFieldWords, FolioFieldWords>();
        services.AddSingleton<IFolioNavigation, FolioNavigation>();
        services.AddSingleton<IFolioLinks, FolioLinks>();
        services.AddSingleton<IFolioPageJump, FolioPageJump>();
        services.AddSingleton<IFolioPageStatus, FolioPageStatus>();
        services.AddSingleton<FolioNavigatorSource>();
        services.AddSingleton<FolioFieldCatalog>();
        services.AddSingleton<Fields.IFolioFieldInsertion, Fields.FolioFieldInsertion>();
        services.AddSingleton(provider => new Fields.FolioFieldsServices(
            provider.GetRequiredService<FolioFieldCatalog>(),
            [
                new Fields.ClockFieldOptions(),
                new Fields.NamedFieldOptions(provider.GetRequiredService<PulseOffice.Documents.Fields.IMergeRecords>()),
                new Fields.VariableSetFieldOptions(),
                new Fields.UserFieldOptions(),
                new Fields.ConditionFieldOptions(),
                new Fields.PlainFieldOptions(),
            ],
            provider.GetRequiredService<PulseOffice.Documents.Fields.IFieldUpdater>(),
            provider.GetRequiredService<IFolioFieldRefresh>(),
            provider.GetRequiredService<PulseOffice.Documents.Fields.IReferenceTargets>(),
            provider.GetRequiredService<PulseOffice.Documents.Fields.IBookmarkCatalog>(),
            provider.GetRequiredService<PulseOffice.Documents.Fields.IDocumentReadingOrder>(),
            provider.GetRequiredService<Fields.IFolioFieldInsertion>(),
            provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<Fields.IFolioFieldsDialog, Fields.FolioFieldsDialog>();
        services.AddSingleton<Indexes.FolioIndexPresets>();
        services.AddSingleton<Indexes.IFolioIndexEntries, Indexes.FolioIndexEntries>();
        services.AddSingleton<ITableTextSampler, TableTextSampler>();
        services.AddSingleton<Shell.IFolioMergeLetters, Shell.FolioMergeLetters>();
        services.AddSingleton<Shell.IFolioMergeWizard, Shell.FolioMergeWizard>();
        services.AddSingleton<Indexes.IFolioIndexGallery, Indexes.FolioIndexGallery>();
        services.AddSingleton<FolioFieldCommands>();
        services.AddSingleton<FolioObjectLinkCommands>();
        services.AddSingleton<PulseOffice.Documents.UI.Editing.IFolioObjectLinks, PulseOffice.Documents.UI.Editing.FolioObjectLinks>();
        services.AddSingleton<FolioIndexCommands>();
        services.AddSingleton<FolioTableServices>();
        services.AddSingleton<FolioCanvasServices>();
        services.AddSingleton<IFolioEditRefusals, FolioEditRefusals>();
        services.AddTransient<FolioViewServices>();
        services.AddSingleton<Func<FolioViewServices>>(provider => provider.GetRequiredService<FolioViewServices>);
        services.TryAddSingleton<PulseOffice.Localization.IInterfaceCulture>(provider =>
            new PulseOffice.Localization.InterfaceCulture(provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>()));
        services.AddSingleton<IFolioDocumentFactory, FolioDocumentFactory>();
        services.AddSingleton<IDocumentOperations, FolioDocumentOperations>();
        services.AddSingleton<FolioModuleFormats>();
        services.AddSingleton<IFolioDocumentService, FolioDocumentService>();
        services.AddSingleton<IWorkspaceOpener>(provider => provider.GetRequiredService<IFolioDocumentService>());
        services.AddSingleton<IDocumentFileService>(provider => provider.GetRequiredService<IFolioDocumentService>());
        services.AddSingleton<PulseOffice.Typography.Embedding.IUsedFontCollector, FolioFontCollector>();
        services.AddSingleton<PulseOffice.Documents.UI.Shell.Snapshot.ISnapshotTypeCatalog, PulseOffice.Documents.UI.Shell.Snapshot.SnapshotTypeCatalog>();
        services.AddSingleton<IFolioSnapshotValidator, FolioSnapshotValidator>();
        services.AddSingleton<IFolioSnapshotCodec, FolioSnapshotCodec>();
        services.AddSingleton<IDocumentSnapshotProvider, FolioSnapshotProvider>();
        services.AddSingleton<FolioEditCommands>();
        services.TryAddSingleton<IZoomLadder, ZoomLadder>();
        services.TryAddSingleton<IZoomChoice, ZoomChoice>();
        services.AddSingleton<FolioZoomCommands>();
        services.AddSingleton<IFolioPageStyles, FolioPageStyles>();
        services.AddSingleton<IFolioForms, FolioForms>();
        services.TryAddSingleton<PulseOffice.UI.Controls.Galleries.IGalleryNavigator, PulseOffice.UI.Controls.Galleries.GalleryNavigator>();
        services.TryAddSingleton<PulseOffice.UI.Controls.Themes.IThemePaletteGallery, PulseOffice.UI.Controls.Themes.ThemePaletteGallery>();
        services.TryAddSingleton<PulseOffice.UI.Controls.Sorting.ISortLevelsDialog, PulseOffice.UI.Controls.Sorting.SortLevelsDialog>();
        services.AddSingleton<IFolioCharacterTabs, FolioCharacterTabs>();
        services.AddSingleton<IFolioCharacterDialog, FolioCharacterDialog>();
        services.AddSingleton<IFolioParagraphTabs, FolioParagraphTabs>();
        services.AddSingleton<IFolioParagraphDialog, FolioParagraphDialog>();
        services.AddSingleton<FolioDialogCommands>();
        services.AddSingleton<IFolioListForms, FolioListForms>();
        services.AddSingleton<FolioPageServices>();
        services.AddSingleton<IFolioPageTabs, FolioPageTabs>();
        services.AddSingleton<IFolioPageDialog, FolioPageDialog>();
        services.AddSingleton<StyleCatalog.IFolioStyleNames, StyleCatalog.FolioStyleNames>();
        services.AddSingleton<StyleCatalog.IFolioStyleDialogs, StyleCatalog.FolioStyleDialogs>();
        services.AddSingleton<StyleCatalog.IFolioStyleActions, StyleCatalog.FolioStyleActions>();
        services.AddSingleton<StyleCatalog.FolioStylesSource>();
        services.AddSingleton<FolioStyleCommands>();
        services.AddSingleton<FolioSectionCommands>();
        services.AddSingleton<FolioReviewCommands>();
        services.AddSingleton<FolioExportCommands>();
        services.AddSingleton<IMergeDataReader, MergeDataReader>();
        services.AddSingleton<IMergeFileNames, MergeFileNames>();
        services.AddSingleton<IFolioMergeOutput, FolioMergeOutput>();
        services.AddSingleton<FolioMailMergeCommands>();
        services.TryAddSingleton<PulseOffice.Formats.ILinkedFiles, PulseOffice.Formats.LinkedFiles>();
        services.AddSingleton<FolioMasterCommands>();
        services.AddSingleton<FolioToolViews>();
        services.AddSingleton<FolioToolServices>();
        services.AddSingleton<FolioToolCommands>();
        services.AddSingleton<FolioClipboardCommands>();
        services.AddSingleton<FolioBreakCommands>();
        services.AddSingleton<FolioNavigationCommands>();
        services.AddSingleton<FolioTextToolCommands>();
        services.AddSingleton<FolioShapeServices>();
        services.AddSingleton<FolioShapeCommands>();
        services.AddSingleton<FolioSendServices>();
        services.AddSingleton<FolioSendCommands>();
        services.AddSingleton<IFolioNumberRecognition, FolioNumberRecognition>();
        services.AddSingleton<FolioCellCommands>();
        services.AddSingleton<IFolioAccessibilityMonitor, FolioAccessibilityMonitor>();
        services.AddSingleton<FolioAccessibilityCommands>();
        services.TryAddSingleton<PulseOffice.Platform.IAtomicFileWriter, PulseOffice.Platform.AtomicFileWriter>();
        services.AddSingleton<IAutoTextLibrary, AutoTextLibrary>();
        services.AddSingleton<FolioAutoTextCommands>();
        services.AddSingleton<FolioDrawCommands>();
        services.AddSingleton<FolioViewCommandServices>();
        services.AddSingleton<FolioViewCommands>();
        services.AddSingleton<Scripting.FolioScriptHost>();
        services.AddSingleton<Scripting.FolioScriptServices>();
        services.AddSingleton<Scripting.FolioFacade>();
        services.AddSingleton<Scripting.FolioScriptGlobals>();
        services.AddSingleton<PulseOffice.Scripting.IModuleScriptGlobals>(provider => provider.GetRequiredService<Scripting.FolioScriptGlobals>());
        services.AddSingleton<PulseOffice.Scripting.Editor.IScriptApiSection, Scripting.FolioScriptApi>();
        services.AddSingleton<Scripting.FolioMacroEventsSource>();
        services.AddSingleton<Scripting.FolioMacroCommands>();
        services.TryAddSingleton<PulseOffice.Scripting.IScriptExecutionPermissions, PulseOffice.Scripting.ScriptExecutionPermissions>();
        services.AddSingleton<Scripting.IFolioFormEvents, Scripting.FolioFormEvents>();
        services.AddSingleton<FolioSpellingCommands>();
        services.AddSingleton<IFolioReviewText, FolioReviewText>();
        services.AddSingleton<FolioReviewSource>();
        services.AddSingleton<FolioTitlePageCommands>();
        services.AddSingleton<FolioListCommands>();
        services.AddSingleton<FolioCharacterCommands>();
        services.AddSingleton<FolioParagraphCommands>();
        services.AddSingleton<FolioRibbonCommands>();
        services.AddSingleton<FolioInsertCommands>();
        services.AddSingleton<FolioPageCommands>();
        services.AddSingleton<FolioFindCommands>();
        services.AddSingleton<FolioTableCommands>();
        services.AddSingleton<IFolioTableLooks, FolioTableLooks>();
        services.AddSingleton<FolioTableEditing>();
        services.AddSingleton<IFolioTableDialogs, FolioTableDialogs>();
        services.AddSingleton<FolioObjectCommands>();
        services.TryAddSingleton<PulseOffice.UI.Controls.Services.IContextMenuKey, PulseOffice.UI.Controls.Services.ContextMenuKey>();
        services.AddSingleton<IFolioContextMenus, FolioContextMenus>();
        services.AddSingleton<IFolioCaretSpeech, FolioCaretSpeech>();
        services.AddSingleton<IFolioObjectCycle, FolioObjectCycle>();
        services.AddSingleton<IFolioObjectHits, FolioObjectHits>();
        services.AddSingleton<IFolioObjectMover, FolioObjectMover>();
        services.TryAddSingleton<PulseOffice.UI.Controls.Shapes.IDrawingObjectRenderer>(_ => new PulseOffice.UI.Controls.Shapes.DrawingObjectRenderer());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<PulseOffice.UI.Controls.Services.IObjectPicturePainter, PulseOffice.UI.Controls.Services.DrawingPicturePainter>());
        services.TryAddSingleton<PulseOffice.UI.Controls.Formulas.IFormulaObjectRenderer, PulseOffice.UI.Controls.Formulas.FormulaObjectRenderer>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<PulseOffice.UI.Controls.Services.IObjectPicturePainter, PulseOffice.UI.Controls.Services.FormulaPicturePainter>());
        services.TryAddSingleton<PulseOffice.UI.Controls.Formulas.IFormulaRenderer>(_ => new PulseOffice.UI.Controls.Formulas.FormulaRenderer());
        services.TryAddSingleton<PulseOffice.Formulas.IFormulaParser>(_ => new PulseOffice.Formulas.FormulaParser());
        services.TryAddSingleton<PulseOffice.UI.Controls.Services.IEmbeddedObjectPicture, PulseOffice.UI.Controls.Services.EmbeddedObjectPicture>();
        services.TryAddSingleton<PulseOffice.UI.Controls.Objects.IBitmapPixels, PulseOffice.UI.Controls.Objects.BitmapPixels>();
        services.TryAddSingleton<PulseOffice.UI.Embedding.IContentPainter, PulseOffice.UI.Embedding.StandardContentPainters>();
        services.TryAddSingleton<PulseOffice.UI.Embedding.ISheetObjectFactory, PulseOffice.UI.Embedding.SheetObjectFactory>();
        services.TryAddSingleton<PulseOffice.Drawing.Printing.IOriginalImageSources, PulseOffice.Drawing.Printing.OriginalImageSources>();
        services.AddSingleton<IEmbeddedRasterizer, EmbeddedRasterizer>();
        services.TryAddSingleton<PulseOffice.Drawing.Printing.IObjectPathBuilder>(_ => new PulseOffice.Drawing.Printing.ObjectPathBuilder());
        services.TryAddSingleton<PulseOffice.Drawing.Assets.IImageExtents, PulseOffice.Drawing.Assets.ImageExtents>();
        services.AddSingleton<IFolioImages, FolioImages>();
        services.TryAddSingleton<PulseOffice.Drawing.Shapes.IContentFlipping, PulseOffice.Drawing.Shapes.ContentFlipping>();
        services.TryAddSingleton<PulseOffice.UI.Controls.Objects.IImageContourTracer, PulseOffice.UI.Controls.Objects.ImageContourTracer>();
        services.AddSingleton<IFolioObjectEditing, FolioObjectEditing>();
        services.AddSingleton<IFolioObjectDialogs, FolioObjectDialogs>();
        services.AddSingleton<FolioObjectInsertCommands>();
        services.AddSingleton<FolioImageCommands>();
        services.AddSingleton<FolioChartCommands>();
        services.AddSingleton<IFolioContentControls, FolioContentControls>();
        services.AddSingleton<FolioContentControlCommands>();
        services.TryAddSingleton<PulseOffice.Drawing.Shapes.IObjectManipulator, PulseOffice.Drawing.Shapes.ObjectManipulator>();
        return services;
    }
}
