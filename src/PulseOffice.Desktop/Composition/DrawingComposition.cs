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

internal static class DrawingComposition
{
    public static IServiceCollection AddDrawing(this IServiceCollection services, IUserDataPaths paths)
    {
        services.AddSingleton<IConnectorRouter, ConnectorRouter>();
        services.AddSingleton<IConnectorFramer, ConnectorFramer>();
        services.AddSingleton<ISheetConnectors, SheetConnectors>();
        services.AddSingleton<ISheetPlacement, SheetPlacement>();
        services.AddSingleton<ISheetObjectAnchoring, SheetObjectAnchoring>();
        services.AddSingleton<ISheetGrouping, SheetGrouping>();
        services.AddSingleton<IShapeGeometry, ShapeGeometry>();
        services.AddSingleton<PulseOffice.Text.ITextCaseTransform, PulseOffice.Text.UpperCase>();
        services.AddSingleton<PulseOffice.Text.ITextCaseTransform, PulseOffice.Text.LowerCase>();
        services.AddSingleton<PulseOffice.Text.ITextCaseTransform, PulseOffice.Text.SentenceCase>();
        services.AddSingleton<PulseOffice.Text.ITextCaseTransform, PulseOffice.Text.TitleCase>();
        services.AddSingleton<PulseOffice.Text.ITextCaseTransform, PulseOffice.Text.ToggleCase>();
        services.AddSingleton<PulseOffice.Text.ITextCaseTransform, PulseOffice.Text.HalfWidth>();
        services.AddSingleton<PulseOffice.Text.ITextCaseTransform, PulseOffice.Text.FullWidth>();
        services.AddSingleton<PulseOffice.Text.ITextCaseTransform, PulseOffice.Text.Hiragana>();
        services.AddSingleton<PulseOffice.Text.ITextCaseTransform, PulseOffice.Text.Katakana>();
        services.AddSingleton<PulseOffice.Text.ITextCases, PulseOffice.Text.TextCases>();
        services.AddSingleton<PulseOffice.UI.Controls.Shapes.IShapeStyling, PulseOffice.UI.Controls.Shapes.ShapeStyling>();
        services.AddSingleton<IShapePresets, ShapePresets>();
        services.AddSingleton<IDimensionLineTemplate, DimensionLineTemplate>();
        services.AddSingleton<IAssetSource>(provider => new BuiltInAssetSource(
            new IconCatalog(),
            provider.GetRequiredService<IShapeGeometry>(),
            provider.GetRequiredService<IShapePathWriter>(),
            provider.GetRequiredService<IShapePresets>()));
        services.AddSingleton<IAssetSource>(_ => new FolderAssetSource(
            paths.Configuration("libraries")));
        services.AddSingleton<IAssetLibrary>(provider =>
            new AssetLibrary(provider.GetServices<IAssetSource>()));
        services.AddSingleton<IShapePathWriter, ShapePathWriter>();
        services.AddSingleton<IObjectManipulator, ObjectManipulator>();
        services.AddSingleton<IGroupChildLinks, GroupChildLinks>();
        services.AddSingleton<IVectorOutlineReader, VectorOutlineReader>();
        services.AddSingleton<PulseOffice.UI.Controls.Shapes.IShapeBrushes>(provider => new PulseOffice.UI.Controls.Shapes.ShapeBrushes(provider.GetRequiredService<PulseOffice.UI.Controls.Objects.IBitmapPixels>()));
        services.AddSingleton<PulseOffice.UI.Controls.Shapes.IShapeRenderer>(provider => new PulseOffice.UI.Controls.Shapes.ShapeRenderer(
            provider.GetRequiredService<IShapeGeometry>(), provider.GetRequiredService<PulseOffice.UI.Controls.Shapes.IShapeBrushes>()));
        services.AddSingleton<PulseOffice.Drawing.Shapes.Geometry.IShapeAdjustHandles>(_ => new PulseOffice.Drawing.Shapes.Geometry.ShapeAdjustHandles());
        services.AddSingleton<PulseOffice.UI.Controls.Shapes.IAdjustHandleChrome>(provider => new PulseOffice.UI.Controls.Shapes.AdjustHandleChrome(
            provider.GetRequiredService<PulseOffice.Drawing.Shapes.Geometry.IShapeAdjustHandles>(), provider.GetRequiredService<IShapeGeometry>()));
        services.AddSingleton<PulseOffice.UI.Controls.Shapes.IShapeStyleEditors, PulseOffice.UI.Controls.Shapes.ShapeStyleEditors>();
        services.TryAddSingleton<PulseOffice.Drawing.Shapes.IGradientStopEditing, PulseOffice.Drawing.Shapes.GradientStopEditing>();
        services.TryAddSingleton<PulseOffice.Drawing.Shapes.IFillPatternCatalog, PulseOffice.Drawing.Shapes.FillPatternCatalog>();
        services.AddSingleton<PulseOffice.UI.Controls.Fills.IFillEditors>(provider => new PulseOffice.UI.Controls.Fills.FillEditors(
            provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>(),
            provider.GetRequiredService<PulseOffice.UI.Controls.Shapes.IShapeBrushes>(),
            provider.GetRequiredService<PulseOffice.Drawing.Shapes.IGradientStopEditing>(),
            provider.GetRequiredService<PulseOffice.Drawing.Shapes.IFillPatternCatalog>(),
            provider.GetService<PulseOffice.UI.Controls.Objects.IRasterImageFiles>()));
        services.TryAddSingleton<PulseOffice.Document.Borders.IBorderFrameResolver, PulseOffice.Document.Borders.BorderFrameResolver>();
        services.TryAddSingleton<PulseOffice.Document.Borders.IBorderPresetCatalog, PulseOffice.Document.Borders.BorderPresetCatalog>();
        services.TryAddSingleton<PulseOffice.Document.Borders.IBorderFrameEditing, PulseOffice.Document.Borders.BorderFrameEditing>();
        services.AddSingleton<PulseOffice.UI.Controls.Borders.IBorderSchemaGeometry, PulseOffice.UI.Controls.Borders.BorderSchemaGeometry>();
        services.AddSingleton<PulseOffice.UI.Controls.Borders.IBorderFramePainter, PulseOffice.UI.Controls.Borders.BorderFramePainter>();
        services.AddSingleton<PulseOffice.UI.Controls.Borders.IBorderEditors>(provider => new PulseOffice.UI.Controls.Borders.BorderEditors(
            provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>(),
            provider.GetRequiredService<PulseOffice.Document.Borders.IBorderPresetCatalog>(),
            provider.GetRequiredService<PulseOffice.Document.Borders.IBorderFrameEditing>(),
            provider.GetRequiredService<PulseOffice.UI.Controls.Borders.IBorderFramePainter>(),
            provider.GetRequiredService<PulseOffice.UI.Controls.Borders.IBorderSchemaGeometry>()));
        services.AddSingleton<PulseOffice.UI.Controls.Borders.IBorderDialog, PulseOffice.UI.Controls.Borders.BorderDialog>();
        services.AddSingleton<PulseOffice.Sheets.Styling.ICellBorderFrames, PulseOffice.Sheets.Styling.CellBorderFrames>();
        services.AddSingleton<PulseOffice.Presentations.UI.IDeckTableLookSeed, PulseOffice.Presentations.UI.DeckTableLookSeed>();
        services.AddSingleton<PulseOffice.Presentations.UI.IDeckTableBorders, PulseOffice.Presentations.UI.DeckTableBorders>();
        services.AddSingleton<PulseOffice.Presentations.UI.IDeckTableCellFormatting, PulseOffice.Presentations.UI.DeckTableCellFormatting>();
        services.AddSingleton<PulseOffice.Presentations.UI.IDeckTableCellEditing, PulseOffice.Presentations.UI.DeckTableCellEditing>();
        services.AddSingleton<PulseOffice.UI.Controls.Shapes.IFontworkRenderer>(provider => new PulseOffice.UI.Controls.Shapes.FontworkRenderer(provider.GetRequiredService<PulseOffice.UI.Controls.Shapes.IShapeBrushes>()));
        services.TryAddSingleton<IExtendedShapes, ExtendedShapes>();
        services.TryAddSingleton<PulseOffice.Drawing.Shapes.Geometry.IPresetShapeCatalog, PulseOffice.Drawing.Shapes.Geometry.PresetShapeCatalog>();
        services.TryAddSingleton<PulseOffice.Drawing.Shapes.Geometry.IDrawingBuiltIns, PulseOffice.Drawing.Shapes.Geometry.DrawingBuiltIns>();
        services.TryAddSingleton<PulseOffice.Drawing.Shapes.Geometry.IGuideOperators, PulseOffice.Drawing.Shapes.Geometry.GuideOperators>();
        services.TryAddSingleton<PulseOffice.Drawing.Shapes.Geometry.IPresetGeometryEngine, PulseOffice.Drawing.Shapes.Geometry.PresetGeometryEngine>();
        services.TryAddSingleton<PulseOffice.Drawing.Shapes.Geometry.IEnhancedPathInterpreter>(_ => new PulseOffice.Drawing.Shapes.Geometry.EnhancedPathInterpreter());
        services.TryAddSingleton<PulseOffice.Drawing.Shapes.Geometry.IComputedGeometryEngine, PulseOffice.Drawing.Shapes.Geometry.ComputedGeometryEngine>();
        services.TryAddSingleton<IArcFlattener, ArcFlattener>();
        services.TryAddSingleton<PulseOffice.Sheets.Engine.Operations.IConditionalRuleRemoval, PulseOffice.Sheets.Engine.Operations.ConditionalRuleRemoval>();
        services.TryAddSingleton<PulseOffice.Sheets.Engine.Operations.ISheetRangeReferences, PulseOffice.Sheets.Engine.Operations.SheetRangeReferences>();
        services.AddSingleton<ISvgPathParser, SvgPathParser>();
        services.AddSingleton<PulseOffice.UI.Controls.StylePanels.IStyleRecents, PulseOffice.UI.Controls.StylePanels.StyleRecents>();
        services.AddSingleton<IIconCatalog>(provider => new CompositeIconCatalog(
        [
            new IconCatalog(),
            new CellIcons(),
            new DataIcons(),
            new DataDialogIcons(),
            new FormulaIcons(),
            new DrawingIcons(),
            new ViewIcons(),
            new ReviewIcons(),
            new ToolIcons(),
            new SettingsIcons(),
            new PageIcons(),
            new DeckIcons(),
            new FolioIcons(),
            new TextFormatIcons(),
            new RibbonControlIcons(),
            new ChartIcons(),
            new PanelIcons(),
            new ShapeIconCatalog(
                provider.GetRequiredService<IShapeGeometry>(),
                provider.GetRequiredService<IShapePathWriter>()),
        ]));

        return services;
    }
}
