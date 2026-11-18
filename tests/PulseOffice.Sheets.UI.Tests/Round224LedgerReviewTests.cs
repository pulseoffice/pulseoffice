using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round224LedgerReviewTests
{
    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection().AddSheetsEngine().BuildServiceProvider()
            .GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    private static readonly PulseOffice.Localization.ITextCatalog Texts =
        new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);

    [Fact]
    public void The_review_panel_replies_to_resolves_and_deletes_replies_of_a_cell_comment()
    {
        var (context, workbook) = Round224SheetContext.Create(Engines);
        workbook.Sheets[0].Comments[CellAddress.First] = new CellComment { Text = "Question", Author = "Ada" };
        var review = new LedgerReviewSource(context, null!, new PulseOffice.Foundation.Review.AuthorPalette(), Texts, new Round224SheetContext.Identity());

        review.Reply("0:A1", "Answer @Ada");
        review.ToggleResolved("0:A1");

        var comment = workbook.Sheets[0].Comments[CellAddress.First];
        Assert.True(comment.IsResolved);
        Assert.Equal(("Answer @Ada", "Bob"), (comment.Replies[0].Text, comment.Replies[0].Author));
        Assert.Contains(review.Comments, item => item.Id == "0:A1#0" && item.Depth == 1);

        review.Delete(["0:A1#0"]);
        Assert.Empty(workbook.Sheets[0].Comments[CellAddress.First].Replies);

        context.History!.Undo();
        Assert.Single(workbook.Sheets[0].Comments[CellAddress.First].Replies);
    }
}

internal static class Round224SheetContext
{
    public sealed class Identity : PulseOffice.Foundation.Identity.IUserIdentity
    {
        public string Name => "Bob";

        public string Initials => "B";
    }

    public static (ActiveSheetContext Context, Workbook Workbook) Create(Engine.Recalculation.ICalculationEngineFactory engines, Func<CellRange>? selection = null)
    {
        var workbook = Workbook.CreateDefault();
        var context = new ActiveSheetContext();
        var zoom = 1.0;
        context.Attach(new SheetTarget
        {
            Workbook = workbook,
            SheetIndex = 0,
            History = new UndoStack(),
            Engine = engines.Create(workbook),
            Selection = selection ?? (() => CellRange.Single(CellAddress.First)),
            WriteSelection = _ => { },
            ReadViewOption = _ => false,
            WriteViewOption = (_, _) => { },
            Refresh = () => { },
            FreezePanes = (_, _) => { },
            SplitPanes = (_, _) => { },
            ReadPanesSplit = () => false,
            ApplyRowFilter = (_, _) => { },
            ReadZoom = () => zoom,
            WriteZoom = value => zoom = value,
            ReadSelectedObject = () => null,
            ReadSelectedObjects = () => [],
            WriteSelectedObjects = _ => { },
            SelectSheet = _ => { },
            WriteSelectedObject = _ => { },
        });
        return (context, workbook);
    }
}
