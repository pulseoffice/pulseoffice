using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round291MergeTests
{
    private readonly ServiceProvider _services = new ServiceCollection().AddDocumentsModel().BuildServiceProvider();

    private IMailMergeEngine Engine => _services.GetRequiredService<IMailMergeEngine>();

    private TextDocument Template(params TextRun[] runs)
    {
        var document = _services.GetRequiredService<ITextDocumentFactory>().Create();
        document.Language = "en-US";
        document.Body.Replace([new ParagraphBlock(document.NextId("p"), new TextParagraph(string.Concat(runs.Select(run => run.Text))) { Runs = runs })]);
        return document;
    }

    [Fact]
    public void Merge_field_case_switches_and_number_pictures_shape_the_value()
    {
        var template = Template(
            new("«Name»") { Field = TextFieldKind.Database, FieldArgument = "Name", FieldDetail = new FieldDetail { Format = MergeCases.Upper } },
            new(" "),
            new("«Total»") { Field = TextFieldKind.Database, FieldArgument = "Total", FieldDetail = new FieldDetail { Value = "#,##0.00" } });
        var data = new MergeData(["Name", "Total"], [["anna rossi", "1234.5"]]);

        Assert.Equal("ANNA ROSSI 1,234.50", Engine.Record(template, data, 0).Body.Blocks.OfType<ParagraphBlock>().Single().Text);
    }

    [Fact]
    public void Merge_field_caps_capitalise_each_word()
    {
        Assert.Equal("Anna De Rossi", new MergeValueFormat().Shown("anna de rossi", new FieldDetail { Format = MergeCases.Caps }, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void A_record_paragraph_switching_to_a_personal_page_style_uses_the_record_clone()
    {
        var template = Template(new TextRun("Dear "), new TextRun("«Name»") { Field = TextFieldKind.Database, FieldArgument = "Name" });
        var annex = (PageStyle)template.Styles.Page.Find(template.InitialPageStyle)!.CloneAs("Annex");
        annex.Header.Enabled = true;
        annex.Header.Default.Replace([new ParagraphBlock(template.NextId("p"), new TextParagraph("«Name»") { Runs = [new("«Name»") { Field = TextFieldKind.Database, FieldArgument = "Name" }] })]);
        template.Styles.Page.Add(annex);
        template.Body.Replace([.. template.Body.Blocks, new ParagraphBlock(template.NextId("p"), new TextParagraph("Annex") { PageStyleName = "Annex", PageBreakBefore = true })]);
        var data = new MergeData(["Name"], [["Anna"], ["Bruno"]]);

        var combined = Engine.Combine(template, data, [0, 1]);
        var annexes = combined.Body.Blocks.OfType<ParagraphBlock>().Where(paragraph => paragraph.Text == "Annex").Select(paragraph => paragraph.Content.PageStyleName!).ToList();

        Assert.Equal(2, annexes.Count);
        Assert.NotEqual(annexes[0], annexes[1]);
        Assert.Equal("Bruno", combined.Styles.Page.Find(annexes[1])!.Header.Default.Blocks.OfType<ParagraphBlock>().Single().Text);
    }

    [Fact]
    public void Recipients_leave_out_excluded_and_filtered_records()
    {
        var data = new MergeData(["Name", "Email"], [["Anna", "a@x"], ["Bruno", ""], ["Carla", "c@x"]]);

        var rows = new MergeRecipients().Rows(data, new MergeFilter("Email", MergeFilterOperator.NotEmpty, string.Empty), new HashSet<int> { 2 });

        Assert.Equal([0], rows);
    }
}
