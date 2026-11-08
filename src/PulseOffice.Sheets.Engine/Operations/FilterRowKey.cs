using System.Text;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public interface IFilterRowKey
{
    string Key(Worksheet sheet, int row, int firstColumn, int columnCount);
}

public sealed class FilterRowKey : IFilterRowKey
{
    private const char Separator = '\u001f';

    public string Key(Worksheet sheet, int row, int firstColumn, int columnCount)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var key = new StringBuilder();
        for (var column = firstColumn; column < firstColumn + columnCount; column++)
        {
            var value = sheet[new CellAddress(row, column)].Value;
            if (column > firstColumn) key.Append(Separator);
            key.Append(Tag(value.Kind)).Append(value.ToString());
        }
        return key.ToString();
    }

    private char Tag(ValueKind kind) => kind switch
    {
        ValueKind.Number => 'n',
        ValueKind.Text => 't',
        ValueKind.Boolean => 'b',
        ValueKind.Error => 'e',
        _ => '0',
    };
}
