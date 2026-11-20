using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round289MergePreviewTests
{
    private static void Letter(TextDocument document)
    {
        TextRun[] runs = [new("Mr") { Field = TextFieldKind.ConditionalText, FieldArgument = "Gender == \"F\"", FieldDetail = new FieldDetail { WhenTrue = "Ms", WhenFalse = "Mr" } }];
        document.Body.Replace([new ParagraphBlock(document.NextId("p"), new TextParagraph("Mr") { Runs = runs })]);
    }

    [Fact]
    public Task Moving_between_records_refreshes_conditions_on_merge_columns() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene(Letter);
        var records = scene.Services.GetRequiredService<IMergeRecords>();
        records.Load(scene.Document, new MergeData(["Gender"], [["M"], ["F"]]));
        records.Preview(scene.Document, true);
        records.Move(scene.Document, 1);

        scene.Services.GetRequiredService<IFolioFieldRefresh>().ShowMergeRecord(scene.Editor);

        Assert.Equal(["Ms"], scene.Paragraphs);
        return Task.CompletedTask;
    });
}
