using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Sheets.UI.DataDialogs;

public interface ILedgerDialogProbes
{
    IConsolidatePreviewer Consolidation(Workbook workbook, int sheetIndex);

    IMultipleOperationsPreviewer MultipleOperations(Engine.Recalculation.CalculationEngine engine, int sheetIndex, CellRange target);

    ILabelsProbe Labels(Workbook workbook, int sheetIndex);

    IRandomSampler Random { get; }

    IShufflePreviewer Shuffle(Workbook workbook, int sheetIndex);

    IGoalSeekProbe GoalSeek(Engine.Recalculation.CalculationEngine engine, int sheetIndex);

    IStatisticsProbe Statistics(Workbook workbook, int sheetIndex);

    IScenarioProbe Scenarios(Workbook workbook, int sheetIndex);
}

public sealed class LedgerDialogProbes(
    ISheetConsolidation consolidation,
    IMultipleOperations operations,
    ISheetRandomFill random,
    IGoalSeek goalSeek,
    IStatisticsProcedures statistics,
    Dialogs.ISheetReferenceReader reader,
    ISketchCells cells) : ILedgerDialogProbes
{
    public IRandomSampler Random { get; } = new RandomSampler(random);

    public IConsolidatePreviewer Consolidation(Workbook workbook, int sheetIndex) =>
        new WorkbookConsolidatePreviewer(consolidation, reader, cells, workbook, sheetIndex);

    public IMultipleOperationsPreviewer MultipleOperations(Engine.Recalculation.CalculationEngine engine, int sheetIndex, CellRange target) =>
        new EngineMultipleOperationsPreviewer(operations, reader, cells, engine, sheetIndex, target);

    public ILabelsProbe Labels(Workbook workbook, int sheetIndex) => new SheetLabelsProbe(reader, cells, workbook, sheetIndex);

    public IShufflePreviewer Shuffle(Workbook workbook, int sheetIndex) => new SheetShufflePreviewer(random, reader, cells, workbook, sheetIndex);

    public IGoalSeekProbe GoalSeek(Engine.Recalculation.CalculationEngine engine, int sheetIndex) =>
        new EngineGoalSeekProbe(goalSeek, reader, cells, engine, sheetIndex);

    public IStatisticsProbe Statistics(Workbook workbook, int sheetIndex) => new SheetStatisticsProbe(statistics, reader, workbook, sheetIndex);

    public IScenarioProbe Scenarios(Workbook workbook, int sheetIndex) => new SheetScenarioProbe(reader, cells, workbook, sheetIndex);
}
