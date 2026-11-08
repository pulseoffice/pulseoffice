using PulseOffice.Documents.Fields;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class FieldConditionLimitsTests
{
    private readonly FieldCondition _condition = new();

    private readonly Dictionary<string, string> _variables = new(StringComparer.Ordinal);

    [Fact]
    public void UnaCondizioneAnnidataADismisuraNonEsaurisceLoStack()
    {
        var nested = new string('(', 200_000) + "1" + new string(')', 200_000);

        Assert.False(_condition.Holds(nested, _variables));
    }

    [Fact]
    public void UnaCatenaDiNegazioniADismisuraNonEsaurisceLoStack()
    {
        Assert.Equal(new string('-', 200_000) + "1", _condition.Value(new string('-', 200_000) + "1", _variables));
        Assert.False(_condition.Holds(new string('!', 200_000) + "1", _variables));
    }

    [Fact]
    public void ARepeatedlyDoubledVariableDoesNotGrowWithoutLimit()
    {
        _variables["a"] = "xxxxxxxxxx";
        for (var step = 0; step < 20; step++) _variables["a"] = _condition.Value("a + a", _variables);

        Assert.True(_variables["a"].Length <= 65_536, $"{_variables["a"].Length} characters");
    }

    [Fact]
    public void UnAnnidamentoModeratoSiValutaAncora()
    {
        Assert.True(_condition.Holds(new string('(', 50) + "2 > 1" + new string(')', 50), _variables));
    }
}

public sealed class PropertyDateFormatTests
{
    private readonly Fixture _f = new();

    [Theory]
    [InlineData(TextFieldKind.CreatedDate)]
    [InlineData(TextFieldKind.ModifiedDate)]
    public void UnFormatoDiDataNonValidoLettoDalFileNonFaCadereLAggiornamento(TextFieldKind kind)
    {
        _f.Document.Properties.Created = new DateTimeOffset(2026, 10, 10, 9, 30, 0, TimeSpan.Zero);
        _f.Document.Properties.Modified = new DateTimeOffset(2026, 10, 11, 9, 30, 0, TimeSpan.Zero);
        var run = new TextRun("x") { Field = kind, FieldDetail = new FieldDetail { Format = "%" } };
        var context = new FieldContext(_f.Document, new FieldUpdate(new NoFieldLayoutFacts(), DateTime.Now), new Dictionary<string, int>());

        var value = new PropertyFieldSource(new PulseOffice.Text.Rich.TextCultures()).Evaluate(run, context);

        Assert.False(string.IsNullOrEmpty(value!.Text));
    }
}

public sealed class StatisticFieldObjectCountTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void Tables_images_and_objects_are_counted_once_per_update_pass()
    {
        var tree = new CountingBlockTree();
        var source = new StatisticFieldSource(new PulseOffice.Documents.Editing.DocumentStatisticsCounter(), tree, new PulseOffice.Text.Rich.TextCultures());
        var context = new FieldContext(_f.Document, new FieldUpdate(new NoFieldLayoutFacts(), DateTime.Now), new Dictionary<string, int>());

        for (var field = 0; field < 50; field++)
        {
            source.Evaluate(new TextRun("0") { Field = TextFieldKind.TableCount }, context);
            source.Evaluate(new TextRun("0") { Field = TextFieldKind.ImageCount }, context);
            source.Evaluate(new TextRun("0") { Field = TextFieldKind.ObjectCount }, context);
        }

        Assert.Equal(1, tree.BlockWalks);
    }

    private sealed class CountingBlockTree : PulseOffice.Documents.Blocks.IBlockTree
    {
        private readonly PulseOffice.Documents.Blocks.BlockTree _inner = new();

        public int BlockWalks { get; private set; }

        public IEnumerable<PulseOffice.Documents.Blocks.ParagraphBlock> Paragraphs(PulseOffice.Documents.Blocks.BlockContainer root) => _inner.Paragraphs(root);

        public IEnumerable<PulseOffice.Documents.Blocks.Block> Blocks(PulseOffice.Documents.Blocks.BlockContainer root)
        {
            BlockWalks++;
            return _inner.Blocks(root);
        }

        public PulseOffice.Documents.Blocks.BlockLocation? Locate(PulseOffice.Documents.Blocks.BlockContainer root, string blockId) => _inner.Locate(root, blockId);

        public PulseOffice.Documents.Blocks.Block? Resolve(PulseOffice.Documents.Blocks.BlockContainer root, PulseOffice.Documents.Blocks.BlockPath path) => _inner.Resolve(root, path);
    }
}
