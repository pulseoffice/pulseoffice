using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round167IndexChangeTests
{
    private readonly Fixture _f = new();

    private sealed class NoFacts : IFieldLayoutFacts
    {
        public string? PageOf(string paragraphId) => null;

        public string? NoteCitation(string noteId) => null;

        public string? ListLabel(string paragraphId) => null;

        public int? PageCount => null;
    }

    [Fact]
    public void Changing_an_index_keeps_its_hidden_state_password_and_condition()
    {
        _f.Document.Body.Replace([new ParagraphBlock("p1", "Uno", DefaultStyleNames.HeadingLevel(1))]);
        _f.IndexEditing.Insert(_f.Document, _f.History, new DocumentPosition("p1", 0), new IndexDefinition(IndexKind.Contents), new NoFacts());
        var section = _f.IndexEditing.Indexes(_f.Document).Single();
        section.Hidden = true;
        section.ProtectionKey = [1, 2, 3];
        section.Condition = "x == 1";

        _f.IndexEditing.Change(_f.Document, _f.History, section.Id, new IndexDefinition(IndexKind.Contents) { Title = "Contents" }, new NoFacts());

        var changed = _f.IndexEditing.Indexes(_f.Document).Single();
        Assert.Equal("Contents", changed.Index!.Title);
        Assert.Equal((true, "x == 1"), (changed.Hidden, changed.Condition));
        Assert.Equal([1, 2, 3], changed.ProtectionKey);
    }
}
