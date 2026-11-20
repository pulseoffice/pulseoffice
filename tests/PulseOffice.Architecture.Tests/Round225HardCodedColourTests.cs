using System.Text.RegularExpressions;
using Xunit;

namespace PulseOffice.Architecture.Tests;

public sealed class Round225HardCodedColourTests
{
    private static readonly Regex Literal = new(
        @"Color\.FromRgb\(|Color\.FromArgb\(|Color\.Parse\(""#|Colors\.(?!Transparent\b)[A-Z]\w*|Brushes\.(?!Transparent\b)[A-Z]\w*",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, int> Allowed = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["PulseOffice.Agent.UI/AgentPageRenderer.cs"] = 1,
        ["PulseOffice.Data.UI/DataImportView.cs"] = 1,
        ["PulseOffice.Data.UI/DataTableRenderer.cs"] = 6,
        ["PulseOffice.Data.UI/TableEditorPalette.cs"] = 11,
        ["PulseOffice.Data.UI/TabularPreview.cs"] = 1,
        ["PulseOffice.Documents.UI/Canvas/FolioCanvas.cs"] = 9,
        ["PulseOffice.Documents.UI/Canvas/FolioCommentMargin.cs"] = 4,
        ["PulseOffice.Documents.UI/Canvas/FolioObjectGestures.cs"] = 2,
        ["PulseOffice.Documents.UI/Canvas/FolioTextOverlays.cs"] = 7,
        ["PulseOffice.Documents.UI/Commands/AutoTextView.cs"] = 2,
        ["PulseOffice.Documents.UI/Commands/EnvelopePreview.cs"] = 4,
        ["PulseOffice.Documents.UI/Commands/FolioObjectDialogs.cs"] = 3,
        ["PulseOffice.Documents.UI/Commands/FolioToolCommands.cs"] = 4,
        ["PulseOffice.Documents.UI/Commands/TableGridPreview.cs"] = 4,
        ["PulseOffice.Documents.UI/Commands/TablePlacementPreview.cs"] = 5,
        ["PulseOffice.Documents.UI/Fields/FolioFieldsView.cs"] = 2,
        ["PulseOffice.Documents.UI/Indexes/FolioIndexGallery.cs"] = 4,
        ["PulseOffice.Documents.UI/Shell/FolioMergeWizard.cs"] = 3,
        ["PulseOffice.Presentations.UI/CustomShowsView.cs"] = 1,
        ["PulseOffice.Presentations.UI/DeckColorBar.cs"] = 1,
        ["PulseOffice.Presentations.UI/DeckShapeTextEditing.cs"] = 1,
        ["PulseOffice.Presentations.UI/DeckTextEditing.cs"] = 2,
        ["PulseOffice.Presentations.UI/DeckView.cs"] = 1,
        ["PulseOffice.Presentations.UI/Design/ImageExportView.cs"] = 1,
        ["PulseOffice.Presentations.UI/Design/MasterElementsView.cs"] = 1,
        ["PulseOffice.Presentations.UI/Design/ShowSetupView.cs"] = 1,
        ["PulseOffice.Presentations.UI/Design/SlidePickerView.cs"] = 1,
        ["PulseOffice.Presentations.UI/Motion/DeckSlidePictures.cs"] = 2,
        ["PulseOffice.Presentations.UI/Motion/TransitionThumbnail.cs"] = 5,
        ["PulseOffice.Presentations.UI/PresentationWindow.cs"] = 6,
        ["PulseOffice.Presentations.UI/PresenterWindow.cs"] = 3,
        ["PulseOffice.Presentations.UI/PrintMasterView.cs"] = 11,
        ["PulseOffice.Presentations.UI/Remote/RemoteView.cs"] = 2,
        ["PulseOffice.Presentations.UI/Rendering/ChromePainter.cs"] = 2,
        ["PulseOffice.Presentations.UI/Rendering/ElementPainters.cs"] = 2,
        ["PulseOffice.Presentations.UI/ShowRehearsalClock.cs"] = 2,
        ["PulseOffice.Presentations.UI/SlideImageRenderer.cs"] = 1,
        ["PulseOffice.Presentations.UI/Rendering/SlideBackdrop.cs"] = 1,
        ["PulseOffice.Presentations.UI/SlideView.cs"] = 19,
        ["PulseOffice.Presentations.UI/Themes/ThemePreviews.cs"] = 1,
        ["PulseOffice.Sheets.UI/CellFormat/CellAlignmentPage.cs"] = 4,
        ["PulseOffice.Sheets.UI/CellFormat/CellStylePainter.cs"] = 3,
        ["PulseOffice.Sheets.UI/CellFormat/NumberFormatPage.cs"] = 3,
        ["PulseOffice.Sheets.UI/CellFormat/SparklineDialogView.cs"] = 2,
        ["PulseOffice.Sheets.UI/CellTextMeasure.cs"] = 1,
        ["PulseOffice.Sheets.UI/Conditional/ConditionalRuleCatalog.cs"] = 1,
        ["PulseOffice.Sheets.UI/Conditional/ConditionalRuleEditorView.cs"] = 2,
        ["PulseOffice.Sheets.UI/Conditional/ConditionalRulePreview.cs"] = 6,
        ["PulseOffice.Sheets.UI/DataDialogs/CellGridSketch.cs"] = 6,
        ["PulseOffice.Sheets.UI/DataDialogs/PasteSketchView.cs"] = 6,
        ["PulseOffice.Sheets.UI/DataDialogs/RandomView.cs"] = 1,
        ["PulseOffice.Sheets.UI/DataDialogs/SeriesView.cs"] = 6,
        ["PulseOffice.Sheets.UI/DataDialogs/ValidationMessagePanes.cs"] = 17,
        ["PulseOffice.Sheets.UI/Dialogs/ValidationCriteriaField.cs"] = 3,
        ["PulseOffice.Sheets.UI/FilterMenuView.cs"] = 4,
        ["PulseOffice.Sheets.UI/GridParts/GridTheme.cs"] = 13,
        ["PulseOffice.Sheets.UI/GridParts/Painting/CellDecorationPainter.cs"] = 9,
        ["PulseOffice.Sheets.UI/GridParts/Painting/CellValuePainter.cs"] = 3,
        ["PulseOffice.Sheets.UI/GridParts/Painting/GridCellPainter.cs"] = 1,
        ["PulseOffice.Sheets.UI/GridParts/Painting/GridHeaderPainter.cs"] = 1,
        ["PulseOffice.Sheets.UI/GridParts/Painting/GridNotePainter.cs"] = 3,
        ["PulseOffice.Sheets.UI/GridParts/Painting/GridObjectPainter.cs"] = 6,
        ["PulseOffice.Sheets.UI/GridParts/Painting/GridOverlayPainter.cs"] = 5,
        ["PulseOffice.Sheets.UI/LedgerViewParts/LedgerShapeTextSession.cs"] = 1,
        ["PulseOffice.Sheets.UI/SheetGridView.cs"] = 4,
        ["PulseOffice.Sheets.UI/SheetObjectRasterizer.cs"] = 1,
        ["PulseOffice.Sheets.UI/SheetTabStrip.cs"] = 4,
        ["PulseOffice.Sheets.UI/SparklinePainter.cs"] = 1,
        ["PulseOffice.Shell/StartView.cs"] = 5,
        ["PulseOffice.UI.Controls/Borders/BorderFramePainter.cs"] = 1,
        ["PulseOffice.UI.Controls/Borders/BorderGlyphs.cs"] = 3,
        ["PulseOffice.UI.Controls/Borders/BorderSchema.cs"] = 5,
        ["PulseOffice.UI.Controls/Charts/ChartPreview.cs"] = 2,
        ["PulseOffice.UI.Controls/Charts/ChartSeriesStroke.cs"] = 2,
        ["PulseOffice.UI.Controls/Charts/ChartText.cs"] = 7,
        ["PulseOffice.UI.Controls/Charts/Plots/ChartPlot.cs"] = 1,
        ["PulseOffice.UI.Controls/Charts/Plots/FinancialPlot.cs"] = 2,
        ["PulseOffice.UI.Controls/Charts/Plots/LinePlot.cs"] = 1,
        ["PulseOffice.UI.Controls/Charts/Plots/MeterPlot.cs"] = 3,
        ["PulseOffice.UI.Controls/Charts/Plots/StatisticalPlot.cs"] = 3,
        ["PulseOffice.UI.Controls/Choices/SwatchStrip.cs"] = 1,
        ["PulseOffice.UI.Controls/ColorPickerView.cs"] = 1,
        ["PulseOffice.UI.Controls/Drawing/FreeformSketch.cs"] = 1,
        ["PulseOffice.UI.Controls/Fields/AngleDial.cs"] = 4,
        ["PulseOffice.UI.Controls/Fills/FillVisuals.cs"] = 7,
        ["PulseOffice.UI.Controls/Forms/InterfaceModeField.cs"] = 6,
        ["PulseOffice.UI.Controls/Formulas/FormulaObjectRenderer.cs"] = 1,
        ["PulseOffice.UI.Controls/Formulas/FormulaRenderer.cs"] = 1,
        ["PulseOffice.UI.Controls/IconView.cs"] = 1,
        ["PulseOffice.UI.Controls/Objects/ColorCorrectionView.cs"] = 1,
        ["PulseOffice.UI.Controls/Objects/ContourEditorView.cs"] = 6,
        ["PulseOffice.UI.Controls/Objects/ExternalLinksView.cs"] = 2,
        ["PulseOffice.UI.Controls/Objects/ImageMapEditorView.cs"] = 1,
        ["PulseOffice.UI.Controls/Objects/MediaView.cs"] = 2,
        ["PulseOffice.UI.Controls/Objects/PackagedObjectRenderer.cs"] = 2,
        ["PulseOffice.UI.Controls/Pages/LineNumberSampleView.cs"] = 3,
        ["PulseOffice.UI.Controls/Preview/LivePreviewPane.cs"] = 3,
        ["PulseOffice.UI.Controls/Preview/PagePreview.cs"] = 6,
        ["PulseOffice.UI.Controls/Printing/AvaloniaPageCanvas.cs"] = 4,
        ["PulseOffice.UI.Controls/Printing/DrawingReplay.cs"] = 1,
        ["PulseOffice.UI.Controls/Printing/PrintPagesView.cs"] = 3,
        ["PulseOffice.UI.Controls/Prompts/ZoomView.cs"] = 6,
        ["PulseOffice.UI.Controls/Ribbon/ChoiceSlot.cs"] = 5,
        ["PulseOffice.UI.Controls/Ribbon/ColorPaletteView.cs"] = 1,
        ["PulseOffice.UI.Controls/Ribbon/RibbonTabStrip.cs"] = 4,
        ["PulseOffice.UI.Controls/RibbonView.cs"] = 2,
        ["PulseOffice.UI.Controls/Rulers/RulerStrip.cs"] = 8,
        ["PulseOffice.UI.Controls/Services/EmbeddedObjectPicture.cs"] = 2,
        ["PulseOffice.UI.Controls/Settings/SwitchToggle.cs"] = 1,
        ["PulseOffice.UI.Controls/Shapes/AdjustHandleChrome.cs"] = 3,
        ["PulseOffice.UI.Controls/Shapes/AssetPickerView.cs"] = 1,
        ["PulseOffice.UI.Controls/Shapes/Fontwork/FontworkGalleryView.cs"] = 1,
        ["PulseOffice.UI.Controls/Shapes/Fontwork/FontworkPreview.cs"] = 1,
        ["PulseOffice.UI.Controls/Shapes/FontworkRenderer.cs"] = 3,
        ["PulseOffice.UI.Controls/Shapes/ObjectFrameChrome.cs"] = 1,
        ["PulseOffice.UI.Controls/Shapes/Painting/Painters.cs"] = 2,
        ["PulseOffice.UI.Controls/Shapes/Painting/ScenePainter.cs"] = 8,
        ["PulseOffice.UI.Controls/Shapes/ShapeBrushes.cs"] = 8,
        ["PulseOffice.UI.Controls/Shapes/ShapeGalleryView.cs"] = 1,
        ["PulseOffice.UI.Controls/Shapes/ShapePickerView.cs"] = 1,
        ["PulseOffice.UI.Controls/Shapes/ShapeRenderer.cs"] = 23,
        ["PulseOffice.UI.Controls/Shapes/ShapeTextEditor.cs"] = 1,
        ["PulseOffice.UI.Controls/Spectrum/SpectrumSurface.cs"] = 8,
        ["PulseOffice.UI.Controls/Tables/Insert/TableSizeGrid.cs"] = 3,
        ["PulseOffice.UI.Controls/Tables/TableStyleGalleryView.cs"] = 4,
        ["PulseOffice.UI.Controls/Tabs/PageTabStrip.cs"] = 3,
        ["PulseOffice.UI.Controls/Tabs/TabStrip.cs"] = 2,
        ["PulseOffice.UI.Controls/Templates/PageThumbnail.cs"] = 5,
        ["PulseOffice.UI.Controls/Templates/TemplateGalleryView.cs"] = 1,
        ["PulseOffice.UI.Controls/Text/Editing/InlineTextEditor.cs"] = 2,
        ["PulseOffice.UI.Controls/Text/Rich/TextTypesetting.cs"] = 2,
        ["PulseOffice.UI.Controls/TextFormat/CharacterSampleView.cs"] = 9,
        ["PulseOffice.UI.Controls/TextFormat/ListSampleView.cs"] = 4,
        ["PulseOffice.UI.Controls/TextFormat/ParagraphSampleView.cs"] = 3,
        ["PulseOffice.UI.Controls/Themes/ThemePaletteGallery.cs"] = 4,
        ["PulseOffice.UI.Embedding/ContentPainters.cs"] = 18,
        ["PulseOffice.UI.Embedding/SheetObjectFactory.cs"] = 1,
        ["PulseOffice.UI/Theming/ColorContrast.cs"] = 4,
        ["PulseOffice.UI/Theming/ThemePreference.cs"] = 1,
    };

    [Fact]
    public void Interface_drawing_code_takes_colours_from_the_palette()
    {
        var sources = Sources();
        var offenders = new List<string>();
        foreach (var project in sources.EnumerateDirectories().Where(Interface))
            foreach (var file in project.EnumerateFiles("*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sources.FullName, file.FullName).Replace(Path.DirectorySeparatorChar, '/');
                if (relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal)) continue;
                var count = Literal.Matches(File.ReadAllText(file.FullName)).Count;
                if (count > Allowed.GetValueOrDefault(relative)) offenders.Add($"{relative}: {count} colour literals, {Allowed.GetValueOrDefault(relative)} allowed");
            }

        Assert.True(offenders.Count == 0, "Use Palette tokens instead of colour literals:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    private static bool Interface(DirectoryInfo project) =>
        project.Name.Contains(".UI", StringComparison.Ordinal) || project.Name.EndsWith(".Shell", StringComparison.Ordinal);

    private static DirectoryInfo Sources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PulseOffice.slnx"))) directory = directory.Parent;
        return new DirectoryInfo(Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("PulseOffice.slnx"), "src"));
    }
}
