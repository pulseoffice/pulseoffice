using PulseOffice.Document.Tables;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.UI.Editing;

namespace PulseOffice.Documents.UI.Commands;

public interface IFolioTableLooks
{
    void Apply(FolioEditor editor, TableBlock table, TableLook look, bool headerRow, TableBanding banding);
}

public sealed class FolioTableLooks(FolioTableEditing editing) : IFolioTableLooks
{
    public void Apply(FolioEditor editor, TableBlock table, TableLook look, bool headerRow, TableBanding banding)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(look);
        ArgumentNullException.ThrowIfNull(banding);
        using (editor.History.BeginTransaction("apply-table-style"))
        {
            editing.Formatting.EnsurePreset(editor.Document, editor.History, look, look.Name);
            editing.Formatting.ApplyStyle(editor.Document, editor.History, table, look.Name);
            editing.Formatting.SetHeaderRows(editor.Document, editor.History, table, headerRow ? Math.Max(1, table.HeaderRows) : 0, table.RepeatHeaderRows);
            editing.Formatting.SetBanding(editor.Document, editor.History, table, banding);
        }
    }
}
