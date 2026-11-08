using PulseOffice.Document.Tables;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.UI.Editing;

namespace PulseOffice.Documents.UI.Commands;

public interface IFolioTableLooks
{
    void Apply(FolioEditor editor, TableBlock table, TableLook look, bool headerRow, bool bandedRows);
}

public sealed class FolioTableLooks(FolioTableEditing editing, ITableStylePresets presets) : IFolioTableLooks
{
    public void Apply(FolioEditor editor, TableBlock table, TableLook look, bool headerRow, bool bandedRows)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(look);
        var applied = bandedRows ? look : look with { BandFill = null };
        var name = bandedRows || look.BandFill is null ? applied.Name : presets.Unbanded(look);
        using (editor.History.BeginTransaction("apply-table-style"))
        {
            editing.Formatting.EnsurePreset(editor.Document, editor.History, applied, name);
            editing.Formatting.ApplyStyle(editor.Document, editor.History, table, name);
            editing.Formatting.SetHeaderRows(editor.Document, editor.History, table, headerRow ? Math.Max(1, table.HeaderRows) : 0, table.RepeatHeaderRows);
        }
    }
}
