using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.UI.Shell;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioSnapshotCompactnessTests
{
    private static async Task<string> Json(FolioScene scene)
    {
        var codec = scene.Services.GetRequiredService<IFolioSnapshotCodec>();
        using var stream = new MemoryStream();
        await codec.WriteAsync(scene.Document, stream);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public Task A_plain_paragraph_does_not_carry_its_default_properties() => FolioScene.RunAsync(async () =>
    {
        var json = await Json(new FolioScene(document => document.Body.Replace([new ParagraphBlock(document.NextId("p"), "Hello world")])));
        var body = json[json.IndexOf("\"body\"", StringComparison.Ordinal)..];

        Assert.DoesNotContain("\"Strikethrough\":false", body);
        Assert.DoesNotContain("\"Hyperlink\":null", body);
        Assert.DoesNotContain("\"IsListItem\"", body);
        Assert.DoesNotContain("\n", json);
    });

    [Fact]
    public Task Accented_text_is_written_unescaped() => FolioScene.RunAsync(async () =>
    {
        var json = await Json(new FolioScene(document => document.Body.Replace([new ParagraphBlock(document.NextId("p"), "perché città")])));

        Assert.Contains("perché città", json);
    });

    [Fact]
    public Task Index_level_styles_and_list_threads_survive_the_snapshot() => FolioScene.RunAsync(async () =>
    {
        var styles = PulseOffice.Documents.Indexes.IndexLevelStyles.Defaults.WithHeading("Top").WithEntry(2, "Mine");
        var scene = new FolioScene(document => document.Body.Replace([
            new ParagraphBlock(document.NextId("p"), new PulseOffice.Text.Rich.TextParagraph("Item") { ListStyleName = PulseOffice.Documents.Styles.DefaultStyleNames.Numbering123, Numbered = true, ListId = "list1" }),
            new SectionBlock("toc", "Contents", new BlockContainer([new ParagraphBlock(document.NextId("p"), "Entry")]))
            {
                Index = new PulseOffice.Documents.Indexes.IndexDefinition(PulseOffice.Documents.Indexes.IndexKind.Contents) { LevelStyles = styles },
            }]));
        var codec = scene.Services.GetRequiredService<IFolioSnapshotCodec>();
        using var stream = new MemoryStream();
        await codec.WriteAsync(scene.Document, stream);
        stream.Position = 0;
        var restored = await codec.ReadAsync(stream);

        Assert.NotNull(restored);
        Assert.Equal("list1", ((ParagraphBlock)restored.Body[0]).Content.ListId);
        Assert.Equal(styles, ((SectionBlock)restored.Body[1]).Index!.LevelStyles);
    });
}
