using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Controls.References;

namespace PulseOffice.Sheets.UI.Dialogs;

public enum ReferenceQualification { WhenOtherSheet, Always }

public sealed class SheetReferencePickTarget(
    IActiveSheetContext context,
    ISheetRangeReferences references,
    ReferenceShape shape,
    ReferenceQualification qualification = ReferenceQualification.WhenOtherSheet) : IReferencePickTarget
{
    public IDisposable Begin(string current, IReferencePickListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        if (context.Workbook is not { } workbook) return new Session(() => { });

        var originSheet = context.SheetIndex;
        var originSelection = context.Selection;
        var originObjects = context.SelectedObjects.ToList();

        Preselect(workbook, current, originSheet);

        var sink = new Sink(this, listener, workbook, originSheet, KeptRanges(current));
        var mode = context.BeginReferencePick(sink);

        return new Session(() =>
        {
            mode?.Dispose();
            if (!context.HasTarget || !ReferenceEquals(context.Workbook, workbook)) return;
            if (context.SheetIndex != originSheet) context.SelectSheet(originSheet);
            context.SelectRange(originSelection);
            if (originObjects.Count > 0) context.SelectObjects(originObjects);
        });
    }

    public string Describe(Workbook workbook, int sheetIndex, CellRange selection, int originSheet)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var address = shape == ReferenceShape.Cell ? selection.Start.ToA1() : selection.ToString();
        return qualification == ReferenceQualification.Always
            ? references.Qualify(workbook, sheetIndex, address, currentSheet: -1)
            : references.Qualify(workbook, sheetIndex, address, originSheet);
    }

    public string Compose(IReadOnlyList<string> kept, string picked) =>
        kept.Count == 0 ? picked : string.Join("; ", kept.Append(picked));

    private IReadOnlyList<string> KeptRanges(string current) =>
        shape == ReferenceShape.RangeList
            ? [.. (current ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).SkipLast(1)]
            : [];

    private void Preselect(Workbook workbook, string current, int originSheet)
    {
        var text = shape == ReferenceShape.RangeList
            ? (current ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault()
            : current;

        if (string.IsNullOrWhiteSpace(text)) return;
        if (references.Parse(workbook, text, originSheet) is not { } reference) return;

        if (reference.SheetIndex != context.SheetIndex) context.SelectSheet(reference.SheetIndex);
        context.SelectRange(reference.Range);
    }

    private sealed class Sink(
        SheetReferencePickTarget owner, IReferencePickListener listener, Workbook workbook, int originSheet,
        IReadOnlyList<string> kept)
        : ISheetReferencePickSink
    {
        public void SelectionChanged() =>
            listener.SelectionChanged(owner.Compose(
                kept, owner.Describe(workbook, owner.CurrentSheet, owner.CurrentSelection, originSheet)));

        public void Commit() => listener.Commit();

        public void Cancel() => listener.Cancel();
    }

    private int CurrentSheet => context.SheetIndex;

    private CellRange CurrentSelection => context.Selection;

    private sealed class Session(Action end) : IDisposable
    {
        private bool _ended;

        public void Dispose()
        {
            if (_ended) return;
            _ended = true;
            end();
        }
    }
}
