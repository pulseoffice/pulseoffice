using PulseOffice.Charts;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Charts;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioChartCommands(
    IActiveFolioContext context,
    IFolioObjectEditing objects,
    IChartEditor charts,
    IInlineChartData data) : FolioCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Editing("folio.chart.edit") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredSelection = SelectionKind.Chart,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && Chart(editor) is not null,
            Execute = (_, ct) => EditAsync(ct),
        },
    ];

    private ChartDefinition? Chart(FolioEditor editor) =>
        objects.Selected(editor) is { Content: EmbeddedContent { Value: ChartDefinition chart } } ? chart : null;

    private async Task EditAsync(CancellationToken ct)
    {
        if (Editor is not { SelectedObjectId: { } id } editor || Chart(editor) is not { } chart) return;
        if (await charts.EditAsync(chart, data.Source(editor.Services.Cultures.For(editor.Document.Language)), ct).ConfigureAwait(true) is not { } updated) return;
        if (editor.Document.Objects.Find(id) is null) return;

        editor.SelectObject(id);
        objects.Replace(editor, content => content is ChartDefinition ? data.Settle(updated) : content);
    }
}
