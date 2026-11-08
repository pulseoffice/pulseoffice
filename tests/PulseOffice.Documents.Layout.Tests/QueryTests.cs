using PulseOffice.Documents.Editing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class QueryTests
{
    [Fact]
    public void CursoreEPuntoSiCorrispondonoInAndataERitorno()
    {
        var f = new LayoutFixture(width: 60, height: 60);
        var a = f.Add("uno due tre quattro cinque sei sette otto");
        var b = f.Add("ab\tcd", p => p with { Align = TextAlign.Justify, Explicit = ParagraphOverrides.Align });
        var layout = f.Layout();

        foreach (var block in new[] { a, b })
        {
            foreach (var line in layout.Pages.SelectMany(page => page.Lines).Where(line => line.ParagraphId == block.Id))
            {
                for (var offset = line.Line.Start; offset < line.Line.End; offset++)
                {
                    if (block.Text[offset] == ' ' && offset == line.Line.End - 1) continue;
                    var caret = f.Queries.Caret(layout, new DocumentPosition(block.Id, offset));
                    Assert.NotNull(caret);
                    var hit = f.Queries.HitTest(layout, caret!.Value.Page, caret.Value.X + 0.1, caret.Value.Y + caret.Value.Height / 2);
                    Assert.Equal(new DocumentPosition(block.Id, offset), hit);
                }
            }
        }
    }

    [Fact]
    public void IlCursoreAFineParagrafoStaDopoLUltimoCarattere()
    {
        var f = new LayoutFixture(width: 200);
        var block = f.Add("abc");
        var layout = f.Layout();

        var caret = f.Queries.Caret(layout, new DocumentPosition(block.Id, 3))!.Value;

        Assert.Equal(18, caret.X, 3);
        Assert.Equal(12, caret.Height, 3);
    }

    [Fact]
    public void UnPuntoOltreLaRigaDaLaFineDellaRiga()
    {
        var f = new LayoutFixture(width: 200);
        var block = f.Add("abc");
        var layout = f.Layout();

        Assert.Equal(new DocumentPosition(block.Id, 3), f.Queries.HitTest(layout, 0, 150, 5));
        Assert.Equal(new DocumentPosition(block.Id, 0), f.Queries.HitTest(layout, 0, -5, 5));
    }

    [Fact]
    public void IlCursoreSiTrovaSullaPaginaGiusta()
    {
        var f = new LayoutFixture(width: 60, height: 24);
        f.Add("primo");
        f.Add("secondo");
        var third = f.Add("terzo");
        var layout = f.Layout();

        Assert.Equal(1, f.Queries.Caret(layout, new DocumentPosition(third.Id, 2))!.Value.Page);
    }
}
