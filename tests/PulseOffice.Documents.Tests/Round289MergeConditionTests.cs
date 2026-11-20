using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round289MergeConditionTests
{
    private readonly ServiceProvider _services = new ServiceCollection().AddDocumentsModel().BuildServiceProvider();

    private static readonly MergeData Data = new(["Gender", "Name"], [["F", "Anna"], ["M", "Bruno"]]);

    private TextDocument Letter()
    {
        var document = _services.GetRequiredService<ITextDocumentFactory>().Create();
        TextRun[] runs =
        [
            new("Mr") { Field = TextFieldKind.ConditionalText, FieldArgument = "Gender == \"F\"", FieldDetail = new FieldDetail { WhenTrue = "Ms", WhenFalse = "Mr" } },
            new(" "),
            new("«Name»") { Field = TextFieldKind.Database, FieldArgument = "Name" },
        ];
        document.Body.Replace([new ParagraphBlock(document.NextId("p"), new TextParagraph(string.Concat(runs.Select(run => run.Text))) { Runs = runs })]);
        return document;
    }

    [Fact]
    public void Merged_records_resolve_conditions_on_merge_columns()
    {
        var engine = _services.GetRequiredService<IMailMergeEngine>();

        var texts = engine.Combine(Letter(), Data, [0, 1]).Body.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text).ToList();

        Assert.Equal(["Ms Anna", "Mr Bruno"], texts);
    }

    [Fact]
    public void Preview_evaluates_conditions_with_the_current_record()
    {
        var document = Letter();
        var records = _services.GetRequiredService<IMergeRecords>();
        records.Load(document, Data);
        records.Preview(document, true);

        _services.GetRequiredService<IFieldUpdater>().Update(document, new UndoStack(), new FieldUpdate(new NoFieldLayoutFacts(), DateTime.Now));

        Assert.Equal("Ms Anna", document.Body.Blocks.OfType<ParagraphBlock>().Single().Text);
    }

    [Fact]
    public void Each_merged_record_keeps_its_own_header()
    {
        var template = Letter();
        var page = template.Styles.Page.Find(template.InitialPageStyle)!;
        page.Header.Enabled = true;
        TextRun[] header = [new("«Name»") { Field = TextFieldKind.Database, FieldArgument = "Name" }];
        page.Header.Default.Replace([new ParagraphBlock(template.NextId("p"), new TextParagraph("«Name»") { Runs = header })]);

        var combined = _services.GetRequiredService<IMailMergeEngine>().Combine(template, Data, [0, 1]);
        var second = combined.Body.Blocks.OfType<ParagraphBlock>().Last().Content.PageStyleName;

        Assert.Equal("Anna", combined.Styles.Page.Find(combined.InitialPageStyle)!.Header.Default.Blocks.OfType<ParagraphBlock>().Single().Text);
        Assert.NotNull(second);
        Assert.Equal("Bruno", combined.Styles.Page.Find(second)!.Header.Default.Blocks.OfType<ParagraphBlock>().Single().Text);
    }

    [Fact]
    public void Merge_field_affixes_appear_only_around_a_value()
    {
        var document = _services.GetRequiredService<ITextDocumentFactory>().Create();
        TextRun[] runs = [new("«City»") { Field = TextFieldKind.Database, FieldArgument = "City", FieldDetail = new FieldDetail { WhenTrue = "in ", WhenFalse = "," } }];
        document.Body.Replace([new ParagraphBlock(document.NextId("p"), new TextParagraph("«City»") { Runs = runs })]);
        var data = new MergeData(["City"], [["Rome"], [""]]);

        var texts = _services.GetRequiredService<IMailMergeEngine>().Combine(document, data, [0, 1]).Body.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text);

        Assert.Equal(["in Rome,", ""], texts);
    }
}
