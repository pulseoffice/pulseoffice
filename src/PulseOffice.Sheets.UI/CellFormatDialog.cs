using PulseOffice.Localization;
using PulseOffice.Sheets.Styling;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public interface ICellFormatDialog
{
    Task ShowAsync(FormatCellsTab tab, CancellationToken ct);
}

public sealed class CellFormatDialog(
    IActiveSheetContext context,
    ISheetFormattingService formatting,
    IDialogService dialogs,
    PulseOffice.UI.Controls.Fonts.IFontPickerFactory fonts,
    ITextCatalog texts,
    ICellStyleDelta delta,
    PulseOffice.UI.Controls.Borders.IBorderEditors borders,
    PulseOffice.UI.Controls.Fills.IFillEditors fills,
    ICellBorderFrames frames,
    PulseOffice.Document.Borders.IBorderFrameResolver resolver,
    CellFormat.FormatCellsServices services) : ICellFormatDialog
{
    public async Task ShowAsync(FormatCellsTab tab, CancellationToken ct)
    {
        if (context.Workbook is not { } workbook) return;

        var original = formatting.CurrentStyle;
        var selection = context.Selection;
        var active = selection.Start;
        var value = workbook.Sheets[context.SheetIndex].GetValue(active);
        var view = new FormatCellsView(original, workbook.NumberFormats, texts, fonts, borders, fills, frames, selection.RowCount > 1, selection.ColumnCount > 1,
            services, value, active.ToA1(), resolver);

        var updated = await dialogs.ShowAsync<FormatCellsResult>(
            view.Build(tab),
            new DialogOptions { TitleKey = "command.ledger.format.cells.title", PreferredWidth = 860 },
            ct).ConfigureAwait(true);

        if (updated is null) return;

        formatting.ApplyToSelection(
            texts["command.ledger.format.cells.title"],
            (range, address, current) =>
            {
                var styled = delta.Apply(original, updated.Style, current);
                var placement = resolver.Place(address.Row, address.Column, range.Start.Row, range.Start.Column, range.End.Row, range.End.Column);
                var edits = resolver.Resolve(updated.BordersBefore, updated.BordersAfter, placement);
                return edits.IsEmpty ? styled : styled with { Borders = frames.Apply(styled.Borders, edits) };
            });
    }
}
