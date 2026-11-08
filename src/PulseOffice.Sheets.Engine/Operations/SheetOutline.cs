using PulseOffice.Sheets;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record DetectedOutline(
    IReadOnlyList<OutlineGroup> Rows, IReadOnlyList<OutlineGroup> Columns);

public interface ISheetOutline
{
    OutlineGroup Group(Worksheet sheet, StructureAxis axis, int first, int last);

    void Ungroup(Worksheet sheet, StructureAxis axis, int first, int last);

    void Collapse(Worksheet sheet, StructureAxis axis, int index);

    void Expand(Worksheet sheet, StructureAxis axis, int index);

    void ShowLevel(Worksheet sheet, StructureAxis axis, int level);

    DetectedOutline Detect(Worksheet sheet, Addressing.CellRange range);
}

public sealed class SheetOutline : ISheetOutline
{
    public DetectedOutline Detect(Worksheet sheet, Addressing.CellRange range)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var rows = new List<OutlineGroup>();
        var start = -1;

        for (var row = range.Start.Row; row <= range.End.Row; row++)
        {
            var isTotal = IsTotalRow(sheet, range, row);

            if (!isTotal)
            {
                if (start < 0) start = row;
                continue;
            }

            if (start >= 0 && row - 1 >= start) rows.Add(new OutlineGroup { First = start, Last = row - 1 });

            start = -1;
        }

        return new DetectedOutline(rows, []);
    }

    private bool IsTotalRow(Worksheet sheet, Addressing.CellRange range, int row)
    {
        for (var column = range.Start.Column; column <= range.End.Column; column++)
        {
            var formula = sheet[new Addressing.CellAddress(row, column)].Formula;

            if (formula is { Length: > 0 }
                && (formula.Contains("SUBTOTAL", StringComparison.OrdinalIgnoreCase)
                    || formula.Contains("SUM(", StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    public OutlineGroup Group(Worksheet sheet, StructureAxis axis, int first, int last)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        if (last < first) (first, last) = (last, first);

        var groups = GroupsOf(sheet, axis);

        var level = 1 + groups
            .Where(group => group.First <= first && group.Last >= last)
            .Select(group => group.Level)
            .DefaultIfEmpty(0)
            .Max();

        var created = new OutlineGroup { First = first, Last = last, Level = level };

        groups.Add(created);
        groups.Sort((left, right) => left.First != right.First
            ? left.First.CompareTo(right.First)
            : left.Level.CompareTo(right.Level));

        return created;
    }

    public void Ungroup(Worksheet sheet, StructureAxis axis, int first, int last)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var groups = GroupsOf(sheet, axis);

        for (var index = groups.Count - 1; index >= 0; index--)
        {
            var group = groups[index];
            if (group.Last < first || group.First > last) continue;

            if (group.IsCollapsed) Expand(sheet, axis, index);
            groups.RemoveAt(index);
        }
    }

    public void Collapse(Worksheet sheet, StructureAxis axis, int index)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var groups = GroupsOf(sheet, axis);
        var group = groups[index];

        if (group.IsCollapsed) return;

        using var deferral = sheet.Layout.DeferRowVisibilityNotifications();
        if (axis == StructureAxis.Rows) sheet.Layout.HideRows(group.First, group.Last);
        else sheet.Layout.HideColumns(group.First, group.Last);

        groups[index] = group with { IsCollapsed = true };
    }

    public void Expand(Worksheet sheet, StructureAxis axis, int index)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var groups = GroupsOf(sheet, axis);
        var group = groups[index];

        if (!group.IsCollapsed) return;

        using var deferral = sheet.Layout.DeferRowVisibilityNotifications();
        if (axis == StructureAxis.Rows) sheet.Layout.ShowRows(group.First, group.Last);
        else sheet.Layout.ShowColumns(group.First, group.Last);

        groups[index] = group with { IsCollapsed = false };
    }

    public void ShowLevel(Worksheet sheet, StructureAxis axis, int level)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var groups = GroupsOf(sheet, axis);
        using var deferral = sheet.Layout.DeferRowVisibilityNotifications();

        for (var index = 0; index < groups.Count; index++)
        {
            if (groups[index].Level > level) Collapse(sheet, axis, index);
            else Expand(sheet, axis, index);
        }
    }

    private List<OutlineGroup> GroupsOf(Worksheet sheet, StructureAxis axis) =>
        axis == StructureAxis.Rows ? sheet.RowGroups : sheet.ColumnGroups;
}
