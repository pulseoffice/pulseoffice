namespace PulseOffice.Sheets.UI;

public sealed class ActiveWorkbookDateEpoch(IActiveSheetContext active) : IDefaultDateEpoch
{
    public DateOnly Epoch => active.Workbook?.Calculation.DateEpoch ?? CalculationSettings.StandardDateEpoch;
}
