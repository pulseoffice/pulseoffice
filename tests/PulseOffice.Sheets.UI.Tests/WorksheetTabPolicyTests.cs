using PulseOffice.Sheets;
using PulseOffice.Sheets.UI;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class WorksheetTabPolicyTests
{
    private readonly WorksheetTabPolicy _policy = new();

    private static Workbook Book(params bool[] hidden)
    {
        var book = new Workbook();
        for (var i = 0; i < hidden.Length; i++)
            book.AddSheet($"S{i + 1}").IsHidden = hidden[i];
        return book;
    }

    [Fact]
    public void NonSiEliminaLUltimoFoglioVisibile()
    {
        var book = Book(false, true);

        Assert.False(_policy.CanDelete(book, 0));
        Assert.True(_policy.CanDelete(book, 1));
    }

    [Fact]
    public void NonSiEliminaLUnicoFoglio() => Assert.False(_policy.CanDelete(Book(false), 0));

    [Fact]
    public void NonSiNascondeLUltimoVisibile()
    {
        var book = Book(false, true, false);

        Assert.True(_policy.CanHide(book, 0));
        book.Sheets[2].IsHidden = true;
        Assert.False(_policy.CanHide(book, 0));
        Assert.False(_policy.CanHide(book, 1));
    }

    [Fact]
    public void RinominareCambiandoSoloLeMaiuscoleNonEUnConflitto()
    {
        var book = Book(false, false);

        Assert.False(_policy.NameTaken(book, book.Sheets[0], "s1"));
        Assert.True(_policy.NameTaken(book, book.Sheets[0], "s2"));
    }

    [Fact]
    public void DopoLEliminazioneSiSelezionaUnFoglioVisibile()
    {
        var book = Book(false, true, false);

        Assert.Equal(1, _policy.VisibleAfterRemoval(book, 0));
        Assert.Equal(0, _policy.VisibleAfterRemoval(book, 2));
        Assert.Equal(0, _policy.VisibleNear(book, 2));
    }
}
