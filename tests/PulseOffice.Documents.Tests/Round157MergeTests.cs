using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Revisions;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round157MergeTests
{
    private readonly ServiceProvider _services = new ServiceCollection().AddDocumentsModel().BuildServiceProvider();

    private TextDocument Document(params (string Text, string Style)[] paragraphs)
    {
        var document = _services.GetRequiredService<ITextDocumentFactory>().Create();
        document.Body.Replace(paragraphs.Select(item => new ParagraphBlock(document.NextId("p"), item.Text, item.Style)));
        return document;
    }

    [Fact]
    public void A_merged_paragraph_keeps_the_style_it_has_in_the_other_document()
    {
        var current = Document(("Uno", DefaultStyleNames.Standard));
        var other = Document(("Uno", DefaultStyleNames.Standard), ("Titolo", DefaultStyleNames.HeadingLevel(1)));

        var count = _services.GetRequiredService<IDocumentComparison>().Merge(current, other, new UndoStack(), new RevisionSignature("Bruno", DateTimeOffset.UnixEpoch));

        Assert.True(count > 0);
        var added = Assert.IsType<ParagraphBlock>(current.Body[1]);
        Assert.Equal("Titolo", added.Text);
        Assert.Equal(DefaultStyleNames.HeadingLevel(1), added.StyleName);
        Assert.Contains(current.Changes.All, change => change is Insertion);
    }
}
