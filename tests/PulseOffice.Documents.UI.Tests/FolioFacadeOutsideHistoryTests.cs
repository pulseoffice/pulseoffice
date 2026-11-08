using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.UI.Scripting;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioFacadeOutsideHistoryTests
{
    [Fact]
    public Task Turning_change_tracking_on_from_a_macro_marks_the_document_modified_and_is_not_an_undo_step() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        try
        {
            var doc = scene.Services.GetRequiredService<FolioFacade>();
            var outside = 0;
            scene.History.ChangedOutsideHistory += (_, _) => outside++;

            doc.SetTrackChanges(true);

            Assert.True(scene.Document.Changes.IsRecording);
            Assert.Equal(1, outside);
            Assert.False(scene.History.CanUndo);
        }
        finally
        {
            scene.Window.Close();
        }

        return Task.CompletedTask;
    });
}
