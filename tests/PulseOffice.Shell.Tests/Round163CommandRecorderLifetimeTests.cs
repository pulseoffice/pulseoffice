using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class Round163CommandRecorderLifetimeTests
{
    [Fact]
    public void Closing_the_document_being_recorded_ends_the_recording_and_lets_the_document_go()
    {
        var workspace = new DocumentWorkspace();
        var owner = new OpenDocument(PulseOffice.Foundation.Modules.ModuleKind.Sheets, "registrato", new Avalonia.Controls.Panel(), new PulseOffice.Document.Commands.UndoStack());
        workspace.Open(owner);
        var recorder = new CommandRecorder(new ScriptableCommandPolicy(), workspace);
        var changes = 0;
        recorder.Changed += (_, _) => changes++;
        recorder.Start();
        recorder.Note(new CommandDefinition { Id = new CommandId("ledger.format.bold"), Execute = (_, _) => Task.CompletedTask });

        workspace.Close(owner);

        Assert.False(recorder.IsRecording);
        Assert.Equal(2, changes);
        var recorded = recorder.Stop();
        Assert.Null(recorded.Owner);
        Assert.Empty(recorded.Commands);
    }
}
