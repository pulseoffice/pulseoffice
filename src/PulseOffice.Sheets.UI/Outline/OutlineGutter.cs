using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Sheets.UI.Outline;

public enum OutlineButtonKind { Level, Toggle }

public sealed record OutlineButton(StructureAxis Axis, OutlineButtonKind Kind, int Level)
{
    public int Index { get; init; } = -1;

    public int GroupIndex { get; init; } = -1;

    public bool Collapsed { get; init; }

    public int SpanFirst { get; init; } = -1;

    public int SpanLast { get; init; } = -1;
}

public interface IOutlineGutter
{
    double SlotSize { get; }

    int Depth(Worksheet sheet, StructureAxis axis);

    double Thickness(Worksheet sheet, StructureAxis axis);

    double SlotOffset(int level);

    IReadOnlyList<OutlineButton> Levels(Worksheet sheet, StructureAxis axis);

    IReadOnlyList<OutlineButton> Toggles(Worksheet sheet, StructureAxis axis);

    OutlineButton? LevelAt(Worksheet sheet, StructureAxis axis, double across);

    OutlineButton? ToggleAt(Worksheet sheet, StructureAxis axis, double across, int index);

    void Press(Worksheet sheet, OutlineButton button);
}

public sealed class OutlineGutter(ISheetOutline outline) : IOutlineGutter
{
    private const double Slot = 14;
    private const double Padding = 2;

    public OutlineGutter() : this(new SheetOutline())
    {
    }

    public double SlotSize => Slot;

    public int Depth(Worksheet sheet, StructureAxis axis)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return GroupsOf(sheet, axis).Select(group => group.Level).DefaultIfEmpty(0).Max();
    }

    public double Thickness(Worksheet sheet, StructureAxis axis) =>
        Depth(sheet, axis) is var depth and > 0 ? (depth + 1) * Slot + Padding : 0;

    public double SlotOffset(int level) => Padding / 2 + (level - 1) * Slot;

    public IReadOnlyList<OutlineButton> Levels(Worksheet sheet, StructureAxis axis) =>
        [.. Enumerable.Range(1, Depth(sheet, axis) is var depth and > 0 ? depth + 1 : 0)
            .Select(level => new OutlineButton(axis, OutlineButtonKind.Level, level))];

    public IReadOnlyList<OutlineButton> Toggles(Worksheet sheet, StructureAxis axis)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var groups = GroupsOf(sheet, axis);
        var after = axis == StructureAxis.Rows ? sheet.OutlineSummary.Below : sheet.OutlineSummary.Right;
        return [.. groups.Select((group, index) => new OutlineButton(axis, OutlineButtonKind.Toggle, group.Level)
        {
            Index = after || group.First == 0 ? group.Last + 1 : group.First - 1,
            GroupIndex = index,
            Collapsed = group.IsCollapsed,
            SpanFirst = group.First,
            SpanLast = group.Last,
        })];
    }

    public OutlineButton? LevelAt(Worksheet sheet, StructureAxis axis, double across) =>
        SlotAt(across) is var level && level >= 1 && level <= Depth(sheet, axis) + 1 && Depth(sheet, axis) > 0
            ? new OutlineButton(axis, OutlineButtonKind.Level, level)
            : null;

    public OutlineButton? ToggleAt(Worksheet sheet, StructureAxis axis, double across, int index)
    {
        var level = SlotAt(across);
        return Toggles(sheet, axis).FirstOrDefault(button => button.Level == level && button.Index == index);
    }

    public void Press(Worksheet sheet, OutlineButton button)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(button);

        if (button.Kind == OutlineButtonKind.Level)
        {
            outline.ShowLevel(sheet, button.Axis, button.Level - 1);
            return;
        }

        if (button.GroupIndex < 0 || button.GroupIndex >= GroupsOf(sheet, button.Axis).Count) return;
        if (GroupsOf(sheet, button.Axis)[button.GroupIndex].IsCollapsed) outline.Expand(sheet, button.Axis, button.GroupIndex);
        else outline.Collapse(sheet, button.Axis, button.GroupIndex);
    }

    private int SlotAt(double across) => across < Padding / 2 ? 0 : (int)Math.Floor((across - Padding / 2) / Slot) + 1;

    private List<OutlineGroup> GroupsOf(Worksheet sheet, StructureAxis axis) =>
        axis == StructureAxis.Rows ? sheet.RowGroups : sheet.ColumnGroups;
}
