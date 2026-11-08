using PulseOffice.Foundation.Modules;

namespace PulseOffice.UI.Commands;

[Flags]
public enum SelectionKind
{
    None = 0,
    TextCaret = 1 << 0,
    TextRange = 1 << 1,
    Cell = 1 << 2,
    CellRange = 1 << 3,
    Row = 1 << 4,
    Column = 1 << 5,
    Table = 1 << 6,
    Shape = 1 << 7,
    Image = 1 << 8,
    Chart = 1 << 9,
    Frame = 1 << 10,
    Slide = 1 << 11,
    Comment = 1 << 12,
    Field = 1 << 13,

    AnyText = TextCaret | TextRange,
    AnyCell = Cell | CellRange | Row | Column,
    AnyObject = Shape | Image | Chart | Frame,
}

[Flags]
public enum DocumentState
{
    None = 0,
    DocumentOpen = 1 << 0,
    Modified = 1 << 1,
    ReadOnly = 1 << 2,
    CanUndo = 1 << 3,
    CanRedo = 1 << 4,
    TrackingChanges = 1 << 5,
    HasSelection = 1 << 6,
    ClipboardHasContent = 1 << 7,
    CompatibilityMode = 1 << 8,
    ObjectSelected = 1 << 9,
    ObjectGroupSelected = 1 << 10,
    ManyObjectsSelected = 1 << 11,
}

public sealed record CommandContext(
    ModuleKind Module,
    SelectionKind Selection,
    DocumentState State)
{
    public object? Argument { get; init; }

    public static readonly CommandContext Empty =
        new(ModuleKind.Shell, SelectionKind.None, DocumentState.None);

    public bool Has(DocumentState flag) => (State & flag) == flag;
    public bool HasAnySelection(SelectionKind mask) => (Selection & mask) != 0;

    public bool IsReadOnly => Has(DocumentState.ReadOnly);
    public bool HasDocument => Has(DocumentState.DocumentOpen);
}
