using Xunit;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;

namespace PulseOffice.Shell.Tests;

public sealed class CommandRecorderTests
{
    private static CommandDefinition Command(string id) => new() { Id = new CommandId(id), Execute = (_, _) => Task.CompletedTask };

    [Theory]
    [InlineData("ledger.data.refresh")]
    [InlineData("ledger.database.refresh")]
    [InlineData("ledger.data.providerRefresh")]
    [InlineData("ledger.data.provider")]
    [InlineData("ledger.data.xmlSource")]
    [InlineData("folio.tools.updateLinks")]
    [InlineData("folio.tools.updateAll")]
    [InlineData("folio.links.update")]
    [InlineData("folio.mailMerge.run")]
    [InlineData("folio.hyperlink.open")]
    [InlineData("ledger.image.externalEdit")]
    public void Una_macro_non_raggiunge_dati_esterni_rete_o_processi_attraverso_i_comandi(string command) =>
        Assert.False(new ScriptableCommandPolicy().Allows(new CommandId(command)));

    [Theory]
    [InlineData("ledger.trace.refresh")]
    [InlineData("ledger.data.pivotRefresh")]
    [InlineData("deck.slide.add")]
    public void I_comandi_interni_al_documento_restano_permessi(string command) =>
        Assert.True(new ScriptableCommandPolicy().Allows(new CommandId(command)));

    [Fact]
    public void Commands_outside_the_undo_history_are_neither_recorded_nor_run_by_a_macro()
    {
        var protect = Command("ledger.review.protectSheet") with { OutsideHistory = true };
        var recorder = new CommandRecorder(new ScriptableCommandPolicy());
        recorder.Start();
        recorder.Note(protect);

        Assert.Empty(recorder.Stop());
        Assert.False(new ScriptableCommandPolicy().Allows(protect));
    }

    [Fact]
    public void RegistraSoloMentreEAcceso()
    {
        var recorder = new CommandRecorder(new ScriptableCommandPolicy());
        recorder.Note(Command("a"));
        recorder.Start();
        recorder.Note(Command("b"));
        recorder.Note(Command("c"));

        Assert.Equal(new[] { "b", "c" }, recorder.Stop().Select(id => id.Value).ToArray());
        Assert.False(recorder.IsRecording);
    }

    [Fact]
    public void LaMacroRichiamaIComandiInOrdine()
    {
        var script = new JavaScriptMacroWriter().Write([new CommandId("deck.slide.add"), new CommandId("x\"y")]);

        Assert.Equal("app.Run(\"deck.slide.add\");\napp.Run(\"x\\\"y\");\n", script);
    }

    [Fact]
    public void NonRegistraIlProprioInterruttoreNeIComandiCheAppRunRifiuta()
    {
        var recorder = new CommandRecorder(new ScriptableCommandPolicy());
        recorder.Start();
        recorder.Note(Command("shell.tools.recordMacro"));
        recorder.Note(Command("shell.file.save"));
        recorder.Note(Command("ledger.format.bold"));

        Assert.Equal(new[] { "ledger.format.bold" }, recorder.Stop().Select(id => id.Value).ToArray());
    }
}
