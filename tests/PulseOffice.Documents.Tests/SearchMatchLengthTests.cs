using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class SearchMatchLengthTests
{
    [Fact]
    public void UnaCorrispondenzaConCaratteriIgnorabiliCopreTuttoIlTestoTrovato()
    {
        var paragraph = new ParagraphBlock("p1", "pa­rola e parola");

        var found = new DocumentSearch().FindAll([paragraph], new SearchQuery("parola"));

        Assert.Equal([DocumentRange.In("p1", 0, 7), DocumentRange.In("p1", 10, 16)], found);
    }
}
