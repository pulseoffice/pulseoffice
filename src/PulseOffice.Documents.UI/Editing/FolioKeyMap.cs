using Avalonia.Input;
using PulseOffice.Documents.Editing;
using PulseOffice.UI.Controls.Text;

namespace PulseOffice.Documents.UI.Editing;

public abstract class FolioKeyAction
{
    public abstract void Apply(FolioView view);
}

public sealed class MotionKeyAction(CaretMotion motion, bool extend) : FolioKeyAction
{
    public override void Apply(FolioView view) => view.Editor.Move(motion, extend, view.Canvas.VisibleLines());
}

public sealed class EditKeyAction(Func<FolioEditor, bool> edit) : FolioKeyAction
{
    public override void Apply(FolioView view) => edit(view.Editor);
}

public sealed class TextKeyAction(TextEditingAction action) : FolioKeyAction
{
    public override void Apply(FolioView view) => view.Perform(action);
}

public sealed class ToggleKeyAction(CharacterToggle toggle) : FolioKeyAction
{
    public override void Apply(FolioView view) => view.Editor.Toggle(toggle);
}

public sealed class ListLevelKeyAction(IListEditing lists, int delta, Func<FolioEditor, bool> otherwise) : FolioKeyAction
{
    public override void Apply(FolioView view)
    {
        var editor = view.Editor;
        if (AtListItemStart(editor))
        {
            if (!editor.RefusesEdit()) editor.Change(() => lists.ShiftLevel(editor.Document, editor.History, editor.ParagraphRange(), delta, false));
            return;
        }
        otherwise(editor);
    }

    private bool AtListItemStart(FolioEditor editor) =>
        editor.CurrentParagraph().ListStyleName is not null
        && (editor.HasSelection ? editor.Selection.Start.ParagraphId != editor.Selection.End.ParagraphId : editor.Caret.Offset == 0);
}

public interface IFolioKeyMap
{
    FolioKeyAction? Resolve(Key key, KeyModifiers modifiers);
}

public sealed class FolioKeyMap : IFolioKeyMap
{
    private readonly Dictionary<(Key Key, KeyModifiers Modifiers), FolioKeyAction> _actions = [];

    public FolioKeyMap(IListEditing lists)
    {
        Motion(Key.Left, KeyModifiers.None, CaretMotion.Left);
        Motion(Key.Right, KeyModifiers.None, CaretMotion.Right);
        Motion(Key.Up, KeyModifiers.None, CaretMotion.Up);
        Motion(Key.Down, KeyModifiers.None, CaretMotion.Down);
        Motion(Key.Home, KeyModifiers.None, CaretMotion.LineStart);
        Motion(Key.End, KeyModifiers.None, CaretMotion.LineEnd);
        Motion(Key.PageUp, KeyModifiers.None, CaretMotion.PageUp);
        Motion(Key.PageDown, KeyModifiers.None, CaretMotion.PageDown);
        Motion(Key.Left, KeyModifiers.Alt, CaretMotion.WordLeft);
        Motion(Key.Right, KeyModifiers.Alt, CaretMotion.WordRight);
        Motion(Key.Left, KeyModifiers.Control, CaretMotion.WordLeft);
        Motion(Key.Right, KeyModifiers.Control, CaretMotion.WordRight);
        Motion(Key.Left, KeyModifiers.Meta, CaretMotion.LineStart);
        Motion(Key.Right, KeyModifiers.Meta, CaretMotion.LineEnd);
        Motion(Key.Up, KeyModifiers.Meta, CaretMotion.DocumentStart);
        Motion(Key.Down, KeyModifiers.Meta, CaretMotion.DocumentEnd);
        Motion(Key.Home, KeyModifiers.Control, CaretMotion.DocumentStart);
        Motion(Key.End, KeyModifiers.Control, CaretMotion.DocumentEnd);

        Add(Key.Back, KeyModifiers.None, new EditKeyAction(editor => editor.Backspace()));
        Add(Key.Back, KeyModifiers.Shift, new EditKeyAction(editor => editor.Backspace()));
        Add(Key.Delete, KeyModifiers.None, new EditKeyAction(editor => editor.DeleteForward()));
        Add(Key.Enter, KeyModifiers.None, new EditKeyAction(editor => editor.SplitParagraph()));
        Add(Key.Enter, KeyModifiers.Shift, new EditKeyAction(editor => editor.LineBreak()));
        Add(Key.Tab, KeyModifiers.None, new ListLevelKeyAction(lists, 1, editor => editor.MoveToCell(false) || editor.Type("\t")));
        Add(Key.Tab, KeyModifiers.Shift, new ListLevelKeyAction(lists, -1, editor => editor.MoveToCell(true)));
        Add(Key.Tab, KeyModifiers.Control, new EditKeyAction(editor => editor.Type("\t")));

        foreach (var primary in new[] { KeyModifiers.Control, KeyModifiers.Meta })
        {
            Add(Key.A, primary, new TextKeyAction(TextEditingAction.SelectAll));
            Add(Key.C, primary, new TextKeyAction(TextEditingAction.Copy));
            Add(Key.X, primary, new TextKeyAction(TextEditingAction.Cut));
            Add(Key.V, primary, new TextKeyAction(TextEditingAction.Paste));
            Add(Key.Z, primary, new TextKeyAction(TextEditingAction.Undo));
            Add(Key.Z, primary | KeyModifiers.Shift, new TextKeyAction(TextEditingAction.Redo));
            Add(Key.Y, primary, new TextKeyAction(TextEditingAction.Redo));
            Add(Key.B, primary, new ToggleKeyAction(CharacterToggle.Bold));
            Add(Key.I, primary, new ToggleKeyAction(CharacterToggle.Italic));
            Add(Key.U, primary, new ToggleKeyAction(CharacterToggle.Underline));
        }
    }

    public FolioKeyAction? Resolve(Key key, KeyModifiers modifiers) => _actions.GetValueOrDefault((key, modifiers));

    private void Motion(Key key, KeyModifiers modifiers, CaretMotion motion)
    {
        Add(key, modifiers, new MotionKeyAction(motion, extend: false));
        Add(key, modifiers | KeyModifiers.Shift, new MotionKeyAction(motion, extend: true));
    }

    private void Add(Key key, KeyModifiers modifiers, FolioKeyAction action) => _actions[(key, modifiers)] = action;
}
