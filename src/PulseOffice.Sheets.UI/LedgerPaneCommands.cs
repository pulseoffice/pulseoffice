using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Foundation.Modules;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using PulseOffice.Sheets.Styling;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.Drawing.Assets;
using PulseOffice.Platform;
using PulseOffice.UI.Controls.Search;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerPaneCommands(
    ISheetFormattingService formatting,
    IActiveSheetContext context,
    IStatusBarService status,
    PulseOffice.UI.Controls.Zoom.IZoomLadder zoomLadder,
    ITextCatalog texts) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() => ViewCommands();

    private IEnumerable<CommandDefinition> ViewCommands() =>
    [
        View("ledger.view.freezeAtSelection",
            () =>
            {
                var start = Context.Selection.Start;
                if (start.Row == 0 && start.Column == 0)
                {
                    status.Report(texts["status.panes.atOrigin"], NotificationSeverity.Warning);
                    return;
                }
                Context.FreezePanes(start.Row, start.Column);
            }),

        View("ledger.view.freezeFirstRow", () => Context.FreezePanes(1, 0)),

        View("ledger.view.freezeFirstColumn", () => Context.FreezePanes(0, 1)),

        View("ledger.view.unfreeze", () => Context.FreezePanes(0, 0),
            () => Context.Workbook is { } workbook && workbook.Sheets[Context.SheetIndex].Frozen.IsFrozen),

        View("ledger.view.splitAtSelection",
            () =>
            {
                var start = Context.Selection.Start;
                if (start.Row == 0 && start.Column == 0)
                {
                    status.Report(texts["status.panes.atOrigin"], NotificationSeverity.Warning);
                    return;
                }
                Context.SplitPanes(start.Row, start.Column);
            }),

        View("ledger.view.unsplit", () => Context.SplitPanes(0, 0), () => Context.PanesAreSplit),

        Zoom("ledger.view.zoomIn", "Ctrl+Plus", () => zoomLadder.StepIn(Context.Zoom)),
        Zoom("ledger.view.zoomOut", "Ctrl+Minus", () => zoomLadder.StepOut(Context.Zoom)),
        Zoom("ledger.view.zoomReset", "Ctrl+0", () => 1),
    ];

    private CommandDefinition Zoom(string id, string gesture, Func<double> zoom) =>
        Prototype(id) with
        {
            MenuPath = "view/zoom",
            DefaultGesture = gesture,
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => formatting.CanFormat,
            Execute = (_, _) =>
            {
                Context.SetZoom(Math.Round(zoom(), 2));
                return Task.CompletedTask;
            },
        };

    private CommandDefinition View(string id, Action action, Func<bool>? applies = null) =>
        Prototype(id) with
        {
            MenuPath = "view/freeze",
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => formatting.CanFormat && (applies?.Invoke() ?? true),
            Execute = (_, _) =>
            {
                action();
                return Task.CompletedTask;
            },
        };
}
