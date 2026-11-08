using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.UI.GridParts;

internal sealed class GridNotifications(object sender)
{
    public event EventHandler<CellAddress>? EditRequested;
    public event EventHandler<string>? TypingStarted;
    public event EventHandler<CellAddress>? ChoicesRequested;
    public event EventHandler<SheetGridView.FillRequest>? FillRequested;
    public event EventHandler<int>? FilterRequested;
    public event EventHandler? ReferenceCommitRequested;
    public event EventHandler? ReferenceCancelRequested;
    public event EventHandler<string>? ContextMenuRequested;
    public event EventHandler<PlacedObject>? ObjectActivated;
    public event EventHandler<string>? ObjectLinkFollowed;
    public event EventHandler<ShapeTextRequest>? ShapeTextRequested;
    public event EventHandler<PlacedObject?>? SelectedObjectChanged;
    public event EventHandler<FormControlInteractionEventArgs>? FormControlChanged;
    public event EventHandler<FreeformCompletedEventArgs>? FreeformCompleted;
    public event EventHandler<FreeformEditedEventArgs>? FreeformEdited;
    public event EventHandler<ShapeAdjustedEventArgs>? ShapeAdjusted;
    public event EventHandler<IReadOnlyList<SheetGridView.ObjectChange>>? ObjectChangeRequested;
    public event EventHandler<CellRange>? ClearRequested;
    public event EventHandler<IReadOnlyList<PlacedObject>>? ObjectsDeleteRequested;
    public event EventHandler<SheetGridView.ResizeRequest>? ResizeRequested;

    public void Edit(CellAddress address) => EditRequested?.Invoke(sender, address);

    public void Typing(string typed) => TypingStarted?.Invoke(sender, typed);

    public void Choices(CellAddress address) => ChoicesRequested?.Invoke(sender, address);

    public void Fill(SheetGridView.FillRequest request) => FillRequested?.Invoke(sender, request);

    public void Filter(int column) => FilterRequested?.Invoke(sender, column);

    public void CommitReference() => ReferenceCommitRequested?.Invoke(sender, EventArgs.Empty);

    public void CancelReference() => ReferenceCancelRequested?.Invoke(sender, EventArgs.Empty);

    public void ContextMenu(string area) => ContextMenuRequested?.Invoke(sender, area);

    public void Activate(PlacedObject placed) => ObjectActivated?.Invoke(sender, placed);

    public void FollowLink(string link) => ObjectLinkFollowed?.Invoke(sender, link);

    public void ShapeText(ShapeTextRequest request) => ShapeTextRequested?.Invoke(sender, request);

    public void SelectedObject(PlacedObject? placed) => SelectedObjectChanged?.Invoke(sender, placed);

    public void FormControl(FormControlInteractionEventArgs change) => FormControlChanged?.Invoke(sender, change);

    public void Freeform(FreeformCompletedEventArgs completed) => FreeformCompleted?.Invoke(sender, completed);

    public void FreeformEdit(FreeformEditedEventArgs edited) => FreeformEdited?.Invoke(sender, edited);

    public void Adjusted(ShapeAdjustedEventArgs adjusted) => ShapeAdjusted?.Invoke(sender, adjusted);

    public void ObjectChanges(IReadOnlyList<SheetGridView.ObjectChange> changes) => ObjectChangeRequested?.Invoke(sender, changes);

    public void Clear(CellRange range) => ClearRequested?.Invoke(sender, range);

    public void DeleteObjects(IReadOnlyList<PlacedObject> objects) => ObjectsDeleteRequested?.Invoke(sender, objects);

    public void Resize(SheetGridView.ResizeRequest request) => ResizeRequested?.Invoke(sender, request);
}
