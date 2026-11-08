using PulseOffice.Documents.UI.Canvas;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.UI.Commands;

namespace PulseOffice.Documents.UI.Commands;

public sealed record FolioViewCommandServices(IFolioViewLayout Layout, IFolioViewMarks Marks, IFolioSpotlight Spotlight, IFolioFieldNames FieldNames,
    PulseOffice.Documents.Layout.Flow.IHiddenParagraphs Hidden);

public sealed class FolioViewCommands(IActiveFolioContext context, FolioViewCommandServices view) : FolioCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Toggle("folio.view.printLayout", () => view.Layout.Mode == FolioViewMode.Print, () => view.Layout.Mode = FolioViewMode.Print),
        Toggle("folio.view.draft", () => view.Layout.Mode == FolioViewMode.Draft, () => view.Layout.Mode = view.Layout.Mode == FolioViewMode.Draft ? FolioViewMode.Print : FolioViewMode.Draft),
        Toggle("folio.view.web", () => view.Layout.Mode == FolioViewMode.Web, () => view.Layout.Mode = view.Layout.Mode == FolioViewMode.Web ? FolioViewMode.Print : FolioViewMode.Web),
        Toggle("folio.view.singlePage", () => view.Layout.Desk == DeskLayout.Single, () => view.Layout.Desk = DeskLayout.Single, printOnly: true),
        Toggle("folio.view.multiplePages", () => view.Layout.Desk == DeskLayout.Multiple, () => view.Layout.Desk = DeskLayout.Multiple, printOnly: true),
        Toggle("folio.view.book", () => view.Layout.Desk == DeskLayout.Book, () => view.Layout.Desk = DeskLayout.Book, printOnly: true),
        Toggle("folio.view.rulers", () => view.Layout.HorizontalRuler, () => view.Layout.HorizontalRuler = !view.Layout.HorizontalRuler) with { DefaultGesture = "Ctrl+Shift+R" },
        Toggle("folio.view.verticalRuler", () => view.Layout.VerticalRuler, () => view.Layout.VerticalRuler = !view.Layout.VerticalRuler),
        Toggle("folio.view.horizontalScrollBar", () => view.Layout.HorizontalScrollBar, () => view.Layout.HorizontalScrollBar = !view.Layout.HorizontalScrollBar),
        Toggle("folio.view.verticalScrollBar", () => view.Layout.VerticalScrollBar, () => view.Layout.VerticalScrollBar = !view.Layout.VerticalScrollBar),
        Toggle("folio.view.formattingMarks", () => view.Marks.FormattingMarks, () => view.Marks.FormattingMarks = !view.Marks.FormattingMarks) with { DefaultGesture = "Ctrl+F10" },
        Toggle("folio.view.boundaries", () => view.Marks.Boundaries, () => view.Marks.Boundaries = !view.Marks.Boundaries),
        Toggle("folio.view.images", () => view.Marks.Images, () => view.Marks.Images = !view.Marks.Images),
        Toggle("folio.view.whitespace", () => view.Layout.Whitespace, () => view.Layout.Whitespace = !view.Layout.Whitespace, printOnly: true),
        Toggle("folio.view.headerFooterMenu", () => view.Layout.HeaderFooterMenu, () => view.Layout.HeaderFooterMenu = !view.Layout.HeaderFooterMenu),
        Toggle("folio.view.grid", () => view.Layout.Grid, () => view.Layout.Grid = !view.Layout.Grid),
        Toggle("folio.view.snapToGrid", () => view.Layout.SnapToGrid, () => view.Layout.SnapToGrid = !view.Layout.SnapToGrid),
        Toggle("folio.view.helplines", () => view.Layout.Helplines, () => view.Layout.Helplines = !view.Layout.Helplines),
        Toggle("folio.view.baselineGrid", () => view.Layout.BaselineGrid, () => view.Layout.BaselineGrid = !view.Layout.BaselineGrid),
        Toggle("folio.view.selectionStandard", () => !view.Layout.BlockSelection, () => view.Layout.BlockSelection = false),
        Toggle("folio.view.selectionBlock", () => view.Layout.BlockSelection, () => view.Layout.BlockSelection = true),
        Toggle("folio.view.directCursor", () => view.Layout.DirectCursor, () => view.Layout.DirectCursor = !view.Layout.DirectCursor, printOnly: true),
        Toggle("folio.view.fieldNames", () => view.FieldNames.Show, () => view.FieldNames.Show = !view.FieldNames.Show) with { DefaultGesture = "Ctrl+F9" },
        Toggle("folio.view.hiddenParagraphs", () => view.Hidden.Show, () =>
        {
            view.Hidden.Show = !view.Hidden.Show;
            Editor?.Reflow();
        }),
        Toggle("folio.view.spotlightParagraphStyles", () => view.Spotlight.ParagraphStyles, () => view.Spotlight.ParagraphStyles = !view.Spotlight.ParagraphStyles),
        Toggle("folio.view.spotlightCharacterStyles", () => view.Spotlight.CharacterStyles, () => view.Spotlight.CharacterStyles = !view.Spotlight.CharacterStyles),
        Toggle("folio.view.spotlightDirect", () => view.Spotlight.DirectFormatting, () => view.Spotlight.DirectFormatting = !view.Spotlight.DirectFormatting),
    ];

    private CommandDefinition Toggle(string id, Func<bool> on, Action flip, bool printOnly = false) =>
        Viewing(id) with
        {
            EnabledWhen = _ => Editor is not null && (!printOnly || view.Layout.Mode == FolioViewMode.Print),
            CheckedWhen = _ => on(),
            Execute = (_, _) =>
            {
                flip();
                return Task.CompletedTask;
            },
        };
}
