using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Recalculation;

public interface IPivotDataSource
{
    CellValue? Lookup(
        int sheetIndex, CellAddress target, string dataField,
        IReadOnlyList<(string Field, string Item)> filters);
}

public interface IWebResourceAccess
{
    string? Fetch(string url);
}
