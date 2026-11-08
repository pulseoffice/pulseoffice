using Avalonia.Input;
using PulseOffice.Document.Forms;
using PulseOffice.Text.Spelling;
using PulseOffice.UI.Commands.Shortcuts;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shortcuts;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class AuditSixteenTests
{
    [Theory]
    [InlineData(Key.PageDown, "PageDown")]
    [InlineData(Key.PageUp, "PageUp")]
    [InlineData(Key.Return, "Enter")]
    [InlineData(Key.OemSemicolon, "OemSemicolon")]
    public void LaCatturaProduceNomiCheIlLettoreRilegge(Key key, string expected)
    {
        var chord = new ShortcutCapture(false).Chord(key, KeyModifiers.Control);

        Assert.NotNull(chord);
        Assert.Equal(expected, chord!.Key);
        Assert.Equal(chord, new GestureChordParser().Parse(chord.Canonical()));
    }

    [Fact]
    public void UnTastoSconosciutoNonDiventaUnaScorciatoia() =>
        Assert.Null(new ShortcutCapture(false).Chord(Key.ImeConvert, KeyModifiers.Control));

    [Fact]
    public void UnaRinominaSoloNelleMaiuscoleAggiornaIlLegame()
    {
        var slot = new MacroEventSlot("click", "t", "d", "Click");
        var assignments = new MacroAssignments([slot], new Dictionary<string, string?> { ["click"] = "saluta" });

        new MacroBindingRules().Reconcile(
            assignments,
            [new DocumentMacro("saluta", MacroLanguage.JavaScript, "x")],
            [new DocumentMacro("Saluta", MacroLanguage.JavaScript, "x")]);

        Assert.Equal("Saluta", assignments.For("click"));
    }

    [Fact]
    public void UnProfiloRovinatoVieneMessoDaParte()
    {
        var folder = Directory.CreateTempSubdirectory("qo-ac-");
        try
        {
            var path = Path.Combine(folder.FullName, "autocorrect.json");
            File.WriteAllText(path, "{ rotto");

            var profile = new AutoCorrectProfileStore(new AutoCorrectProfileCodec(), path).Load();

            Assert.Same(AutoCorrectProfile.Default, profile);
            Assert.Equal("{ rotto", File.ReadAllText(path + ".corrupt"));
        }
        finally
        {
            folder.Delete(true);
        }
    }
}

public sealed class SettingEditorOrderTests
{
    [Fact]
    public void LEditorDiTestoVaInFondoAnchePassatoPerPrimo()
    {
        var factory = new PulseOffice.UI.Controls.Settings.SettingEditorFactory(
        [
            new PulseOffice.UI.Controls.Settings.TextSettingEditor(),
            new PulseOffice.UI.Controls.Settings.SwitchSettingEditor(),
        ]);

        var editors = (IReadOnlyList<PulseOffice.UI.Controls.Settings.ISettingEditor>)typeof(PulseOffice.UI.Controls.Settings.SettingEditorFactory)
            .GetField("_editors", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(factory)!;

        Assert.IsType<PulseOffice.UI.Controls.Settings.TextSettingEditor>(editors[^1]);
    }
}
