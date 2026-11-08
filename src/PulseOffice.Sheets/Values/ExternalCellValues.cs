namespace PulseOffice.Sheets.Values;

public interface IExternalCellValues
{
    CellValue Parse(string text);
}
