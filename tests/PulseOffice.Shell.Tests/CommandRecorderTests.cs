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
        var recorder = new CommandRecorder(new ScriptableCommandPolicy(), new DocumentWorkspace());
        recorder.Start();
        recorder.Note(protect);

        Assert.Empty(recorder.Stop().Commands);
        Assert.False(new ScriptableCommandPolicy().Allows(protect));
    }

    [Fact]
    public void RegistraSoloMentreEAcceso()
    {
        var recorder = new CommandRecorder(new ScriptableCommandPolicy(), new DocumentWorkspace());
        recorder.Note(Command("a"));
        recorder.Start();
        recorder.Note(Command("b"));
        recorder.Note(Command("c"));

        Assert.Equal(new[] { "b", "c" }, recorder.Stop().Commands.Select(id => id.Value).ToArray());
        Assert.False(recorder.IsRecording);
    }

    private static OpenDocument Document(string title) =>
        new(PulseOffice.Foundation.Modules.ModuleKind.Sheets, title, new Avalonia.Controls.Panel(), new PulseOffice.Document.Commands.UndoStack());

    [Fact]
    public void Recording_belongs_to_the_document_where_it_started_and_ignores_the_other_tabs()
    {
        var workspace = new DocumentWorkspace();
        var first = Document("primo");
        var second = Document("secondo");
        workspace.Open(first);
        var recorder = new CommandRecorder(new ScriptableCommandPolicy(), workspace);

        recorder.Start();
        recorder.Note(Command("ledger.format.bold"));
        workspace.Open(second);
        recorder.Note(Command("ledger.format.italic"));
        workspace.Activate(first);
        recorder.Note(Command("ledger.format.underline"));
        var recorded = recorder.Stop();

        Assert.Same(first, recorded.Owner);
        Assert.Equal(new[] { "ledger.format.bold", "ledger.format.underline" }, recorded.Commands.Select(id => id.Value).ToArray());
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
        var recorder = new CommandRecorder(new ScriptableCommandPolicy(), new DocumentWorkspace());
        recorder.Start();
        recorder.Note(Command("shell.tools.recordMacro"));
        recorder.Note(Command("shell.file.save"));
        recorder.Note(Command("ledger.format.bold"));

        Assert.Equal(new[] { "ledger.format.bold" }, recorder.Stop().Commands.Select(id => id.Value).ToArray());
    }
}
