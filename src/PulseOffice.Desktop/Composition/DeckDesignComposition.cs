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

internal static class DeckDesignComposition
{
    public static IServiceCollection AddDeckDesign(this IServiceCollection services, IUserDataPaths paths)
    {
        services.AddSingleton<PulseOffice.Presentations.ISlideFactory, PulseOffice.Presentations.SlideFactory>();
        services.AddSingleton<PulseOffice.Presentations.IOutlineDeckBuilder, PulseOffice.Presentations.OutlineDeckBuilder>();
        services.AddSingleton(new PulseOffice.Documents.UI.Commands.AutoTextFolder(paths.Configuration("autotext")));
        services.AddSingleton<PulseOffice.Shell.IOutlinePresentation, PulseOffice.Presentations.UI.DeckOutlinePresentation>();
        services.AddSingleton<PulseOffice.Presentations.ISlideOutlining, PulseOffice.Presentations.SlideOutlining>();
        services.AddSingleton<PulseOffice.Presentations.IThemeCatalog, PulseOffice.Presentations.ThemeCatalog>();
        services.AddSingleton<PulseOffice.Presentations.IThemeChoices, PulseOffice.Presentations.ThemeChoices>();
        services.AddSingleton<PulseOffice.Presentations.ITextFrameDefaults, PulseOffice.Presentations.TextFrameDefaults>();
        services.AddSingleton<PulseOffice.Presentations.Decorations.IThemeDecoration, PulseOffice.Presentations.Decorations.SideBandDecoration>();
        services.AddSingleton<PulseOffice.Presentations.Decorations.IThemeDecoration, PulseOffice.Presentations.Decorations.TopBarDecoration>();
        services.AddSingleton<PulseOffice.Presentations.Decorations.IThemeDecoration, PulseOffice.Presentations.Decorations.BottomStripesDecoration>();
        services.AddSingleton<PulseOffice.Presentations.Decorations.IThemeDecoration, PulseOffice.Presentations.Decorations.CornersDecoration>();
        services.AddSingleton<PulseOffice.Presentations.Decorations.IThemeDecoration, PulseOffice.Presentations.Decorations.ArcsDecoration>();
        services.AddSingleton<PulseOffice.Presentations.Decorations.IThemeDecoration, PulseOffice.Presentations.Decorations.WashDecoration>();
        services.AddSingleton<PulseOffice.Presentations.Decorations.IThemeDecoration, PulseOffice.Presentations.Decorations.FrameDecoration>();
        services.AddSingleton<PulseOffice.Presentations.Decorations.IThemeDecoration, PulseOffice.Presentations.Decorations.MosaicDecoration>();
        services.AddSingleton<PulseOffice.Presentations.Decorations.IThemeDecoration, PulseOffice.Presentations.Decorations.OrbsDecoration>();
        services.AddSingleton<PulseOffice.Presentations.Decorations.IThemeDecoration, PulseOffice.Presentations.Decorations.SliceDecoration>();
        services.AddSingleton<PulseOffice.Presentations.Decorations.IThemeDecorations, PulseOffice.Presentations.Decorations.ThemeDecorations>();
        services.AddSingleton<PulseOffice.Presentations.IMasterFactory, PulseOffice.Presentations.MasterFactory>();
        services.AddSingleton<PulseOffice.Presentations.UI.Themes.IThemeNames, PulseOffice.Presentations.UI.Themes.ThemeNames>();
        services.AddSingleton<PulseOffice.Presentations.UI.Themes.IThemePreviews>(provider => new PulseOffice.Presentations.UI.Themes.ThemePreviews(
            new PulseOffice.Presentations.UI.SlideRenderer(
                provider.GetRequiredService<PulseOffice.UI.Controls.Shapes.IDrawingObjectRenderer>(),
                provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>()),
            provider.GetRequiredService<PulseOffice.Presentations.ISlideFactory>()));
        services.AddSingleton<PulseOffice.UI.Controls.Ribbon.IRibbonThumbnailPainter, PulseOffice.Presentations.UI.Themes.ThemeThumbnailPainter>();
        services.AddSingleton<PulseOffice.Presentations.UI.Themes.DeckThemeCommands>();
        services.AddSingleton<PulseOffice.Presentations.ISlideLayoutCatalog, PulseOffice.Presentations.SlideLayoutCatalog>();
        services.AddSingleton<PulseOffice.Presentations.UI.IDeckChoiceNames, PulseOffice.Presentations.UI.DeckChoiceNames>();
        services.AddSingleton<PulseOffice.Presentations.UI.Design.ILayoutChoices, PulseOffice.Presentations.UI.Design.LayoutChoices>();
        services.AddSingleton<PulseOffice.Presentations.UI.Design.ILayoutPreviews>(provider => new PulseOffice.Presentations.UI.Design.LayoutPreviews(
            new PulseOffice.Presentations.UI.SlideRenderer(
                provider.GetRequiredService<PulseOffice.UI.Controls.Shapes.IDrawingObjectRenderer>(),
                provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>()),
            provider.GetRequiredService<PulseOffice.Presentations.ISlideFactory>()));
        services.AddSingleton<PulseOffice.UI.Controls.Ribbon.IRibbonThumbnailPainter, PulseOffice.Presentations.UI.Design.LayoutThumbnailPainter>();
        services.AddSingleton<PulseOffice.Presentations.UI.Design.DeckLayoutCommands>();
        services.AddSingleton(_ => new PulseOffice.Presentations.UI.TransitionStage());
        services.AddSingleton<PulseOffice.Presentations.ITransitionCatalog, PulseOffice.Presentations.TransitionCatalog>();
        services.AddSingleton<PulseOffice.Presentations.IAnimationTimeline, PulseOffice.Presentations.AnimationTimeline>();
        services.AddSingleton<PulseOffice.Presentations.IAnimationOrdering, PulseOffice.Presentations.AnimationOrdering>();
        services.AddSingleton<PulseOffice.Presentations.IAnimationPresetPreview>(_ => new PulseOffice.Presentations.AnimationPresetPreview());
        services.AddSingleton<PulseOffice.Presentations.UI.Motion.IAnimationLooks, PulseOffice.Presentations.UI.Motion.AnimationLooks>();
        services.AddSingleton<PulseOffice.Presentations.UI.Motion.IDeckElementNames, PulseOffice.Presentations.UI.Motion.DeckElementNames>();
        services.AddSingleton<PulseOffice.Presentations.UI.Motion.IDeckSlidePictures>(provider => new PulseOffice.Presentations.UI.Motion.DeckSlidePictures(
            new PulseOffice.Presentations.UI.SlideRenderer(
                provider.GetRequiredService<PulseOffice.UI.Controls.Shapes.IDrawingObjectRenderer>(),
                provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>())));
        services.AddSingleton(provider => new PulseOffice.Presentations.UI.Motion.TransitionEditorServices(
            provider.GetRequiredService<PulseOffice.Presentations.ITransitionCatalog>(),
            provider.GetRequiredService<PulseOffice.Presentations.UI.TransitionStage>(),
            provider.GetRequiredService<PulseOffice.UI.Controls.Fields.IMeasureUnits>(),
            provider.GetRequiredService<IIconCatalog>(),
            provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>(),
            provider.GetRequiredService<PulseOffice.UI.Controls.Galleries.IGalleryNavigator>(),
            provider.GetService<IFileDialogService>()));
        services.AddSingleton(provider => new PulseOffice.Presentations.UI.Motion.AnimationPaneServices(
            provider.GetRequiredService<PulseOffice.Presentations.IAnimationPresetRegistry>(),
            provider.GetRequiredService<PulseOffice.Presentations.IAnimationTimeline>(),
            provider.GetRequiredService<PulseOffice.Presentations.IAnimationOrdering>(),
            provider.GetRequiredService<PulseOffice.Presentations.IAnimationPresetPreview>(),
            provider.GetRequiredService<PulseOffice.Presentations.UI.Motion.IAnimationLooks>(),
            provider.GetRequiredService<PulseOffice.UI.Controls.Fields.IMeasureUnits>(),
            provider.GetRequiredService<IIconCatalog>(),
            provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>(),
            provider.GetRequiredService<PulseOffice.UI.Controls.Galleries.IGalleryNavigator>()));
        services.AddSingleton<PulseOffice.Presentations.UI.Motion.IDeckMotionEditors, PulseOffice.Presentations.UI.Motion.DeckMotionEditors>();
        services.AddSingleton<PulseOffice.Presentations.ISlideBackgrounds, PulseOffice.Presentations.SlideBackgrounds>();
        services.AddSingleton(provider => new PulseOffice.Presentations.UI.Design.ImageExportServices(
            provider.GetRequiredService<PulseOffice.Presentations.UI.Motion.IDeckSlidePictures>(),
            provider.GetRequiredService<PulseOffice.UI.Controls.Fields.IMeasureUnits>(),
            provider.GetRequiredService<IIconCatalog>(),
            provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>()));
        services.AddSingleton(provider => new PulseOffice.Presentations.UI.Design.PhotoAlbumServices(
            provider.GetRequiredService<PulseOffice.UI.Controls.Galleries.IGalleryNavigator>(),
            provider.GetRequiredService<IIconCatalog>(),
            provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>(),
            provider.GetRequiredService<PulseOffice.Presentations.ICustomShowEditing>()));
        services.AddSingleton<PulseOffice.Presentations.ICustomShowEditing, PulseOffice.Presentations.CustomShowEditing>();
        services.AddSingleton<PulseOffice.Presentations.UI.Design.ISlideCaptions, PulseOffice.Presentations.UI.Design.SlideCaptions>();
        services.AddSingleton(provider => new PulseOffice.Presentations.UI.Design.SlidePickerServices(
            provider.GetRequiredService<PulseOffice.Presentations.UI.Motion.IDeckSlidePictures>(),
            provider.GetRequiredService<PulseOffice.UI.Controls.Galleries.IGalleryNavigator>(),
            provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>()));
        services.AddSingleton(provider => new PulseOffice.Presentations.UI.Design.MasterGalleryServices(
            provider.GetRequiredService<PulseOffice.Presentations.UI.Themes.IThemePreviews>(),
            provider.GetRequiredService<PulseOffice.UI.Controls.Galleries.IGalleryNavigator>(),
            provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>()));
        services.AddSingleton(provider => new PulseOffice.Presentations.UI.Design.MasterElementsServices(
            provider.GetRequiredService<PulseOffice.Presentations.UI.Motion.IDeckSlidePictures>(),
            provider.GetRequiredService<PulseOffice.Presentations.ISlideFactory>(),
            provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>()));
        services.AddSingleton(provider => new PulseOffice.Presentations.UI.Design.ShowSetupServices(
            provider.GetRequiredService<PulseOffice.Presentations.UI.Motion.IDeckSlidePictures>(),
            provider.GetRequiredService<PulseOffice.UI.Controls.Fields.IMeasureUnits>(),
            provider.GetRequiredService<IIconCatalog>(),
            provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>()));
        services.TryAddSingleton<PulseOffice.Presentations.ISlideResize, PulseOffice.Presentations.SlideResize>();
        services.AddSingleton(provider => new PulseOffice.Presentations.UI.Design.SlideSizeServices(
            provider.GetRequiredService<PulseOffice.Presentations.ISlideResize>(),
            provider.GetRequiredService<PulseOffice.UI.Controls.Fields.IMeasureUnits>(),
            provider.GetRequiredService<IIconCatalog>(),
            provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>(),
            provider.GetRequiredService<PulseOffice.UI.Controls.Galleries.IGalleryNavigator>()));
        services.AddSingleton(provider => new PulseOffice.Presentations.UI.Design.HeaderFooterServices(
            provider.GetRequiredService<PulseOffice.Presentations.UI.Motion.IDeckSlidePictures>(),
            provider.GetRequiredService<PulseOffice.Document.Pages.IPageSketchLayout>(),
            provider.GetRequiredService<IIconCatalog>(),
            provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>(),
            provider.GetRequiredService<PulseOffice.UI.Controls.Galleries.IGalleryNavigator>(),
            provider.GetRequiredService<PulseOffice.Presentations.IHeaderFooterDateFormats>(),
            provider.GetRequiredService<PulseOffice.Text.Rich.ITextCultures>()));
        services.AddSingleton<PulseOffice.Presentations.IHeaderFooterDateFormats, PulseOffice.Presentations.HeaderFooterDateFormats>();
        services.AddSingleton(provider => new PulseOffice.Presentations.UI.Design.SlideBackgroundServices(
            provider.GetRequiredService<PulseOffice.UI.Controls.Fills.IFillEditors>(),
            provider.GetRequiredService<PulseOffice.Presentations.ISlideBackgrounds>(),
            provider.GetRequiredService<PulseOffice.Presentations.UI.Motion.IDeckSlidePictures>(),
            provider.GetRequiredService<PulseOffice.Presentations.ISlideFactory>(),
            provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>()));
        services.AddSingleton<PulseOffice.UI.Controls.Ribbon.IRibbonThumbnailPainter, PulseOffice.Presentations.UI.Motion.TransitionRibbonThumbnailPainter>();
        services.AddSingleton<PulseOffice.Presentations.IAnimationScheduler, PulseOffice.Presentations.AnimationScheduler>();
        services.AddSingleton<PulseOffice.Presentations.UI.IDeckDocumentFactory,
            PulseOffice.Presentations.UI.DeckDocumentFactory>();

        return services;
    }
}
