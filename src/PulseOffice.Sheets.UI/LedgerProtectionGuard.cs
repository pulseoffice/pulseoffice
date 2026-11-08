using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerProtectionGuard(
    IActiveSheetContext context,
    PulseOffice.Shell.IDocumentWorkspace workspace,
    IStatusBarService status,
    ITextCatalog texts,
    PulseOffice.Document.Commands.IHistoryReversals reversals) : ICommandEffectGuard
{
    public ICommandEffectCheck? Begin(CommandDefinition command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Module is not (ModuleKind.Sheets or ModuleKind.Shell)) return null;
        if (workspace.Active?.Module != ModuleKind.Sheets || context.Workbook is not { } workbook || context.History is not { } history) return null;
        var sheet = workbook.Sheets[context.SheetIndex];
        return sheet.Protection.IsEnabled ? new Check(this, workbook, sheet, ProtectedState.Of(sheet), history, reversals.For(command.Id.Value)) : null;
    }

    private static string? Violation(Workbook workbook, Worksheet sheet, ProtectedState before)
    {
        if (!sheet.Protection.IsEnabled) return null;
        var after = ProtectedState.Of(sheet);
        if (!after.Objects.SequenceEqual(before.Objects, ReferenceEqualityComparer.Instance)
            || !after.Sparklines.SequenceEqual(before.Sparklines, ReferenceEqualityComparer.Instance)) return "status.protection.objects";
        if (!after.Merges.SequenceEqual(before.Merges) || !after.RowBreaks.SequenceEqual(before.RowBreaks) || !after.ColumnBreaks.SequenceEqual(before.ColumnBreaks))
            return "status.protection.format";
        foreach (var address in before.Cells.Keys.Union(after.Cells.Keys))
        {
            before.Cells.TryGetValue(address, out var old);
            after.Cells.TryGetValue(address, out var current);
            if (Equals(old, current)) continue;
            if (workbook.Styles.Get(old?.StyleId ?? sheet.StyleIdAt(address)).Locked) return "status.protection.locked";
        }
        return null;
    }

    private void Refuse(PulseOffice.Document.Commands.UndoStack history, object? statePoint, PulseOffice.Document.Commands.IHistoryReversal move, string key)
    {
        move.Revert(history, statePoint);
        context.RequestRefresh();
        status.Report(texts[key], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
    }

    private sealed class Check(LedgerProtectionGuard guard, Workbook workbook, Worksheet sheet, ProtectedState before, PulseOffice.Document.Commands.UndoStack history, PulseOffice.Document.Commands.IHistoryReversal move) : ICommandEffectCheck
    {
        private readonly object? _statePoint = history.StatePoint;

        public bool Finish()
        {
            if (ReferenceEquals(history.StatePoint, _statePoint)) return false;
            if (Violation(workbook, sheet, before) is not { } key) return false;
            guard.Refuse(history, _statePoint, move, key);
            return true;
        }
    }

    private sealed record ProtectedState(
        IReadOnlyDictionary<CellAddress, Cell> Cells,
        IReadOnlyList<PlacedObject> Objects,
        IReadOnlyList<CellRange> Merges,
        IReadOnlyList<int> RowBreaks,
        IReadOnlyList<int> ColumnBreaks,
        IReadOnlyList<SparklineGroup> Sparklines)
    {
        public static ProtectedState Of(Worksheet sheet) => new(
            sheet.NonEmptyCells().ToDictionary(pair => pair.Key, pair => pair.Value),
            [.. sheet.Objects],
            [.. sheet.Merges.All],
            [.. sheet.RowBreaks],
            [.. sheet.ColumnBreaks],
            [.. sheet.Sparklines.Groups]);
    }
}
