using PulseOffice.Drawing.Shapes;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerShapeColourCommands(
    IActiveSheetContext context,
    IShapeStyling styling,
    IShapeColourCommands colours,
    IRibbonPaletteBuilder palettes,
    ITextCatalog texts,
    ShapeText.ILedgerShapeFitting? fitting = null) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands()
    {
        var target = new ShapeColourTarget(SelectedStyle, Restyle, () => palettes.Build(palettes.OfficeTheme));
        return
        [
            colours.Fill(Shape("ledger.object.fill"), target),
            colours.Outline(Shape("ledger.object.lineColor"), target),
        ];
    }

    private CommandDefinition Shape(string id) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectSelected,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => ObjectsEditable && SelectedStyle() is not null,
        };

    private ShapeStyle? SelectedStyle() =>
        Context.SelectedObject is { } placed ? styling.Editable(placed.Content)?.Style : null;

    private void Restyle(string key, Func<ShapeStyle, ShapeStyle> change)
    {
        if (Context.Workbook is not { } workbook || !ObjectsEditable || Context.SelectedObject is not { } placed) return;
        if (Context.SheetIndex < 0 || Context.SheetIndex >= workbook.Sheets.Count) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var index = sheet.Objects.IndexOf(placed);
        if (index < 0 || styling.Restyle(placed.Content, change) is not { } content || Equals(content, placed.Content)) return;

        var replacement = (fitting ?? new ShapeText.LedgerShapeFitting()).Fit(placed with { Content = content });
        sheet.Objects[index] = replacement;

        Context.History?.Push(new Document.Commands.DelegateAction(
            texts[$"command.{key}.title"],
            redo: () => sheet.Objects[index] = replacement,
            undo: () => sheet.Objects[index] = placed));

        Context.SelectObject(replacement);
        Context.RequestRefresh();
    }
}
