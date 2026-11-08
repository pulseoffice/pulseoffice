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

internal static class SharedControlsComposition
{
    public static IServiceCollection AddSharedControls(this IServiceCollection services)
    {
        services.AddSingleton<PulseOffice.UI.Controls.Shapes.IObjectNudgeKeys, PulseOffice.UI.Controls.Shapes.ObjectNudgeKeys>();
        services.AddSingleton<PulseOffice.UI.Controls.Rulers.IParagraphRuler, PulseOffice.UI.Controls.Rulers.ParagraphRuler>();
        services.AddSingleton<PulseOffice.UI.Controls.Notifications.INotificationCenter, PulseOffice.UI.Controls.Notifications.NotificationCenter>();
        services.AddSingleton<PulseOffice.UI.Controls.Notifications.INotificationHistory, PulseOffice.UI.Controls.Notifications.NotificationHistory>();
        services.AddSingleton<IStatusBarService, StatusBarService>();
        services.AddSingleton(provider =>
        {
            var host = new DialogHost();
            host.Use(
                provider.GetRequiredService<IIconCatalog>(),
                provider.GetRequiredService<ITextCatalog>());
            host.AdmitResultsThrough(provider.GetService<IDialogResultAdmission>);
            return host;
        });
        services.AddSingleton<IDialogService>(s => s.GetRequiredService<DialogHost>());
        services.AddSingleton<ITabbedForms, TabbedForms>();
        services.AddSingleton<PulseOffice.UI.Controls.Galleries.IGalleryNavigator, PulseOffice.UI.Controls.Galleries.GalleryNavigator>();
        services.AddSingleton<PulseOffice.UI.Controls.Themes.IThemePaletteGallery, PulseOffice.UI.Controls.Themes.ThemePaletteGallery>();
        services.AddSingleton<PulseOffice.UI.Controls.Preview.IDebounceTimerFactory, PulseOffice.UI.Controls.Preview.DispatcherDebounceTimerFactory>();
        services.AddSingleton<PulseOffice.Document.Pages.IPageSketchLayout, PulseOffice.Document.Pages.PageSketchLayout>();
        services.AddSingleton<PulseOffice.UI.Controls.Tables.ITableStyleGallery, PulseOffice.UI.Controls.Tables.TableStyleGallery>();
        services.AddSingleton<PulseOffice.UI.Controls.Tables.Insert.ITableInsertPrompt, PulseOffice.UI.Controls.Tables.Insert.TableInsertPrompt>();
        services.AddSingleton<PulseOffice.UI.Controls.Links.ILinkAddresses, PulseOffice.UI.Controls.Links.LinkAddresses>();
        services.AddSingleton<PulseOffice.UI.Controls.Review.IReviewPanelRequests, PulseOffice.UI.Controls.Review.ReviewPanelRequests>();
        services.AddSingleton<PulseOffice.UI.Controls.Links.IHyperlinkPrompt, PulseOffice.UI.Controls.Links.HyperlinkPrompt>();
        services.AddSingleton<PulseOffice.UI.Controls.Links.ILinkFollower, PulseOffice.UI.Controls.Links.LinkFollower>();
        services.AddSingleton<PulseOffice.Shell.StylePanels.ModuleStylesSource>();
        services.AddSingleton<PulseOffice.UI.Controls.StylePanels.IStyleRenamePrompt, PulseOffice.UI.Controls.StylePanels.StyleRenamePrompt>();
        services.AddSingleton<PulseOffice.Shell.Navigation.ModuleNavigatorSource>();
        services.AddSingleton<PulseOffice.Shell.Navigation.ModuleReviewSource>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckNavigatorSource>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckReviewSource>();
        services.AddSingleton<PulseOffice.Sheets.UI.LedgerNavigatorSource>();
        services.AddSingleton<PulseOffice.UI.Controls.Text.ILanguageChoices, PulseOffice.UI.Controls.Text.LanguageChoices>();
        services.AddSingleton<PulseOffice.UI.Controls.Fields.IMeasureUnits, PulseOffice.UI.Controls.Fields.MeasureUnits>();
        services.AddSingleton<PulseOffice.UI.Controls.TextFormat.ITextFormatPreviews, PulseOffice.UI.Controls.TextFormat.TextFormatPreviews>();
        services.AddSingleton<PulseOffice.UI.Controls.TextFormat.ICharacterSampleLayout, PulseOffice.UI.Controls.TextFormat.CharacterSampleLayout>();
        services.AddSingleton<PulseOffice.UI.Controls.TextFormat.IParagraphSampleLayout, PulseOffice.UI.Controls.TextFormat.ParagraphSampleLayout>();
        services.AddSingleton<PulseOffice.UI.Controls.Pages.ILineNumberSampleLayout, PulseOffice.UI.Controls.Pages.LineNumberSampleLayout>();
        services.AddSingleton<PulseOffice.UI.Controls.TextFormat.ICharacterFormatTabs, PulseOffice.UI.Controls.TextFormat.CharacterFormatTabs>();
        services.AddSingleton<PulseOffice.UI.Controls.TextFormat.ICharacterFormatDialog, PulseOffice.UI.Controls.TextFormat.CharacterFormatDialog>();
        services.AddSingleton<PulseOffice.UI.Controls.TextFormat.IParagraphFormatTabs, PulseOffice.UI.Controls.TextFormat.ParagraphFormatTabs>();
        services.AddSingleton<PulseOffice.UI.Controls.TextFormat.IParagraphFormatDialog, PulseOffice.UI.Controls.TextFormat.ParagraphFormatDialog>();
        services.AddSingleton<PulseOffice.UI.Controls.TextFormat.IListLabeler, PulseOffice.UI.Controls.TextFormat.ListLabeler>();
        services.AddSingleton<PulseOffice.UI.Controls.TextFormat.IListSampleRows, PulseOffice.UI.Controls.TextFormat.ListSampleRows>();
        services.AddSingleton<PulseOffice.UI.Controls.TextFormat.IListPresetCatalog, PulseOffice.UI.Controls.TextFormat.ListPresetCatalog>();
        services.AddSingleton<PulseOffice.UI.Controls.TextFormat.IListLevelsEditors, PulseOffice.UI.Controls.TextFormat.ListLevelsEditors>();
        services.AddSingleton<PulseOffice.UI.Controls.TextFormat.IListPictureChooser, PulseOffice.UI.Controls.TextFormat.ListPictureChooser>();
        services.AddSingleton<PulseOffice.UI.Controls.TextFormat.IListFormatTabs, PulseOffice.UI.Controls.TextFormat.ListFormatTabs>();
        services.AddSingleton<PulseOffice.UI.Controls.TextFormat.IListFormatDialog, PulseOffice.UI.Controls.TextFormat.ListFormatDialog>();
        services.AddSingleton<PulseOffice.UI.Controls.TextFormat.IParagraphListFormats, PulseOffice.UI.Controls.TextFormat.ParagraphListFormats>();
        services.AddSingleton<PulseOffice.UI.Controls.Pages.IMarginPresetCatalog, PulseOffice.UI.Controls.Pages.MarginPresetCatalog>();
        services.AddSingleton<PulseOffice.UI.Controls.Pages.IPageMarginsEditors, PulseOffice.UI.Controls.Pages.PageMarginsEditors>();
        services.AddSingleton<PulseOffice.UI.Controls.Pages.IColumnLayoutEditors, PulseOffice.UI.Controls.Pages.ColumnLayoutEditors>();
        services.AddSingleton<PulseOffice.UI.Controls.Pages.IPageNumberPositionGalleries, PulseOffice.UI.Controls.Pages.PageNumberPositionGalleries>();
        services.AddSingleton<PulseOffice.UI.Controls.References.IDialogCollapse>(s => s.GetRequiredService<DialogHost>());
        services.AddSingleton<PulseOffice.UI.Controls.References.IReferencePickers, PulseOffice.UI.Controls.References.ReferencePickers>();

        return services;
    }
}
