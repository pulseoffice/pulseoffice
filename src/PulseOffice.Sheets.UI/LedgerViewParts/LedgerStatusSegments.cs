using PulseOffice.Localization;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI.LedgerViewParts;

internal sealed class LedgerStatusSegments(
    IStatusBarService status, ITextCatalog texts, SheetSelection selection, ISelectionSummary summary, CalculationEngine engine)
{
    private readonly CircularReferenceReport _circularReport = new();
    private readonly StatusSegment _circular = status.Register("ledger.circular", StatusSegmentKind.Contextual);

    private readonly StatusSegment _summary = status.Register("ledger.summary", StatusSegmentKind.Contextual);
    private readonly StatusSegment _zoom = status.Register("ledger.zoom", StatusSegmentKind.Contextual);

    private bool _listening;

    private bool _refreshPending;

    public void Show(Worksheet? sheet, double zoom)
    {
        if (!_listening)
        {
            engine.Recalculated += OnRecalculated;
            _listening = true;
        }
        _summary.IsVisible = true;
        if (sheet is not null) UpdateSummary(sheet);
        ShowZoom(zoom);
    }

    public void Hide()
    {
        if (_listening)
        {
            engine.Recalculated -= OnRecalculated;
            _listening = false;
        }
        _summary.IsVisible = false;
        _zoom.IsVisible = false;
        _circular.IsVisible = false;
    }

    public void ShowZoom(double zoom)
    {
        _zoom.Text = texts.Format("status.zoom", (int)Math.Round(zoom * 100));
        _zoom.IsVisible = true;
    }

    public void UpdateSummary(Worksheet sheet)
    {
        UpdateCircular();
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

    private void OnRecalculated(object? sender, EventArgs e)
    {
        if (_refreshPending) return;
        _refreshPending = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _refreshPending = false;
            if (_listening) UpdateCircular();
        });
    }

    private void UpdateCircular()
    {
        var first = _circularReport.FirstCell(engine);
        _circular.Text = first is null ? string.Empty : texts.Format("status.circular", first);
        _circular.IsVisible = first is not null;
    }
}
