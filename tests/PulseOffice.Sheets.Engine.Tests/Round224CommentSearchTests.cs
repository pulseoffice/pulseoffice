using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round224CommentSearchTests
{
    private static readonly SearchOptions InComments = new() { InComments = true };

    [Fact]
    public void Find_in_comments_matches_comment_and_reply_text_but_not_cells()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("budget"));
        sheet.Comments[new CellAddress(1, 0)] = new CellComment { Text = "check the budget" };
        sheet.Comments[new CellAddress(2, 0)] = new CellComment { Text = "ok", Replies = [new CommentReply { Text = "Budget approved" }] };

        var matches = new WorkbookSearch().FindAll(workbook, 0, "budget", InComments);

        Assert.Equal([new CellAddress(1, 0), new CellAddress(2, 0)], matches.Select(match => match.Address));
    }

    [Fact]
    public void Replace_in_comments_changes_comment_text_in_one_undo_step()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("old"));
        sheet.Comments[new CellAddress(0, 0)] = new CellComment { Text = "old note", Replies = [new CommentReply { Text = "also old" }] };
        var history = new UndoStack();

        var replaced = new WorkbookSearch().ReplaceAll(workbook, 0, "old", "new", InComments, history: history);

        Assert.Equal(1, replaced);
        Assert.Equal("new note", sheet.Comments[new CellAddress(0, 0)].Text);
        Assert.Equal("also new", sheet.Comments[new CellAddress(0, 0)].Replies[0].Text);
        Assert.Equal("old", sheet[new CellAddress(0, 0)].Value.AsText());

        history.Undo();
        Assert.Equal("old note", sheet.Comments[new CellAddress(0, 0)].Text);
    }
}
