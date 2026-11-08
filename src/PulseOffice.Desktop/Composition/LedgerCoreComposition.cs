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
using PulseOffice.Exchange;

namespace PulseOffice.Desktop.Composition;

internal static class LedgerCoreComposition
{
    public static IServiceCollection AddLedgerCore(this IServiceCollection services)
    {
        services.AddSingleton<FormatCodeVocabulary>();
        services.AddSingleton<PulseOffice.Sheets.Formatting.INumberFormatParser, PulseOffice.Sheets.Formatting.NumberFormatParser>();
        services.AddSingleton<PulseOffice.Sheets.Formatting.ISpreadsheetLocale, Sheets.UI.InterfaceSpreadsheetLocale>();
        services.AddSingleton<INumberFormatter>(provider => new PulseOffice.Sheets.Formatting.LocaleNumberFormatter(
            provider.GetRequiredService<PulseOffice.Sheets.Formatting.ISpreadsheetLocale>(),
            serial => new DateOnly(1899, 12, 30).ToDateTime(TimeOnly.MinValue).AddDays(serial)));
        services.AddSingleton<ICellPresenter, CellPresenter>();
        services.AddSingleton<Sheets.UI.ICellTextComposer, Sheets.UI.CellTextComposer>();
        services.AddSingleton<Sheets.Formatting.ICellTextMeasure, Sheets.UI.CellTextMeasure>();
        services.AddSingleton<Sheets.Formatting.IRowAutoHeight, Sheets.Formatting.RowAutoHeight>();
        services.AddSingleton<Sheets.Formatting.IColumnAutoWidth, Sheets.Formatting.ColumnAutoWidth>();
        services.AddSingleton<PulseOffice.Sheets.Engine.Operations.ConditionalFormattingEvaluator>();
        services.AddSingleton<IActiveSheetContext, ActiveSheetContext>();
        services.TryAddSingleton<PulseOffice.Document.Commands.UndoToStatePoint>();
        services.TryAddSingleton<PulseOffice.Document.Commands.RedoToStatePoint>();
        services.TryAddSingleton<PulseOffice.Document.Commands.IHistoryReversals, PulseOffice.Document.Commands.HistoryReversals>();
        services.AddSingleton<PulseOffice.UI.Commands.ICommandEffectGuard, LedgerProtectionGuard>();
        services.AddSingleton<PulseOffice.Shell.IDocumentStateWitness, LedgerStateWitness>();
        services.AddSingleton<ISheetFormattingService, SheetFormattingService>();
        services.AddSingleton<PulseOffice.Sheets.Addressing.ISelectionWorkArea, PulseOffice.Sheets.Addressing.SelectionWorkArea>();
        services.AddSingleton<PulseOffice.Sheets.Styling.IStyleBandFormatter, PulseOffice.Sheets.Styling.StyleBandFormatter>();
        services.AddSingleton<PulseOffice.Sheets.Styling.IBorderPresets, PulseOffice.Sheets.Styling.BorderPresets>();
        services.AddSingleton<PulseOffice.Sheets.Styling.ICellStyleDelta, PulseOffice.Sheets.Styling.CellStyleDelta>();
        services.AddSingleton<ICellFormatDialog, CellFormatDialog>();
        services.AddSingleton<PulseOffice.Sheets.Formatting.INumberFormatDesigner, PulseOffice.Sheets.Formatting.NumberFormatDesigner>();
        services.AddSingleton<PulseOffice.UI.Controls.Fields.IAngleDialGeometry, PulseOffice.UI.Controls.Fields.AngleDialGeometry>();
        services.AddSingleton<Sheets.UI.CellFormat.ICellCharacterFormat, Sheets.UI.CellFormat.CellCharacterFormat>();
        services.AddSingleton<Sheets.UI.CellFormat.FormatCellsServices>();
        services.AddSingleton<LedgerRibbonCommands>();
        services.AddSingleton<PulseOffice.Sheets.UI.Exchange.ISheetRangeCapture, PulseOffice.Sheets.UI.Exchange.SheetRangeCapture>();
        services.AddSingleton<PulseOffice.Sheets.UI.Exchange.IExchangeCellStyles, PulseOffice.Sheets.UI.Exchange.ExchangeCellStyles>();
        services.AddSingleton<PulseOffice.Sheets.UI.Exchange.IExchangeGridSnapshots, PulseOffice.Sheets.UI.Exchange.ExchangeGridSnapshots>();
        services.AddSingleton<PulseOffice.Sheets.Styling.IStyleTransplant, PulseOffice.Sheets.Styling.StyleTransplant>();
        services.AddSingleton<PulseOffice.Sheets.Engine.Operations.ISheetClipboard, PulseOffice.Sheets.Engine.Operations.SheetClipboard>();
        services.AddSingleton<PulseOffice.Sheets.UI.Exchange.LedgerExchangeServices>();
        services.AddSingleton<PulseOffice.Sheets.UI.Exchange.ILedgerExchange, PulseOffice.Sheets.UI.Exchange.LedgerExchange>();
        services.AddSingleton<PulseOffice.Exchange.IExchangeParticipant>(s => s.GetRequiredService<PulseOffice.Sheets.UI.Exchange.ILedgerExchange>());
        services.AddSingleton<ISheetClipboardService, SheetClipboardService>();
        services.AddSingleton<PulseOffice.Sheets.ISheetEditGuard, PulseOffice.Sheets.SheetEditGuard>();
        services.AddSingleton<PulseOffice.Sheets.Styling.ICellStyleReferences, PulseOffice.Sheets.Styling.CellStyleReferences>();
        services.AddSingleton<ISheetFormatGate, SheetFormatGate>();
        services.AddSingleton<ISheetSortGate, SheetSortGate>();
        services.AddSingleton<PulseOffice.Platform.IRichClipboardFormatsProvider, RichClipboardFormatsProvider>();
        services.AddSingleton<PulseOffice.UI.Controls.Services.IExchangeDataTransfers, PulseOffice.UI.Controls.Services.ExchangeDataTransfers>();
        services.AddSingleton(s => new PulseOffice.UI.Controls.Services.SystemClipboard(
            () => Avalonia.Controls.TopLevel.GetTopLevel(s.GetRequiredService<ShellWindow>())?.Clipboard,
            s.GetRequiredService<PulseOffice.UI.Controls.Services.IExchangeDataTransfers>()));
        services.AddSingleton<PulseOffice.UI.Controls.Services.ISystemClipboardText>(s => s.GetRequiredService<PulseOffice.UI.Controls.Services.SystemClipboard>());
        services.AddSingleton<PulseOffice.Exchange.IExchangeClipboard>(s => s.GetRequiredService<PulseOffice.UI.Controls.Services.SystemClipboard>());
        services.AddObjectExchange();
        services.AddSingleton<PulseOffice.UI.Controls.Exchange.IExchangePasteChooser, PulseOffice.UI.Controls.Exchange.ExchangePasteChooser>();
        services.AddSingleton<INumberFormatCodeEditor, NumberFormatCodeEditor>();
        services.AddSingleton<LedgerCellFormatCommands>();
        services.AddSingleton<LedgerPaneCommands>();
        services.AddSingleton<LedgerEditDataCommands>();
        services.AddSingleton<LedgerConditionalRuleCommands>();
        services.AddSingleton<LedgerChartCommands>();
        services.AddSingleton<LedgerShapeCommands>();
        services.AddSingleton<LedgerShapeListCommands>();
        services.AddSingleton<LedgerCommands>();
        services.AddSingleton<PulseOffice.Sheets.IFormControlBinding, PulseOffice.Sheets.FormControlBinding>();
        services.AddSingleton<ILedgerDocumentFactory, LedgerDocumentFactory>();
        services.TryAddSingleton<PulseOffice.Sheets.Engine.Localization.IFunctionNameVocabulary, PulseOffice.Sheets.Engine.Localization.FunctionNameVocabulary>();
        services.AddSingleton<PulseOffice.Sheets.Engine.Localization.IFormulaPunctuationConvention, PulseOffice.Sheets.Engine.Localization.FormulaPunctuationConvention>();
        services.AddSingleton<PulseOffice.Sheets.Engine.Localization.IFormulaLocalization, PulseOffice.Sheets.Engine.Localization.FormulaLocalization>();
        services.AddSingleton<PulseOffice.UI.Controls.Shapes.IDrawingObjectRenderer,
            PulseOffice.UI.Controls.Shapes.DrawingObjectRenderer>();
        services.AddSingleton<PulseOffice.UI.Controls.Services.IObjectPicturePainter, PulseOffice.UI.Controls.Services.ChartPicturePainter>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<PulseOffice.UI.Controls.Services.IObjectPicturePainter, PulseOffice.UI.Controls.Services.DrawingPicturePainter>());
        services.TryAddSingleton<PulseOffice.UI.Controls.Formulas.IFormulaObjectRenderer, PulseOffice.UI.Controls.Formulas.FormulaObjectRenderer>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<PulseOffice.UI.Controls.Services.IObjectPicturePainter, PulseOffice.UI.Controls.Services.FormulaPicturePainter>());
        services.TryAddSingleton<PulseOffice.UI.Controls.Formulas.IFormulaRenderer>(_ => new PulseOffice.UI.Controls.Formulas.FormulaRenderer());
        services.TryAddSingleton<PulseOffice.Formulas.IFormulaParser>(_ => new PulseOffice.Formulas.FormulaParser());
        services.TryAddSingleton<PulseOffice.UI.Controls.Services.IEmbeddedObjectPicture, PulseOffice.UI.Controls.Services.EmbeddedObjectPicture>();

        return services;
    }
}
