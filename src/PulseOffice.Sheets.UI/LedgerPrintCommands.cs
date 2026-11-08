using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Foundation.Pages;
using System.Globalization;
using PulseOffice.Formats.Pdf;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Printing;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Printing;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerPrintCommands(
    IActiveSheetContext context,
    IPaperCatalog papers,
    ISheetPrintDocumentBuilder builder,
    IDialogService dialogs,
    IStatusBarService status,
    PageLayout.SheetPageSetupServices pageServices,
    Dialogs.ISheetReferenceSources sources,
    Dialogs.ISheetReferenceReader reader,
    PulseOffice.Shell.IDocumentWorkspace workspace,
    ITextCatalog texts) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("ledger.print.pageSetup") with
        {
            MenuPath = "format/page",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, ct) => PageSetupAsync(ct),
        },
    ];

    public IPrintableDocument? BuildDocument(string documentName) => builder.Build(documentName);

    private async Task PageSetupAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var current = workbook.PageStyles.For(sheet).Setup;

        var input = new PageLayout.SheetPageSetupInput(
            current, papers.All,
            string.Join(' ', sheet.PrintRanges.Select(range => range.ToString())),
            sheet.RepeatedPrintRows, sheet.RepeatedPrintColumns,
            sheet.Name, workspace.Active?.Title ?? sheet.Name,
            sources.For(Dialogs.ReferenceShape.RangeList));
        var view = new PageLayout.SheetPageSetupView(input, pageServices, texts);

        var result = await dialogs.ShowAsync<PageLayout.SheetPageSetupResult>(
            view.Build(),
            new DialogOptions { TitleKey = "dialog.pageSetup.title", PreferredWidth = 760 }, ct)
            .ConfigureAwait(true);

        if (result is null) return;

        IReadOnlyList<CellRange> areas = [];
        if (result.PrintArea.Length > 0)
        {
            if (reader.Read(workbook, result.PrintArea, Context.SheetIndex, Dialogs.ReferenceShape.RangeList) is not { } references
                || references.Any(reference => reference.SheetIndex != Context.SheetIndex))
            {
                status.Report(texts["status.analysis.badReference"], NotificationSeverity.Error);
                return;
            }
            areas = [.. references.Select(reference => reference.Range)];
        }

        var setup = result.Setup;
        if (setup.ContentArea.Width.Value <= 0 || setup.ContentArea.Height.Value <= 0)
        {
            status.Report(texts["status.page.marginsTooLarge"], NotificationSeverity.Error);
            return;
        }
        var areasBefore = sheet.PrintRanges.ToList();
        var rowsBefore = sheet.RepeatedPrintRows;
        var columnsBefore = sheet.RepeatedPrintColumns;
        var sheetChanged = !areasBefore.SequenceEqual(areas) || rowsBefore != result.RepeatRows || columnsBefore != result.RepeatColumns;
        if (setup == current && !sheetChanged) return;

        var previousName = sheet.PageStyleName;
        var name = workbook.PageStyles.OwnNameFor(sheet, workbook.Sheets);
        var previousStyle = workbook.PageStyles.Find(name);

        void Areas(IEnumerable<CellRange> ranges, int rows, int columns)
        {
            sheet.PrintRanges.Clear();
            sheet.PrintRanges.AddRange(ranges);
            sheet.RepeatedPrintRows = rows;
            sheet.RepeatedPrintColumns = columns;
        }

        void Redo()
        {
            if (setup != current)
            {
                workbook.PageStyles.Define(name, setup);
                sheet.PageStyleName = name;
            }
            Areas(areas, result.RepeatRows, result.RepeatColumns);
        }

        void Undo()
        {
            if (setup != current)
            {
                if (previousStyle is not null) workbook.PageStyles.Define(previousStyle.Name, previousStyle.Setup, previousStyle.IsBuiltIn);
                else workbook.PageStyles.Remove(name);
                sheet.PageStyleName = previousName;
            }
            Areas(areasBefore, rowsBefore, columnsBefore);
        }

        Redo();

        Context.History?.Push(new PulseOffice.Document.Commands.DelegateAction(
            texts["dialog.pageSetup.title"], redo: Redo, undo: Undo));

        status.Report(texts.Format("status.page.saved", sheet.Name));
    }
}
