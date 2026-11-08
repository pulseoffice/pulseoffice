using PulseOffice.Document.Forms;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Drawing;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI.LedgerViewParts;

internal sealed class LedgerObjectEditing(
    Workbook workbook,
    SheetGridView grid,
    PulseOffice.Document.Commands.UndoStack history,
    IStatusBarService status,
    ITextCatalog texts,
    ISheetConnectors connectors,
    IFormControlBinding formBinding,
    LedgerCellWriter writer)
{
    public IOptionGroups OptionGroups { get; set; } = new OptionGroups();

    public IFormEventRunner? FormEvents { get; set; }

    public DocumentState ObjectFacts(DocumentState state, PlacedObject? placed)
    {
        state &= ~(DocumentState.ObjectSelected
            | DocumentState.ObjectGroupSelected
            | DocumentState.ManyObjectsSelected);

        if (placed is null) return state;

        state |= DocumentState.ObjectSelected;

        if (placed.Content is Drawing.Shapes.ShapeGroupObject) state |= DocumentState.ObjectGroupSelected;
        if (grid.SelectedObjects.Count > 1) state |= DocumentState.ManyObjectsSelected;

        return state;
    }

    public void DeleteObjects(IReadOnlyList<PlacedObject> objects)
    {
        var sheet = workbook.Sheets[grid.SheetIndex];
        if (!CanEditObjects(sheet)) return;

        var removed = objects.Where(sheet.Objects.Contains).ToList();
        if (removed.Count == 0) return;

        foreach (var placed in removed) sheet.Objects.Remove(placed);

        history.Push(new PulseOffice.Document.Commands.DelegateAction(
            texts["history.object.delete"],
            redo: () =>
            {
                foreach (var placed in removed) sheet.Objects.Remove(placed);
            },
            undo: () =>
            {
                foreach (var placed in removed) sheet.Objects.Add(placed);
            }));

        grid.SelectObject(null);
        grid.InvalidateVisual();
    }

    public PlacedObject? ReplaceShape(Worksheet sheet, PlacedObject placed, Drawing.Shapes.ShapeObject shape, string description)
    {
        if (!CanEditObjects(sheet)) return null;
        var current = sheet.Objects.FirstOrDefault(candidate => candidate.Id == placed.Id);
        if (current is null) return null;
        var replacement = current with { Content = shape };
        var previous = current;
        ReplaceById(sheet, replacement);
        history.Push(new PulseOffice.Document.Commands.DelegateAction(
            description,
            redo: () => ReplaceById(sheet, replacement),
            undo: () => ReplaceById(sheet, previous)));
        grid.Refresh();
        return replacement;
    }

    public void InsertFreeform(FreeformCompletedEventArgs completed)
    {
        var sheet = workbook.Sheets[grid.SheetIndex];
        if (!CanEditObjects(sheet)) return;

        var id = Guid.NewGuid().ToString("N");

        var placed = new PlacedObject
        {
            Id = id,
            Anchor = completed.Anchor,
            OffsetX = completed.OffsetX,
            OffsetY = completed.OffsetY,
            Size = completed.Size,
            Content = new Drawing.Freeform.FreeformObject
            {
                Id = id,
                Path = completed.Path,
                Style = completed.Filled
                    ? Drawing.Shapes.ShapeStyle.Default
                    : Drawing.Shapes.ShapeStyle.Default with
                    {
                        Fill = Drawing.Shapes.ShapeStyle.Default.Fill with
                        {
                            Kind = Drawing.Shapes.ShapeFillKind.None,
                        },
                    },
            },
        };

        sheet.Objects.Add(placed);

        history.Push(new PulseOffice.Document.Commands.DelegateAction(
            texts["history.draw.insert"],
            redo: () => sheet.Objects.Add(placed),
            undo: () => sheet.Objects.Remove(placed)));

        grid.SelectObject(placed);
        grid.Refresh();
    }

    public void ApplyFreeform(FreeformEditedEventArgs edited)
    {
        var sheet = workbook.Sheets[grid.SheetIndex];
        if (!CanEditObjects(sheet)) return;

        var index = sheet.Objects.FindIndex(candidate => candidate.Id == edited.Placed.Id);
        if (index < 0 || sheet.Objects[index] is not { Content: Drawing.Freeform.FreeformObject freeform } current) return;

        var replacement = current with { Content = freeform with { Path = edited.Path } };
        var previous = current;

        sheet.Objects[index] = replacement;
        connectors.Refresh(sheet);

        history.Push(new PulseOffice.Document.Commands.DelegateAction(
            texts["history.draw.editPoints"],
            redo: () => ReplaceById(sheet, replacement),
            undo: () => ReplaceById(sheet, previous)));

        grid.SelectObject(replacement);
        grid.Refresh();
    }

    public void ApplyAdjustment(ShapeAdjustedEventArgs adjusted)
    {
        var sheet = workbook.Sheets[grid.SheetIndex];
        if (!CanEditObjects(sheet)) return;

        var index = sheet.Objects.FindIndex(candidate => candidate.Id == adjusted.Placed.Id);
        if (index < 0 || sheet.Objects[index] is not { Content: Drawing.Shapes.ShapeObject } current) return;

        var replacement = current with { Content = adjusted.Shape };
        var previous = current;

        sheet.Objects[index] = replacement;
        connectors.Refresh(sheet);

        history.Push(new PulseOffice.Document.Commands.DelegateAction(
            texts["history.draw.adjustShape"],
            redo: () => ReplaceById(sheet, replacement),
            undo: () => ReplaceById(sheet, previous)));

        grid.SelectObject(replacement);
        grid.Refresh();
    }

    public void ApplyFormControl(FormControlInteractionEventArgs change)
    {
        if (!writer.CanEdit()) return;
        var sheet = workbook.Sheets[grid.SheetIndex];

        var index = sheet.Objects.FindIndex(candidate => candidate.Id == change.Placed.Id);
        if (index < 0 || sheet.Objects[index] is not { Content: FormControlObject form } current) return;

        if (!CanPublish(sheet, form.ControlKind, change.Properties))
        {
            grid.Refresh();
            return;
        }

        var replacement = current with
        {
            Content = form with { Properties = change.Properties },
        };

        var previous = current;

        using (history.BeginTransaction(texts["history.form.control"]))
        {
            sheet.Objects[index] = replacement;

            history.Push(new PulseOffice.Document.Commands.DelegateAction(
                texts["history.form.control"],
                redo: () => ReplaceById(sheet, replacement),
                undo: () => ReplaceById(sheet, previous)));

            PublishFormControl(sheet, form.ControlKind, change.Properties);

            ReleaseOptionSiblings(sheet, index);
        }

        grid.Refresh();
        _ = RaiseFormEventsAsync(form, change.Properties);
    }

    public void ApplyObjectChanges(IReadOnlyList<SheetGridView.ObjectChange> changes)
    {
        if (changes.Count == 0) return;

        var sheet = workbook.Sheets[grid.SheetIndex];
        if (!CanEditObjects(sheet)) return;

        using var transaction = history.BeginTransaction(texts["history.object.change"]);

        var updated = new List<PlacedObject>(changes.Count);

        foreach (var change in changes)
        {
            var index = sheet.Objects.FindIndex(candidate => candidate.Id == change.Object.Id);
            if (index < 0) continue;

            var content = change.RotationDegrees is { } degrees
                ? Rotated(change.Object.Content, degrees)
                : change.Object.Content;

            var replacement = change.Object with
            {
                Content = content,
                Anchor = change.Anchor,
                OffsetX = change.OffsetX,
                OffsetY = change.OffsetY,
                Size = change.Size,
            };

            var previous = change.Object;

            sheet.Objects[index] = replacement;
            connectors.Refresh(sheet);
            updated.Add(replacement);

            history.Push(new PulseOffice.Document.Commands.DelegateAction(
                texts["history.object.change"],
                redo: () => ReplaceById(sheet, replacement),
                undo: () => ReplaceById(sheet, previous)));
        }

        grid.SelectObjects(updated);
        grid.Refresh();
    }

    public bool CanEditObjects(Worksheet sheet)
    {
        if (!writer.CanEdit()) return false;
        if (!sheet.Protection.IsEnabled) return true;

        status.Report(texts["status.protection.objects"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
        grid.Refresh();
        return false;
    }

    private void ReleaseOptionSiblings(Worksheet sheet, int chosen)
    {
        var controls = sheet.Objects.Select(placed => placed.Content as FormControlObject).ToList();

        foreach (var release in OptionGroups.Release(controls, chosen))
        {
            if (sheet.Objects[release.Index] is not { Content: FormControlObject sibling } previous) continue;

            var index = release.Index;
            var replacement = previous with { Content = sibling with { Properties = release.Released } };
            sheet.Objects[index] = replacement;

            history.Push(new PulseOffice.Document.Commands.DelegateAction(
                texts["history.form.control"],
                redo: () => ReplaceById(sheet, replacement),
                undo: () => ReplaceById(sheet, previous)));

            PublishFormControl(sheet, sibling.ControlKind, release.Released);
        }
    }

    private async Task RaiseFormEventsAsync(
        FormControlObject form, FormControlProperties changed)
    {
        if (FormEvents is null) return;

        await FormEvents.RaiseAsync(changed, FormControlEvent.Clicked).ConfigureAwait(true);

        if (!Equals(changed.Value, form.Properties.Value))
            await FormEvents.RaiseAsync(changed, FormControlEvent.ValueChanged).ConfigureAwait(true);
    }

    private void PublishFormControl(
        Worksheet sheet, FormControlKind kind, FormControlProperties control)
    {
        if (formBinding.Publish(kind, control, workbook) is not { } write) return;

        var owner = write.Sheet ?? sheet;
        var target = new SheetCell(workbook.IndexOf(owner), write.Address);
        var previous = owner[write.Address];

        writer.WriteValidated(target, previous with { Formula = null, Value = write.Value });
    }

    private bool CanPublish(Worksheet sheet, FormControlKind kind, FormControlProperties control)
    {
        if (formBinding.Publish(kind, control, workbook) is not { } write) return true;

        var owner = write.Sheet ?? sheet;
        if (writer.IsLocked(owner, write.Address))
        {
            status.Report(texts["status.protection.locked"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return false;
        }

        return !writer.Refused(new SheetCell(workbook.IndexOf(owner), write.Address), write.Value);
    }

    private void ReplaceById(Worksheet sheet, PlacedObject placed)
    {
        var index = sheet.Objects.FindIndex(candidate => candidate.Id == placed.Id);
        if (index < 0) return;

        sheet.Objects[index] = placed;
        connectors.Refresh(sheet);
    }

    private static Document.Embedding.EmbeddedObject Rotated(
        Document.Embedding.EmbeddedObject content, double degrees) => content switch
    {
        Drawing.Shapes.ShapeObject shape => shape with { RotationDegrees = degrees },
        Drawing.Shapes.ShapeGroupObject group => group with { RotationDegrees = degrees },
        Drawing.Assets.VectorGraphicObject vector => vector with { RotationDegrees = degrees },
        _ => content,
    };
}
