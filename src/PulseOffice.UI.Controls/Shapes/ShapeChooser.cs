using PulseOffice.Drawing.Shapes;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Icons;

namespace PulseOffice.UI.Controls.Shapes;

public interface IShapeChooser
{
    Task<ShapeKind?> ChooseAsync(IReadOnlyList<ShapeTool> tools, CancellationToken ct = default);
}

public sealed class ShapeChooser(IDialogService dialogs, ITextCatalog texts, IIconCatalog icons, IRecentShapes recent) : IShapeChooser
{
    public async Task<ShapeKind?> ChooseAsync(IReadOnlyList<ShapeTool> tools, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tools);
        var model = new ShapePickerModel(texts, tools, recent.Keys);
        var chosen = await dialogs.ShowAsync<ShapeChoice>(
            new ShapePickerView(model, texts, icons),
            new DialogOptions { TitleKey = "dialog.shapeGallery.title", PreferredWidth = 760 },
            ct).ConfigureAwait(true);
        if (chosen is null) return null;

        recent.Remember(chosen.Key);
        if (chosen.Shape is { } kind) return kind;
        tools.FirstOrDefault(tool => tool.Key == chosen.Key)?.Run();
        return null;
    }
}
