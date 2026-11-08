using PulseOffice.Drawing.Text;

namespace PulseOffice.Sheets;

public interface IWorkbookFontNames
{
    IReadOnlyCollection<string> Used(Workbook workbook);
}

public sealed class WorkbookFontNames(IObjectFontNames objects) : IWorkbookFontNames
{
    public WorkbookFontNames() : this(new ObjectFontNames())
    {
    }

    public IReadOnlyCollection<string> Used(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var style in workbook.Styles.Styles) names.Add(style.Font.Family);
        foreach (var placed in workbook.Sheets.SelectMany(sheet => sheet.Objects))
            foreach (var name in objects.Of(placed.Content)) names.Add(name);

        names.RemoveWhere(string.IsNullOrWhiteSpace);
        return names;
    }
}
