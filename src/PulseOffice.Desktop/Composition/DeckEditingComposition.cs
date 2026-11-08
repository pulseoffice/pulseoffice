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

internal static class DeckEditingComposition
{
    public static IServiceCollection AddDeckEditing(this IServiceCollection services)
    {
        services.AddSingleton<PulseOffice.Presentations.UI.IDeckDocumentService, PulseOffice.Presentations.UI.DeckDocumentService>();
        services.AddSingleton<PulseOffice.Presentations.UI.IActiveDeckContext, PulseOffice.Presentations.UI.ActiveDeckContext>();
        services.AddSingleton<PulseOffice.Shell.IDocumentStateWitness, PulseOffice.Presentations.UI.DeckStateWitness>();
        services.AddSingleton<PulseOffice.Presentations.UI.IDeckMediaFiles, PulseOffice.Presentations.UI.DeckMediaFiles>();
        services.AddSingleton<PulseOffice.Presentations.UI.IShowMediaPlayer, PulseOffice.Presentations.UI.ShowMediaPlayer>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckCommands>();
        services.AddSingleton<PulseOffice.Text.Rich.IRichTextFormatter, PulseOffice.Text.Rich.RichTextFormatter>();
        services.AddSingleton<PulseOffice.Text.Rich.IRichTextInsertion, PulseOffice.Text.Rich.RichTextInsertion>();
        services.AddSingleton<PulseOffice.Presentations.UI.IDeckRichTextTransfer, PulseOffice.Presentations.UI.DeckRichTextTransfer>();
        services.AddSingleton<PulseOffice.Text.Rich.ITextFrameResize, PulseOffice.Text.Rich.TextFrameResize>();
        services.AddSingleton<PulseOffice.Text.Rich.IListLabels>(_ => new PulseOffice.Text.Rich.ListLabels());
        services.AddSingleton<PulseOffice.UI.Controls.Text.Rich.ITextFrameMeasure, PulseOffice.UI.Controls.Text.Rich.TextFrameMeasure>();
        services.AddSingleton<PulseOffice.Presentations.UI.Rendering.ITextFrameFitting, PulseOffice.Presentations.UI.Rendering.TextFrameFitting>();
        services.AddSingleton<PulseOffice.Text.Rich.ICaretRowNavigation, PulseOffice.Text.Rich.CaretRowNavigation>();
        services.AddSingleton<PulseOffice.Text.Input.IPreeditComposer, PulseOffice.Text.Input.PreeditComposer>();
        services.AddSingleton<PulseOffice.UI.Controls.Text.ITypingAutoCorrection, PulseOffice.UI.Controls.Text.TypingAutoCorrection>();
        services.AddSingleton(provider => new PulseOffice.UI.Controls.Text.Editing.InlineEditingServices(
            provider.GetRequiredService<PulseOffice.Text.Rich.ICaretRowNavigation>(),
            provider.GetRequiredService<PulseOffice.Text.Input.IPreeditComposer>(),
            provider.GetRequiredService<PulseOffice.UI.Controls.Text.ITypingAutoCorrection>()));
        services.AddSingleton<PulseOffice.Presentations.ISlideTextHyphenation, PulseOffice.Presentations.SlideTextHyphenation>();
        services.AddSingleton<PulseOffice.Presentations.UI.IDeckFileInsertion, PulseOffice.Presentations.UI.DeckFileInsertion>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckEditCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.IDeckTextInsertion, PulseOffice.Presentations.UI.DeckTextInsertion>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckFieldCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckCharacterCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckFormatCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckRibbonCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.IDeckTextFormats, PulseOffice.Presentations.UI.DeckTextFormats>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckTextDialogs>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckTextDialogCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckTextFrameCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckZoomCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckSlideSetupCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.ObjectDialogs.DeckObjectDialogCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.ObjectDialogs.IDeckObjectEditing>(provider =>
            provider.GetRequiredService<PulseOffice.Presentations.UI.ObjectDialogs.DeckObjectDialogCommands>());
        services.AddSingleton<PulseOffice.Presentations.IFreeAreaPlacement, PulseOffice.Presentations.FreeAreaPlacement>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckInsertCommands>();
        services.AddSingleton<PulseOffice.Presentations.IDeckConnectors, PulseOffice.Presentations.DeckConnectors>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckConnectorCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckSlideCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckNavigationCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckFormControlCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.IDeckDrawingState, PulseOffice.Presentations.UI.DeckDrawingState>();
        services.AddSingleton<PulseOffice.Presentations.ICanvasContextChooser, PulseOffice.Presentations.CanvasContextChooser>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckDrawCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckImageCommands>();
        services.AddSingleton<PulseOffice.Exchange.IExchangeParticipant, PulseOffice.Presentations.UI.Exchange.DeckExchange>();
        services.TryAddSingleton<PulseOffice.UI.Embedding.IContentPainter, PulseOffice.UI.Embedding.StandardContentPainters>();
        services.TryAddSingleton<PulseOffice.UI.Embedding.ISheetObjectFactory, PulseOffice.UI.Embedding.SheetObjectFactory>();
        services.AddSingleton<PulseOffice.Presentations.UI.Exchange.IDeckSelectionPackages, PulseOffice.Presentations.UI.Exchange.DeckSelectionPackages>();
        services.AddSingleton<PulseOffice.Presentations.UI.Exchange.IDeckPortableTextBox, PulseOffice.Presentations.UI.Exchange.DeckPortableTextBox>();
        services.AddSingleton<PulseOffice.Presentations.UI.Rendering.IDeckSelectionPicture>(s => new PulseOffice.Presentations.UI.Rendering.DeckSelectionPicture(
            new PulseOffice.Presentations.UI.SlideRenderer(s.GetRequiredService<PulseOffice.UI.Controls.Shapes.IDrawingObjectRenderer>(), s.GetRequiredService<PulseOffice.Localization.ITextCatalog>(), s.GetRequiredService<PulseOffice.UI.Controls.Shapes.IShapeBrushes>())));
        services.AddSingleton<PulseOffice.Presentations.UI.DeckClipboardCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.Scripting.DeckScriptHost>();
        services.AddSingleton<PulseOffice.Presentations.UI.Scripting.DeckFacade>();
        services.AddSingleton<PulseOffice.Presentations.UI.Scripting.DeckScriptGlobals>();
        services.AddSingleton<PulseOffice.Scripting.IModuleScriptGlobals>(provider => provider.GetRequiredService<PulseOffice.Presentations.UI.Scripting.DeckScriptGlobals>());
        services.AddSingleton<PulseOffice.Scripting.Editor.IScriptApiSection, PulseOffice.Presentations.UI.Scripting.DeckScriptApi>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckMacroCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.IControlTabOrder, PulseOffice.Presentations.UI.ControlTabOrder>();
        services.AddSingleton<PulseOffice.Presentations.UI.ThemeMasterLookup>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckFormCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckMacroEventsSource>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckFindCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckSpellingCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckViewCommands>();
        services.AddSingleton<PulseOffice.Presentations.IDeckGrouping, PulseOffice.Presentations.DeckGrouping>();
        services.AddSingleton<PulseOffice.Presentations.ISlideGroupUnits, PulseOffice.Presentations.SlideGroupUnits>();
        services.AddSingleton<PulseOffice.Presentations.ISlideGroupEditing, PulseOffice.Presentations.SlideGroupEditing>();
        services.AddSingleton<PulseOffice.Presentations.ISlideGroupDecomposition, PulseOffice.Presentations.SlideGroupDecomposition>();
        services.AddSingleton<PulseOffice.Drawing.Layout.IBoxDistribution, PulseOffice.Drawing.Layout.BoxDistribution>();
        services.AddSingleton<PulseOffice.UI.Controls.Objects.IRasterColorAdjust, PulseOffice.UI.Controls.Objects.RasterColorAdjust>();
        services.AddSingleton<PulseOffice.UI.Controls.Objects.IColorCorrectionPresets, PulseOffice.UI.Controls.Objects.ColorCorrectionPresets>();
        services.AddSingleton<PulseOffice.Drawing.Freeform.IShapeConversion>(_ => new PulseOffice.Drawing.Freeform.ShapeConversion());
        services.AddSingleton<PulseOffice.Presentations.INativeAnimationPresetTable, PulseOffice.Presentations.NativeAnimationPresetTable>();
        services.AddSingleton<PulseOffice.Presentations.IAnimationPresetRegistry, PulseOffice.Presentations.AnimationPresetRegistry>();
        services.AddSingleton<PulseOffice.Drawing.Freeform.IPathEditing, PulseOffice.Drawing.Freeform.PathEditing>();
        services.AddSingleton<PulseOffice.UI.Controls.Objects.IImageLinks, PulseOffice.UI.Controls.Objects.ImageLinks>();
        services.AddSingleton<PulseOffice.Presentations.UI.IDeckLinkResolver, PulseOffice.Presentations.UI.DeckLinkResolver>();
        services.AddSingleton<PulseOffice.Platform.IExecutableFileTypes, PulseOffice.Platform.ExecutableFileTypes>();
        services.AddSingleton<PulseOffice.UI.Controls.Objects.IEmbeddedFileLauncher, PulseOffice.UI.Controls.Objects.EmbeddedFileLauncher>();
        services.AddSingleton<PulseOffice.UI.Controls.Objects.IMediaPlayer, PulseOffice.UI.Controls.Objects.MediaPlayer>();
        services.AddSingleton<PulseOffice.Drawing.Assets.Gif.IGifCodec, PulseOffice.Drawing.Assets.Gif.GifCodec>();
        services.AddSingleton<PulseOffice.Presentations.Remote.IRemoteSession, PulseOffice.Presentations.Remote.RemoteSession>();
        services.AddSingleton<PulseOffice.Presentations.Remote.IRemoteAttemptThrottle>(_ => new PulseOffice.Presentations.Remote.RemoteAttemptThrottle(TimeProvider.System));
        services.AddSingleton<PulseOffice.Presentations.Remote.IRemotePage, PulseOffice.Presentations.UI.Remote.RemotePage>();
        services.AddSingleton<PulseOffice.Presentations.Remote.IRemoteServer>(provider => new PulseOffice.Presentations.Remote.RemoteServer(
            provider.GetRequiredService<PulseOffice.Presentations.Remote.IRemoteSession>(),
            action => Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(action).GetTask(),
            provider.GetRequiredService<PulseOffice.Presentations.Remote.IRemotePage>(),
            provider.GetRequiredService<PulseOffice.Presentations.Remote.IRemoteAttemptThrottle>()));
        services.AddSingleton<PulseOffice.Presentations.UI.Remote.DeckRemoteCommands>();
        services.AddSingleton<PulseOffice.UI.Controls.Shapes.Fontwork.IFontworkLooks, PulseOffice.UI.Controls.Shapes.Fontwork.FontworkLooks>();
        services.AddSingleton<PulseOffice.UI.Controls.Shapes.IFontworkPrompt, PulseOffice.UI.Controls.Shapes.FontworkPrompt>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckArrangeCommands>();
        services.TryAddSingleton<PulseOffice.Drawing.Shapes.IContentFlipping, PulseOffice.Drawing.Shapes.ContentFlipping>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckAnimatedImageCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckMediaCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckLinkCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckFormFieldCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckFontworkCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckMotionCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckStyleCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckPanelCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckExtraInsertCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckMasterCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckConvertCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.IDeckInteractionForm, PulseOffice.Presentations.UI.DeckInteractionForm>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckObjectCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.IDeckVideoFrameSourceFactory, PulseOffice.Presentations.UI.DeckVideoFrameSourceFactory>();
        services.AddSingleton<PulseOffice.Presentations.UI.IVideoSlideDuration, PulseOffice.Presentations.UI.VideoSlideDuration>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckExportCommands>();
        services.AddSingleton<PulseOffice.Document.Embedding.ITableSizing, PulseOffice.Document.Embedding.OptimalRowSizing>();
        services.AddSingleton<PulseOffice.Document.Embedding.ITableSizing, PulseOffice.Document.Embedding.MinimalRowSizing>();
        services.AddSingleton<PulseOffice.Document.Embedding.ITableSizing, PulseOffice.Document.Embedding.DistributedRowSizing>();
        services.AddSingleton<PulseOffice.Document.Embedding.ITableSizing, PulseOffice.Document.Embedding.OptimalColumnSizing>();
        services.AddSingleton<PulseOffice.Document.Embedding.ITableSizing, PulseOffice.Document.Embedding.MinimalColumnSizing>();
        services.AddSingleton<PulseOffice.Document.Embedding.ITableSizing, PulseOffice.Document.Embedding.DistributedColumnSizing>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckTableCommands>();
        services.AddSingleton<PulseOffice.Presentations.UI.IDeckTableLooks, PulseOffice.Presentations.UI.DeckTableLooks>();
        services.AddSingleton<PulseOffice.Presentations.IDeckTextRedaction, PulseOffice.Presentations.DeckTextRedaction>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckToolCommands>();
        services.AddSingleton<PulseOffice.Shell.IWorkspaceOpener>(s =>
            (PulseOffice.Shell.IWorkspaceOpener)s.GetRequiredService<PulseOffice.Presentations.UI.IDeckDocumentService>());
        services.AddSingleton<PulseOffice.Shell.IDocumentFileService>(s =>
            (PulseOffice.Shell.IDocumentFileService)s.GetRequiredService<PulseOffice.Presentations.UI.IDeckDocumentService>());
        services.AddSingleton<PulseOffice.Shell.IDocumentFileService>(s =>
            (PulseOffice.Shell.IDocumentFileService)s.GetRequiredService<ISpreadsheetDocumentService>());
        services.AddSingleton<PulseOffice.Shell.IDocumentFileServices, PulseOffice.Shell.DocumentFileServices>();
        services.AddSingleton<PulseOffice.Shell.IWorkspaceOpener>(s =>
            (PulseOffice.Shell.IWorkspaceOpener)s.GetRequiredService<ISpreadsheetDocumentService>());
        services.AddSingleton<PulseOffice.Shell.IDocumentOpenRouter, PulseOffice.Shell.DocumentOpenRouter>();
        services.AddSingleton<PulseOffice.Shell.IUnattendedOpening, PulseOffice.Shell.UnattendedOpening>();
        services.AddSingleton<PulseOffice.Shell.IDocumentArrivals, PulseOffice.Shell.DocumentArrivals>();
        services.AddSingleton<PulseOffice.Shell.IExchangeDragDrop, PulseOffice.Shell.ExchangeDragDrop>();
        services.AddSingleton<PulseOffice.Shell.IHtmlTableGrids, PulseOffice.Shell.HtmlTableGrids>();

        return services;
    }
}
