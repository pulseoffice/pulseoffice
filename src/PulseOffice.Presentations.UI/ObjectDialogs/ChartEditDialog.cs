using PulseOffice.Charts;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Charts;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Presentations.UI.ObjectDialogs;

public sealed class ChartEditDialog(
    IChartEditor editor,
    IInlineChartData data,
    IActiveDeckContext context,
    IDialogService dialogs,
    ITextCatalog texts,
    PulseOffice.Text.Rich.ITextCultures cultures)
    : DeckObjectDialog(context, dialogs, texts)
{
    public override string Id => "deck.chart.edit";

    public override bool EditsOnDoubleClick => true;

    protected override CommandSurface Surface => CommandSurface.Advanced | CommandSurface.ContextMenu;

    protected override string HistoryKey => "deck.action.chartEdit";

    protected override bool Accepts(SlideElement element) => element is ObjectElement { Content: ChartDefinition };

    protected override async Task<SlideElement?> EditAsync(SlideElement element, Presentation deck, CancellationToken ct)
    {
        var target = (ObjectElement)element;
        var chart = (ChartDefinition)target.Content;

        var updated = await editor.EditAsync(chart, data.Source(cultures.For(deck.TextLanguage)), ct).ConfigureAwait(true);

        return updated is null ? null : target with { Content = data.Settle(updated) };
    }
}
