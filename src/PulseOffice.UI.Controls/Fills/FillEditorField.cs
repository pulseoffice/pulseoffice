using PulseOffice.Drawing.Shapes;
using PulseOffice.Localization;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Fills;

public sealed record FillEditorField(string Key, string Label, IFillEditors Editors, ShapeFill Initial, FillEditorOptions Options)
    : CustomFormField(Key, Label)
{
    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var editor = Editors.Create(Initial, Options, metrics);
        return new FormFieldEditor(editor, () => editor.Fill) { Observe = changed => editor.FillChanged += (_, _) => changed() };
    }
}

public sealed record SolidBackgroundField(string Key, string Label, IFillEditors Editors, string? InitialHex)
    : CustomFormField(Key, Label)
{
    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var colors = new ColorFieldValue();
        var initial = colors.Normalize(InitialHex) is { Length: > 0 } hex
            ? new ShapeFill { Kind = ShapeFillKind.Solid, ColorHex = hex }
            : ShapeFill.None;
        var editor = Editors.Create(initial, new FillEditorOptions([FillPage.None, FillPage.Solid]) { ShowTransparency = false, ShowPreview = false }, metrics);
        return new FormFieldEditor(editor, () => editor.Fill.Kind == ShapeFillKind.None ? string.Empty : colors.Normalize(editor.Fill.ColorHex))
        {
            Observe = changed => editor.FillChanged += (_, _) => changed(),
        };
    }
}
