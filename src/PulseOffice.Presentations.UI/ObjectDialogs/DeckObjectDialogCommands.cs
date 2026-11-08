using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Charts;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Presentations.UI.ObjectDialogs;

public interface IDeckObjectEditing
{
    bool TryEditSelection();

    Task EditStyleAsync(CancellationToken ct);
}

public sealed class DeckObjectDialogCommands(
    IChartEditor charts,
    IChartSampleData chartSamples,
    PulseOffice.Charts.IChartFamilyCatalog chartFamilies,
    IActiveDeckContext context,
    IDialogService dialogs,
    IShapeStyling styling,
    PulseOffice.UI.Controls.Shapes.IShapeStyleEditors editors,
    PulseOffice.UI.Controls.Objects.IAltTextPrompt altText,
    ITextCatalog texts,
    PulseOffice.Text.Rich.ITextCultures cultures,
    PulseOffice.UI.Icons.IIconCatalog? icons = null) : IDeckObjectEditing
{
    private IEnumerable<DeckObjectDialog> Dialogs()
    {
        yield return new ShapeStyleDialog("deck.object.style", 0, CommandSurface.Common, styling, editors, context, dialogs, texts);
        yield return new ShapeStyleDialog("deck.object.area", 0, CommandSurface.Advanced, styling, editors, context, dialogs, texts);
        yield return new ShapeStyleDialog("deck.object.line", 1, CommandSurface.Advanced, styling, editors, context, dialogs, texts);
        yield return new ShapeStyleDialog("deck.object.effects", 2, CommandSurface.Advanced, styling, editors, context, dialogs, texts);
        yield return new ShapeStyleDialog("deck.object.textAttributes", 3, CommandSurface.Advanced, styling, editors, context, dialogs, texts);
        yield return new PositionSizeDialog(context, dialogs, texts, icons);
        yield return new AltTextDialog(altText, context, dialogs, texts);
        yield return new ChartEditDialog(charts, new InlineChartData(chartSamples, chartFamilies), context, dialogs, texts, cultures);
    }

    public bool TryEditSelection()
    {
        if (Dialogs().FirstOrDefault(dialog => dialog.EditsOnDoubleClick && dialog.CanEditSelection) is not { } dialog) return false;

        _ = dialog.RunAsync(CancellationToken.None);
        return true;
    }

    public Task EditStyleAsync(CancellationToken ct) => Dialogs().First(dialog => dialog.Id == "deck.object.style").RunAsync(ct);

    public IEnumerable<CommandDefinition> GetCommands() => Dialogs().Select(dialog => dialog.ToCommand());
}
