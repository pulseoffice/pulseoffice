using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI.LedgerViewParts;

internal sealed class LedgerStatusSegments(IStatusBarService status, ITextCatalog texts, SheetSelection selection, ISelectionSummary summary)
{
    private readonly StatusSegment _summary = status.Register("ledger.summary", StatusSegmentKind.Contextual);
    private readonly StatusSegment _zoom = status.Register("ledger.zoom", StatusSegmentKind.Contextual);

    public void Show(Worksheet? sheet, double zoom)
    {
        _summary.IsVisible = true;
        if (sheet is not null) UpdateSummary(sheet);
        ShowZoom(zoom);
    }

    public void Hide()
    {
        _summary.IsVisible = false;
        _zoom.IsVisible = false;
    }

    public void ShowZoom(double zoom)
    {
        _zoom.Text = texts.Format("status.zoom", (int)Math.Round(zoom * 100));
        _zoom.IsVisible = true;
    }

    public void UpdateSummary(Worksheet sheet)
    {
        if (selection.IsSingleCell)
        {
            _summary.Text = string.Empty;
            return;
        }

        var totals = summary.Summarize(sheet, selection.Range);

        _summary.Text = totals.Numbers == 0
            ? texts.Format("status.summary.count", totals.Filled)
            : texts.Format("status.summary.full", totals.Sum, totals.Average, totals.Filled);
    }
}
