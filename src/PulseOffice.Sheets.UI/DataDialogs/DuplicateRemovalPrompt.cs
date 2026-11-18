using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Lists;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed record DuplicateRemovalChoice(bool HasHeader, IReadOnlyList<int> KeyColumns);

public interface IDuplicateRemovalPrompt
{
    Task<DuplicateRemovalChoice?> AskAsync(Worksheet sheet, CellRange range, CancellationToken ct);
}

public sealed class DuplicateRemovalPrompt(IDialogService dialogs, IHeaderRowDetector headers, ITextCatalog texts)
    : IDuplicateRemovalPrompt
{
    public async Task<DuplicateRemovalChoice?> AskAsync(Worksheet sheet, CellRange range, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var labels = Enumerable.Range(range.Start.Column, range.ColumnCount)
            .Select(column => Label(sheet, range.Start.Row, column))
            .ToList();

        var answers = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(texts["duplicates.explanation"],
            [
                new CheckField("header", texts["tools.hasHeader"], headers.LooksLikeHeader(sheet, range)),
                new ChecklistField("columns", texts["duplicates.columns"], labels, labels),
            ], texts),
            new DialogOptions { TitleKey = "dialog.removeDuplicates.title", PreferredWidth = 420 }, ct)
            .ConfigureAwait(true);

        if (answers is null) return null;

        var chosen = answers.GetValueOrDefault("columns") as IReadOnlyList<string> ?? [];
        var columns = labels
            .Select((label, offset) => (label, column: range.Start.Column + offset))
            .Where(entry => chosen.Contains(entry.label))
            .Select(entry => entry.column)
            .ToList();

        return new DuplicateRemovalChoice(answers.GetValueOrDefault("header") is true, columns);
    }

    private string Label(Worksheet sheet, int row, int column)
    {
        var letter = CellAddress.ColumnName(column);
        var value = sheet.GetValue(new CellAddress(row, column));
        return value.IsEmpty ? texts.Format("duplicates.column", letter) : texts.Format("duplicates.columnNamed", letter, value.ToString());
    }
}
