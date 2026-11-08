using Avalonia.Controls;
using PulseOffice.Documents.Editing;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Editing;

public interface IFolioContextMenus
{
    string RootFor(FolioEditor editor);

    bool Open(FolioEditor editor, Control host, Action? closed);

    bool IsMenuKey(Avalonia.Input.KeyEventArgs e);

    bool OpenAt(FolioEditor editor, Control host, Avalonia.Rect anchor, Action? closed);
}

public sealed class FolioContextMenus(IContextMenuFactory menus, ITableEditing tables, IContextMenuKey key) : IFolioContextMenus
{
    public const string TextRoot = "foliotext";

    public const string CellRoot = "foliocell";

    public const string ObjectRoot = "folioobject";

    public string RootFor(FolioEditor editor) => editor.SelectedObjectId is not null ? ObjectRoot : tables.Locate(editor.Document, editor.Caret.ParagraphId) is null ? TextRoot : CellRoot;

    public bool Open(FolioEditor editor, Control host, Action? closed) => menus.Open(RootFor(editor), host, closed);

    public bool IsMenuKey(Avalonia.Input.KeyEventArgs e) => key.Matches(e);

    public bool OpenAt(FolioEditor editor, Control host, Avalonia.Rect anchor, Action? closed) => menus.OpenAt(RootFor(editor), host, anchor, closed);
}
