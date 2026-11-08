using PulseOffice.Printing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Printing;

namespace PulseOffice.Sheets.UI;

public interface ISheetPrintDocumentBuilder
{
    IPrintableDocument? Build(string documentName);

    IPrintableDocument? Build(Workbook workbook, string documentName);

    IPrintableDocument? BuildRange(string documentName, Addressing.CellRange range);
}

public sealed class SheetPrintDocumentBuilder(
    IActiveSheetContext context,
    ICellPresenter presenter,
    IHeaderFooterFields fields,
    Engine.Operations.ConditionalFormattingEvaluator conditional,
    PulseOffice.UI.Controls.Charts.ChartRenderer charts,
    Engine.Operations.IChartDataExtractor chartData,
    PulseOffice.UI.Controls.Printing.IPageImageCapture capture,
    PulseOffice.UI.Controls.Shapes.IDrawingObjectRenderer drawings,
    PulseOffice.Drawing.Printing.IObjectPathBuilder paths,
    ISpreadsheetLocale locale,
    PulseOffice.Drawing.Printing.IOriginalImageSources originals,
    ISheetPrintOptionsFactory options) : ISheetPrintDocumentBuilder
{
    public IPrintableDocument? Build(string documentName) =>
        context.Workbook is { } workbook ? Build(workbook, context.Engine, documentName) : null;

    public IPrintableDocument? Build(Workbook workbook, string documentName) => Build(workbook, null, documentName);

    public IPrintableDocument? BuildRange(string documentName, Addressing.CellRange range)
    {
        if (context.Workbook is not { } workbook || context.SheetIndex < 0 || context.SheetIndex >= workbook.Sheets.Count) return null;

        var images = new SheetObjectRasterizer(workbook, charts, chartData, capture, drawings, paths, locale.Culture, originals);
        var part = Parts(workbook, context.Engine, context.SheetIndex, documentName, images, [range]).Single();
        return part.PageCount > 0 ? part : null;
    }

    private IPrintableDocument? Build(Workbook workbook, Engine.Recalculation.CalculationEngine? engine, string documentName)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var images = new SheetObjectRasterizer(workbook, charts, chartData, capture, drawings, paths, locale.Culture, originals);
        var numbering = new JobPageNumbering();
        var numbered = new List<NumberedPart>();

        for (var index = 0; index < workbook.Sheets.Count; index++)
        {
            if (workbook.Sheets[index].IsHidden) continue;
            var startsSection = true;
            foreach (var part in Parts(workbook, engine, index, documentName, images, numbering: numbering))
            {
                if (part.PageCount == 0) continue;
                numbered.Add(new NumberedPart(part, startsSection, part.Setup.FirstPageNumber));
                startsSection = false;
            }
        }

        if (numbered.Count == 0) return null;
        numbering.Number(numbered);
        return new CompositePrintDocument([.. numbered.Select(part => part.Document)]);
    }

    private IEnumerable<SheetPrintDocument> Parts(Workbook workbook, Engine.Recalculation.CalculationEngine? engine, int index, string documentName, SheetObjectRasterizer images, IReadOnlyList<Addressing.CellRange?>? chosen = null, IJobPageNumbering? numbering = null)
    {
        var sheet = workbook.Sheets[index];
        IConditionalAppearanceSource appearance = engine is not null
            ? new Engine.Operations.EngineConditionalAppearance(conditional, engine, index)
            : new NoConditionalAppearance();

        return _options.For(workbook, sheet, documentName, chosen).Select(options => new SheetPrintDocument(
            sheet, workbook.Styles, presenter, fields, options, conditional: appearance, images: images, numbering: numbering));
    }

    private readonly ISheetPrintOptionsFactory _options = options;
}
