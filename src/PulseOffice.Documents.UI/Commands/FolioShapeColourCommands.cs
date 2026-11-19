using PulseOffice.Documents.Objects;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Drawing.Shapes;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioShapeColourCommands(
    IActiveFolioContext context,
    IFolioObjectEditing objects,
    IShapeStyling styling,
    IShapeColourCommands colours,
    IRibbonPaletteBuilder palettes) : FolioCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands()
    {
        var target = new ShapeColourTarget(SelectedStyle, Restyle, () => palettes.Build(palettes.OfficeTheme));
        return
        [
            colours.Fill(Shape("folio.object.fill"), target),
            colours.Outline(Shape("folio.object.lineColor"), target),
        ];
    }

    private CommandDefinition Shape(string id) =>
        Editing(id) with
        {
            Surface = CommandSurface.Common,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && SelectedStyle() is not null,
        };

    private EmbeddedObject? Content(FolioEditor editor) =>
        objects.Selected(editor) is { Content: EmbeddedContent { Value: var value } } ? value : null;

    private ShapeStyle? SelectedStyle() =>
        Editor is { } editor && Content(editor) is { } content ? styling.Editable(content)?.Style : null;

    private void Restyle(string key, Func<ShapeStyle, ShapeStyle> change)
    {
        if (Editor is { } editor) objects.Replace(editor, value => styling.Restyle(value, change) ?? value);
    }
}
