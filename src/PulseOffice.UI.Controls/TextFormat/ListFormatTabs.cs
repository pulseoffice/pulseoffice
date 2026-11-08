using PulseOffice.Localization;

namespace PulseOffice.UI.Controls.TextFormat;

public interface IListFormatTabs
{
    FormTab Tab(ListFormatRequest request, IReadOnlyList<FormField> extra);

    ListLevelsAnswer Read(ListFormatRequest request, IReadOnlyDictionary<string, object> values);
}

public interface IListFormatDialog
{
    Task<ListLevelsAnswer?> AskAsync(ListFormatRequest request, string titleKey, CancellationToken ct);
}

public sealed class ListFormatTabs(IListLevelsEditors editors, IListSampleRows rows, ITextFormatPreviews previews, ITextCatalog texts) : IListFormatTabs
{
    public const string LevelsKey = "levels";

    public FormTab Tab(ListFormatRequest request, IReadOnlyList<FormField> extra)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(extra);
        var explanation = request.Mode == ListSampleMode.Headings ? texts["textFormat.list.headings.explanation"] : texts["textFormat.list.explanation"];
        return new FormTab(texts["textFormat.list.tab"], explanation,
            [.. extra, new ListLevelsField(LevelsKey, texts["textFormat.list.tab"], request, editors)])
        {
            LivePreview = feed => previews.Create(feed, new FeedPreview<ListLevelsAnswer>(
                values => Read(request, values),
                answer =>
                {
                    var view = new ListSampleView(rows, 220, 170);
                    view.Show(answer.Levels, rows.Pattern(request.Mode, answer.Levels.Count), request.Mode, answer.Level);
                    return view;
                },
                PulseOffice.UI.Controls.Preview.PreviewBackdrop.Page,
                190)),
        };
    }

    public ListLevelsAnswer Read(ListFormatRequest request, IReadOnlyDictionary<string, object> values)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(values);
        return new FormAnswers(values).Value<ListLevelsAnswer>(LevelsKey) ?? new ListLevelsAnswer(request.Levels, request.Level);
    }
}

public sealed class ListFormatDialog(IListFormatTabs tabs, ITabbedForms forms) : IListFormatDialog
{
    public async Task<ListLevelsAnswer?> AskAsync(ListFormatRequest request, string titleKey, CancellationToken ct) =>
        await forms.AskAsync(titleKey, [tabs.Tab(request, [])], ct).ConfigureAwait(true) is { } values ? tabs.Read(request, values) : null;
}
