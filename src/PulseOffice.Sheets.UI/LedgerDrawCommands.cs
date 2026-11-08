using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Drawing;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerDrawCommands(IActiveSheetContext context, IDrawingToolState tools) : LedgerCommandSet(context)
{
    private static readonly (string Id, DrawingTool Tool, bool Filled)[] Pens =
    [
        ("ledger.draw.polygon", DrawingTool.Polygon, false),
        ("ledger.draw.polygonFilled", DrawingTool.Polygon, true),
        ("ledger.draw.polygonConstrained", DrawingTool.PolygonConstrained, false),
        ("ledger.draw.polygonConstrainedFilled", DrawingTool.PolygonConstrained, true),
        ("ledger.draw.curve", DrawingTool.Curve, false),
        ("ledger.draw.curveFilled", DrawingTool.Curve, true),
        ("ledger.draw.freehand", DrawingTool.Freehand, false),
        ("ledger.draw.freehandFilled", DrawingTool.Freehand, true),
    ];

    public IEnumerable<CommandDefinition> GetCommands()
    {
        foreach (var (id, tool, filled) in Pens)
        {
            var chosen = tool;
            var fill = filled;

            yield return Prototype(id) with
            {
                IconKey = id,
                MenuPath = "insert/draw",
                Surface = CommandSurface.Advanced,
                RequiredState = DocumentState.DocumentOpen,
                ForbiddenState = DocumentState.ReadOnly,
                EnabledWhen = _ => Context.HasTarget,
                Execute = (_, _) =>
                {
                    tools.Choose(chosen, fill);
                    return Task.CompletedTask;
                },
            };
        }

        yield return Prototype("ledger.draw.editPoints") with
        {
            IconKey = "ledger.draw.editPoints",
            MenuPath = "insert/draw",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.SelectedObject?.Content is Drawing.Freeform.FreeformObject,
            Execute = (_, _) =>
            {
                tools.ToggleEditingPoints();
                return Task.CompletedTask;
            },
        };

        yield return Prototype("ledger.draw.helplines") with
        {
            MenuPath = "view/aids",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.HasTarget,
            CheckedWhen = _ => tools.HelplinesWhileMoving,
            Execute = (_, _) =>
            {
                tools.ToggleHelplines();
                return Task.CompletedTask;
            },
        };
    }
}
