using PulseOffice.Document.Commands;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round221CommentPasteTests
{
    private static CellAddress A(string a1) => CellAddress.TryParseA1(a1, out var address) ? address : throw new ArgumentException(a1);

    [Fact]
    public void Pasting_with_comments_replaces_the_comments_of_the_pasted_area_in_one_step()
    {
        var sheet = new Worksheet("Sheet1");
        sheet.SetValue(A("A1"), CellValue.Number(1));
        sheet.SetValue(A("A2"), CellValue.Number(2));
        sheet.Comments[A("A1")] = new CellComment { Text = "copied" };
        sheet.Comments[A("D2")] = new CellComment { Text = "old" };
        sheet.Comments[A("D3")] = new CellComment { Text = "outside" };
        var clipboard = new SheetClipboard();
        var snapshot = clipboard.Copy(sheet, new CellRange(A("A1"), A("A2")));
        var history = new UndoStack();

        clipboard.Paste(snapshot, sheet, A("D1"), A("A1"), new PasteRequest { Parts = PasteParts.Everything }, history);

        Assert.Equal("copied", sheet.Comments[A("D1")].Text);
        Assert.False(sheet.Comments.ContainsKey(A("D2")));
        Assert.Equal("outside", sheet.Comments[A("D3")].Text);

        history.Undo();

        Assert.False(sheet.Comments.ContainsKey(A("D1")));
        Assert.Equal("old", sheet.Comments[A("D2")].Text);
        Assert.False(history.CanUndo);
    }
}
