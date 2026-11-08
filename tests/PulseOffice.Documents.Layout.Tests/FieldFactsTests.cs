using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class FieldFactsTests
{
    [Fact]
    public void IFattiDellImpaginazioneDannoLaPaginaDiOgniParagrafo()
    {
        var f = new LayoutFixture(width: 200, height: 48);
        var first = f.Add("uno");
        for (var i = 0; i < 6; i++) f.Add("riga");
        var last = f.Add("fine");
        var layout = f.Layout();
        var numbers = new PageNumberText([new ArabicNumbers(), new RomanNumbers(true)]);

        var facts = new FieldLayoutFactsFactory(numbers).Create(f.Document, layout);

        Assert.Equal("1", facts.PageOf(first.Id));
        Assert.Equal(layout.Pages.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), facts.PageOf(last.Id));
        Assert.Equal(layout.Pages.Count, facts.PageCount);
        f.Document.Styles.Page.Find(LayoutFixture.Page)!.NumberFormat = NumberFormat.UpperRoman;
        Assert.Equal("I", new FieldLayoutFactsFactory(numbers).Create(f.Document, f.Layout()).PageOf(first.Id));
    }
}
