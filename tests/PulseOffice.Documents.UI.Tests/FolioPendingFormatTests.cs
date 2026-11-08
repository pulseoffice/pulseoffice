using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.UI.Editing;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioPendingFormatTests
{
    [Fact]
    public Task Il_grassetto_acceso_col_cursore_fra_le_parole_vale_per_il_testo_scritto_dopo() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo ");
        scene.Editor.Toggle(CharacterToggle.Bold);
        Assert.True(scene.Editor.IsOn(CharacterToggle.Bold));
        scene.Type("forte");

        var paragraph = scene.Editor.Document.Body.Blocks.OfType<ParagraphBlock>().First();
        Assert.Contains(paragraph.Content.Runs, run => run.Text == "forte" && run.Bold);
        Assert.DoesNotContain(paragraph.Content.Runs, run => run.Text.Contains("Testo", StringComparison.Ordinal) && run.Bold);
        return Task.CompletedTask;
    });

    [Fact]
    public Task Il_grassetto_in_sospeso_si_perde_se_il_cursore_si_sposta() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        scene.Type("Uno due ");
        scene.Editor.Toggle(CharacterToggle.Bold);
        scene.Editor.Move(CaretMotion.Left, false);
        Assert.False(scene.Editor.IsOn(CharacterToggle.Bold));
        return Task.CompletedTask;
    });
}
