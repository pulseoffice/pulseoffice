using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Navigation;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class AutoAbstractTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void IlRiassuntoPrendeITitoliEIPrimiParagrafi()
    {
        _f.Document.Body.Replace([
            new ParagraphBlock(_f.Document.NextId("p"), "Uno", DefaultStyleNames.HeadingLevel(1)),
            new ParagraphBlock(_f.Document.NextId("p"), "primo"),
            new ParagraphBlock(_f.Document.NextId("p"), "secondo"),
            new ParagraphBlock(_f.Document.NextId("p"), "Uno.uno", DefaultStyleNames.HeadingLevel(2)),
            new ParagraphBlock(_f.Document.NextId("p"), "Profondo", DefaultStyleNames.HeadingLevel(3)),
            new ParagraphBlock(_f.Document.NextId("p"), "nascosto"),
        ]);
        var abstracts = new AutoAbstract(_f.Outline, _f.Tree, _f.Factory);

        var lines = abstracts.Lines(_f.Document, 2, 1);

        Assert.Equal(["Uno", "primo", "Uno.uno"], lines.Select(line => line.Text));
        var document = abstracts.Document(lines);
        Assert.Equal(DefaultStyleNames.HeadingLevel(2), ((ParagraphBlock)document.Body[2]).StyleName);
    }
}
